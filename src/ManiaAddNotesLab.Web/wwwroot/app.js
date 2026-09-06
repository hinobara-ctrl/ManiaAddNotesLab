const form = document.querySelector('#batch-form');
const chart = document.querySelector('#chart');
const drop = document.querySelector('#drop-zone');
const submit = form.querySelector('button[type=submit]');
const cancel = document.querySelector('#cancel');
const error = document.querySelector('#form-error');
const results = document.querySelector('#results');
let currentJob = null;
let pollTimer = null;

const controls = {
  chanceStart: ['chanceStartOut', v => `${Math.round(v * 100)}%`],
  chanceEnd: ['chanceEndOut', v => `${Math.round(v * 100)}%`],
  chanceStep: ['chanceStepOut', v => `${Math.round(v * 100)}%`],
  runsPerChance: ['runsOut', v => v],
  densityGraceColumns: ['densityGraceOut', v => v],
  densityDecayPerColumn: ['densityDecayOut', v => `${Math.round(v * 100)}%`],
  lnWindowBeats: ['windowOut', v => `±${v} beats`],
  sourceAffinity: ['affinityOut', v => `×${v.toFixed(2)}`],
  distanceDecay: ['decayOut', v => v.toFixed(2)],
  minimumDistanceWeight: ['minimumWeightOut', v => v.toFixed(2)],
  fallbackLaneGapBeats: ['laneGapOut', v => v === 0 ? 'off' : `${Number(v.toFixed(4))} beat`]
};

for (const [name, [outputId, format]] of Object.entries(controls)) {
  const input = form.elements[name];
  input.addEventListener('input', () => {
    document.querySelector(`#${outputId}`).value = format(Number(input.value));
    updateEstimate();
  });
}

function updateEstimate() {
  const start = Number(form.elements.chanceStart.value);
  const end = Number(form.elements.chanceEnd.value);
  const step = Number(form.elements.chanceStep.value);
  const configs = start <= end ? Math.floor((end - start) / step + 1e-9) + 1 : 0;
  const runs = configs * Number(form.elements.runsPerChance.value);
  document.querySelector('#estimate').innerHTML = `<strong>${runs.toLocaleString('es')} ejecuciones</strong><span>${configs} configuraciones × ${form.elements.runsPerChance.value} seeds</span>`;
}

chart.addEventListener('change', () => setFileLabel(chart.files[0]));
for (const event of ['dragenter', 'dragover']) drop.addEventListener(event, e => { e.preventDefault(); drop.classList.add('drag'); });
for (const event of ['dragleave', 'drop']) drop.addEventListener(event, e => { e.preventDefault(); drop.classList.remove('drag'); });
drop.addEventListener('drop', e => {
  const file = e.dataTransfer.files[0];
  if (!file) return;
  const transfer = new DataTransfer();
  transfer.items.add(file);
  chart.files = transfer.files;
  setFileLabel(file);
});
function setFileLabel(file) {
  if (file) document.querySelector('#file-label').textContent = `${file.name} · ${(file.size / 1024).toFixed(1)} KB`;
}

form.addEventListener('submit', async e => {
  e.preventDefault();
  error.textContent = '';
  if (!chart.files.length) { error.textContent = 'Selecciona un chart .osu.'; return; }
  const data = new FormData(form);
  data.set('writeCharts', form.elements.writeCharts.checked);
  data.set('traceFirst', form.elements.traceFirst.checked);
  data.set('diagnosticsFirst', form.elements.diagnosticsFirst.checked);
  for (const name of ['contextualDensity', 'useLocalLaneGap', 'mapRelativeSnap', 'interiorLnOpportunities', 'articulation'])
    data.set(name, form.elements[name].checked);
  submit.disabled = true;
  cancel.hidden = false;
  results.hidden = false;
  resetResults();
  results.scrollIntoView({ behavior: 'smooth', block: 'start' });

  try {
    const response = await fetch('/api/jobs', { method: 'POST', body: data });
    const body = await response.json();
    if (!response.ok) throw new Error(body.error || 'No se pudo iniciar el lote.');
    currentJob = body.id;
    await poll();
  } catch (ex) {
    failUi(readableError(ex));
  }
});

cancel.addEventListener('click', async () => {
  if (currentJob) await fetch(`/api/jobs/${currentJob}/cancel`, { method: 'POST' });
});

async function poll() {
  if (!currentJob) return;
  try {
    const response = await fetch(`/api/jobs/${currentJob}`);
    if (!response.ok) throw new Error('No se pudo consultar el lote.');
    const job = await response.json();
    render(job);
    if (['completed', 'failed', 'cancelled'].includes(job.status)) {
      submit.disabled = false;
      cancel.hidden = true;
      return;
    }
    pollTimer = setTimeout(poll, 350);
  } catch (ex) { failUi(readableError(ex)); }
}

function render(job) {
  const pct = job.totalRuns ? job.completedRuns / job.totalRuns * 100 : 0;
  document.querySelector('#progress-bar').style.width = `${pct}%`;
  document.querySelector('#status-title').textContent = ({ queued: 'Preparando lote', running: 'Ejecutando seeds', completed: 'Lote completado', failed: 'El lote falló', cancelled: 'Lote cancelado' })[job.status] || job.status;
  document.querySelector('#status-detail').textContent = job.error || `${job.completedRuns.toLocaleString('es')} de ${job.totalRuns.toLocaleString('es')} ejecuciones · ${pct.toFixed(0)}%`;

  if (job.status === 'completed') {
    const all = job.aggregates;
    const total = all.reduce((n, r) => n + r.runs, 0);
    const mean = all.reduce((n, r) => n + r.meanPlaced * r.runs, 0) / total;
    const ln = all.reduce((n, r) => n + r.meanResultLnRatio * r.runs, 0) / total;
    const weighted = key => all.reduce((n, r) => n + r[key] * r.runs, 0) / total;
    const rejections = [['muy corta', weighted('meanInteriorRejectedTooShort')], ['contexto', weighted('meanInteriorRejectedContext')], ['anchors', weighted('meanInteriorRejectedAnchors')], ['longitud legacy', weighted('meanInteriorRejectedLegacyLength')]].sort((a, b) => b[1] - a[1]);
    document.querySelector('#summary-cards').innerHTML = cards([
      [total.toLocaleString('es'), 'ejecuciones'], [mean.toFixed(2), 'objetos añadidos μ'], [`${(ln * 100).toFixed(1)}%`, 'LN ratio resultante μ'],
      [`${weighted('meanInteriorOpportunities').toFixed(1)} / ${weighted('meanInteriorSuccessfulRolls').toFixed(1)} / ${weighted('meanInteriorPlaced').toFixed(1)}`, 'interior opp / roll / placed μ'],
      [`${weighted('meanInteriorCandidatesShort').toFixed(1)} / ${weighted('meanInteriorCandidatesMedium').toFixed(1)} / ${weighted('meanInteriorCandidatesLong').toFixed(1)}`, 'candidates short / medium / long μ'],
      [`${rejections[0][0]} · ${rejections[0][1].toFixed(1)}`, 'principal rechazo interior μ']
    ]);
    document.querySelector('#summary-body').innerHTML = all.map(r => `<tr><td>${(r.chance * 100).toFixed(0)}%</td><td>${(r.meanEffectiveChance * 100).toFixed(1)}%</td><td>${(r.meanInteriorEffectiveChance * 100).toFixed(1)}%</td><td>${r.runs}</td><td>${r.meanPlaced.toFixed(2)}</td><td>${r.minPlaced}–${r.maxPlaced}</td><td>${r.meanAddedTaps.toFixed(2)}</td><td>${r.meanAddedLn.toFixed(2)}</td><td>${r.meanInteriorOpportunities.toFixed(1)} / ${r.meanInteriorSuccessfulRolls.toFixed(1)} / ${r.meanInteriorPlaced.toFixed(1)}</td><td>${r.meanArticulationPlaced.toFixed(2)}</td><td>${r.meanInteriorCandidatesShort.toFixed(1)} / ${r.meanInteriorCandidatesMedium.toFixed(1)} / ${r.meanInteriorCandidatesLong.toFixed(1)}</td><td>${r.meanInteriorCandidatesImpossible.toFixed(1)} / ${(r.interiorImpossibleRate * 100).toFixed(1)}%</td><td>${r.meanSimultaneousHeadColumns.toFixed(2)} / ${r.meanHeldLnColumns.toFixed(2)}</td><td>${r.meanFailed.toFixed(2)}</td><td>${r.meanRetries.toFixed(2)}</td><td>${(r.meanResultLnRatio * 100).toFixed(1)}%</td></tr>`).join('');
    document.querySelector('#table-wrap').hidden = false;
    const download = document.querySelector('#download');
    download.href = `/api/jobs/${currentJob}/download`;
    download.hidden = false;
    if (job.trace) {
      document.querySelector('#trace').textContent = job.trace;
      document.querySelector('#trace-wrap').hidden = false;
    }
  }
}

function cards(items) { return items.map(([value, label]) => `<div><strong>${value}</strong><span>${label}</span></div>`).join(''); }
function resetResults() {
  clearTimeout(pollTimer);
  document.querySelector('#status-title').textContent = 'Preparando lote';
  document.querySelector('#status-detail').textContent = 'Validando chart y configuración…';
  document.querySelector('#progress-bar').style.width = '0';
  document.querySelector('#summary-cards').innerHTML = '';
  document.querySelector('#table-wrap').hidden = true;
  document.querySelector('#trace-wrap').hidden = true;
  document.querySelector('#download').hidden = true;
}
function failUi(message) {
  error.textContent = message;
  document.querySelector('#status-title').textContent = 'No se pudo ejecutar';
  document.querySelector('#status-detail').textContent = message;
  submit.disabled = false;
  cancel.hidden = true;
}

function readableError(ex) {
  if (ex instanceof TypeError && /fetch/i.test(ex.message)) {
    if (location.protocol === 'file:')
      return 'La interfaz fue abierta como archivo. Inicia el servidor y entra en http://127.0.0.1:5178.';
    return 'Se perdió la conexión con el servidor local. Mantén abierta la consola de dotnet run y vuelve a intentar.';
  }
  return ex?.message || 'Ocurrió un error inesperado.';
}

updateEstimate();
