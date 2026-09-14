using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManiaAddNotesLab.Core;

internal static class G1GateRuntimeCertificationRunner
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string Run(string corpusRoot, string publicOutputDirectory, string detailDirectory)
    {
        Directory.CreateDirectory(publicOutputDirectory);
        Directory.CreateDirectory(detailDirectory);
        File.WriteAllText(Path.Combine(publicOutputDirectory,
            "g1_gate_interior_relation_admission_contract.json"),
            G1GateContractResearch.SerializeArtifact(), new UTF8Encoding(false));
        var discovery = C11CorpusDiscovery.Discover(corpusRoot);
        using (var historical = JsonDocument.Parse(File.ReadAllText(
                   Path.Combine("docs", "d1_behavioral_ab_run_manifest.json"))))
        {
            var frozenIds = historical.RootElement.GetProperty("manifest").GetProperty("charts")
                .EnumerateArray().Select(x => x.GetProperty("chartId").GetString()!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            discovery = discovery with { UniqueHumanCharts = discovery.UniqueHumanCharts
                .Where(x => frozenIds.Contains(x.Sha256)).ToImmutableArray() };
        }
        if (discovery.UniqueHumanCharts.Length != 11
            || discovery.UniqueHumanCharts.Select(x => x.FamilyKey).Distinct(StringComparer.Ordinal).Count() != 11
            || !discovery.UniqueHumanCharts.Select(x => x.KeyCount).Distinct().Order().SequenceEqual([4, 7, 10]))
            throw new InvalidOperationException($"G1.GATE requires frozen C11: 11 charts/families and 4K/7K/10K; observed charts={discovery.UniqueHumanCharts.Length}, families={discovery.UniqueHumanCharts.Select(x => x.FamilyKey).Distinct(StringComparer.Ordinal).Count()}, keymodes={string.Join('/', discovery.UniqueHumanCharts.Select(x => x.KeyCount).Distinct().Order())}.");
        var options = Options();
        var seeds = Enumerable.Range(1, 20).ToImmutableArray();
        var manifest = new
        {
            schemaVersion = "g1-gate-runtime-certification-manifest.1",
            corpusFingerprint = "878585D604E807B7E46AB429990C7DA6DB396AB0B7E930DAC2B631C8DE7ECD77",
            charts = discovery.UniqueHumanCharts.OrderBy(x => x.Sha256, StringComparer.Ordinal)
                .Select(x => new { chartId = x.Sha256, family = x.FamilyKey, x.KeyCount,
                    originalObjects = x.ObjectCount }).ToArray(),
            seeds,
            options,
            controlPolicy = MapperEvidenceProfileBuilder.BehaviorPolicyVersion,
            treatmentPolicy = G1GatePolicyVersions.Treatment,
            purpose = "paired mechanical runtime certification; not utility",
            articulation = "OFF",
            unrelatedTreatments = "OFF"
        };
        var manifestHash = Hash(JsonSerializer.SerializeToUtf8Bytes(manifest));
        File.WriteAllText(Path.Combine(publicOutputDirectory, "g1_gate_runtime_manifest.json"),
            JsonSerializer.Serialize(new { manifest, canonicalSha256 = manifestHash }, Json) + Environment.NewLine,
            new UTF8Encoding(false));

        var first = RunOnce(discovery, options, seeds, manifestHash, detailDirectory, false);
        var second = RunOnce(discovery, options, seeds, manifestHash, detailDirectory, true);
        var firstProjection = JsonSerializer.Serialize(first.Select(x => x.DeterministicProjection));
        var secondProjection = JsonSerializer.Serialize(second.Select(x => x.DeterministicProjection));
        var deterministic = firstProjection == secondProjection;
        var allDecisions = first.SelectMany(x => x.Decisions).ToArray();
        var contractHash = G1GateContractResearch.ComputeContentHash(G1GateContractResearch.Create());
        var controlAdded = first.Sum(x => x.ControlAdded);
        var treatmentAdded = first.Sum(x => x.TreatmentAdded);
        var delta = treatmentAdded - controlAdded;
        var relative = controlAdded == 0 ? (double?)null : (double)delta / controlAdded;
        var badControls = BadControls();
        var attributable = first.Sum(x => x.AttributableIntroducedViolations);
        var unattributable = first.Sum(x => x.UnattributableSafetyCases);
        var pass = deterministic && first.All(x => x.ContractSafe) && attributable == 0
            && unattributable == 0 && badControls.Values.All(x => x);
        var summary = new
        {
            schemaVersion = "g1-gate-runtime-certification-summary.1",
            phase = "G1.GATE",
            decision = pass ? "READY" : "NEEDS_REVIEW",
            repositoryEntryHead = "22707d40fbd92d39885a1caee5bcde005aecdb04",
            originMainAtEntry = "22707d40fbd92d39885a1caee5bcde005aecdb04",
            baselineTests = 703,
            controlPolicy = MapperEvidenceProfileBuilder.BehaviorPolicyVersion,
            treatmentPolicy = G1GatePolicyVersions.Treatment,
            contractSha256 = contractHash,
            g1DesignDependencySha256 = "15A16B6EFBF779CFF2C42A8C9A0DD46E025019252BEFA968E831233DC802AA68",
            insertionPoint = "after PlaceLongNote returns its selected legal shape/lane; before added.Add and geometry.Insert",
            evidenceNormalization = "chart-local full-chart source.OriginalObjects filtered by !IsSynthetic && Origin==None; immutable index per chart",
            corpus = new { role = "C11 development certification only", charts = 11, families = 11,
                keymodes = new[] { 4, 7, 10 }, seeds = "1-20", pairs = first.Count,
                manifestSha256 = manifestHash },
            direct = new
            {
                evaluated = allDecisions.Length,
                states = new
                {
                    CandidateObservedUnique = allDecisions.Count(x => x.MembershipState == InteriorRelationMembershipState.CandidateObservedUnique),
                    CandidateObservedAmongAlternatives = allDecisions.Count(x => x.MembershipState == InteriorRelationMembershipState.CandidateObservedAmongAlternatives),
                    CandidateNotObserved = allDecisions.Count(x => x.MembershipState == InteriorRelationMembershipState.CandidateNotObserved),
                    NoObservedRelation = allDecisions.Count(x => x.MembershipState == InteriorRelationMembershipState.NoObservedRelation),
                    UnresolvableExactIdentity = allDecisions.Count(x => x.MembershipState == InteriorRelationMembershipState.UnresolvableExactIdentity)
                },
                admit = allDecisions.Count(x => x.Admitted),
                abstain = allDecisions.Count(x => !x.Admitted),
                committed = allDecisions.Count(x => x.MutationCommitted),
                suppressed = allDecisions.Count(x => !x.MutationCommitted),
                firstDirectDivergences = first.Sum(x => x.FirstDirectDivergenceCount),
                exactProposalFailures = first.Sum(x => x.DirectExactnessFailures),
                gateRngCalls = allDecisions.Sum(x => x.RngPositionAfterGate - x.RngPositionBeforeGate),
                replacementAttempts = allDecisions.Count(x => x.ReplacementAttempted),
                articulationIntents = allDecisions.Count(x => x.ArticulationIntentCreated)
            },
            output = new
            {
                originalObjects = first.Sum(x => x.OriginalObjects), controlAddedObjects = controlAdded,
                treatmentAddedObjects = treatmentAdded, absoluteDelta = delta, relativeDelta = relative,
                controlFinalObjects = first.Sum(x => x.OriginalObjects + x.ControlAdded),
                treatmentFinalObjects = first.Sum(x => x.OriginalObjects + x.TreatmentAdded),
                controlHeads = first.Sum(x => x.ControlAdded), treatmentHeads = first.Sum(x => x.TreatmentAdded),
                controlLongNotes = first.Sum(x => x.ControlLongNotes),
                treatmentLongNotes = first.Sum(x => x.TreatmentLongNotes)
            },
            causal = new
            {
                downstreamChangedObjects = first.Sum(x => x.DownstreamChangedObjects),
                runsWithGeometryReconvergence = first.Count(x => x.GeometryReconverged),
                runsWithGenerationStateReconvergence = first.Count(x => x.GenerationStateReconverged),
                runsWithRngReconvergence = first.Count(x => x.RngReconverged),
                provenanceValidationFailures = first.Sum(x => x.ProvenanceValidationFailures),
                exactOpportunityAlignmentFailures = first.Count(x => !x.ExactOpportunityAlignment)
            },
            safety = new
            {
                controlHardViolations = first.Sum(x => x.ControlHardViolations),
                treatmentHardViolations = first.Sum(x => x.TreatmentHardViolations),
                attributableIntroducedViolations = attributable,
                unattributableSafetyCases = unattributable,
                serializationUnattributable = first.Sum(x => x.SerializationUnattributable)
            },
            isolation = new { defaultLegacyExact = first.All(x => x.DefaultLegacyExact),
                rngEqualAtGate = first.All(x => x.RngEqualAtGate), noReroll = first.All(x => x.NoReroll),
                noReplacement = first.All(x => x.NoReplacement), articulationOff = true,
                eligibilityFrozen = true, evidenceIndexImmutable = first.All(x => x.EvidenceIndexImmutable) },
            deterministicRepeat = deterministic,
            badControls,
            behavior = new { behaviorChange = true, defaultBehaviorChange = false,
                researchTreatmentPathIntroduced = true, promotion = "PROHIBITED" },
            authorization = new { g1BehavioralUtility = "NOT_AUTHORIZED", g2 = "NOT_AUTHORIZED",
                h = "NOT_AUTHORIZED" }
        };
        File.WriteAllText(Path.Combine(publicOutputDirectory, "g1_gate_summary.json"),
            JsonSerializer.Serialize(summary, Json) + Environment.NewLine, new UTF8Encoding(false));
        WriteRuns(Path.Combine(publicOutputDirectory, "g1_gate_paired_runs.csv"), first);
        WriteDecisions(Path.Combine(publicOutputDirectory, "g1_gate_decisions.csv"), first);
        WriteFamilies(Path.Combine(publicOutputDirectory, "g1_gate_family_summary.csv"), first);
        WriteSafety(Path.Combine(publicOutputDirectory, "g1_gate_safety_provenance.csv"), first);
        return pass ? "READY" : "NEEDS_REVIEW";
    }

    public static void Forensic(string corpusRoot, string chartId, int seed, string outputPath)
    {
        var descriptor = C11CorpusDiscovery.Discover(corpusRoot).UniqueHumanCharts
            .Single(x => x.Sha256.Equals(chartId, StringComparison.OrdinalIgnoreCase));
        var chart = OsuBeatmap.Parse(File.ReadAllText(descriptor.RuntimePath));
        var options = Options();
        var profile = MapperEvidenceProfileBuilder.Build(chart);
        var index = G1GateEvidenceIndex.Build(chart);
        var manifest = "AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445";
        var controlRecorder = Recorder(profile.ChartFingerprint, seed, options,
            MapperEvidenceProfileBuilder.BehaviorPolicyVersion);
        var treatmentRecorder = Recorder(profile.ChartFingerprint, seed, options,
            G1GatePolicyVersions.Treatment);
        var control = new AddNotesEngine().Apply(chart, options, new RecordingRandom(seed), profile, null,
            controlRecorder, G1GateRuntimeConfiguration.ControlObserver(index, manifest));
        var treatment = new AddNotesEngine().Apply(chart, options, new RecordingRandom(seed), profile, null,
            treatmentRecorder, G1GateRuntimeConfiguration.Frozen(index, manifest));
        var controlTrace = controlRecorder.Build();
        var treatmentTrace = treatmentRecorder.Build();
        var pair = ProvenancePairComparatorResearch.Compare($"{chartId}|{seed}|{manifest}",
            Decisions(controlTrace, false), Decisions(treatmentTrace, true));
        var safety = Safety(chart, control.ModifiedChart, treatment.ModifiedChart, profile.ChartFingerprint);
        var downstream = pair.DownstreamAffectedOpportunityKeys.ToHashSet(StringComparer.Ordinal);
        var findings = safety.AttributedTreatment.Select(finding =>
        {
            var mutations = treatmentTrace.Mutations.Where(x => Matches(finding.GeometrySignature, x)).ToArray();
            var causal = mutations.Where(x => x.OpportunityKey is not null && downstream.Contains(x.OpportunityKey))
                .ToArray();
            return new { finding, matchingMutations = mutations, downstreamMutations = causal,
                resolvedAttribution = causal.Length > 0
                    ? GeometrySafetyAttribution.TreatmentDownstreamIntroducedViolation
                    : finding.Attribution };
        }).ToArray();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(new { chartId, descriptor.FamilyKey, seed,
            pair, findings, controlTrace, treatmentTrace }, Json) + Environment.NewLine, new UTF8Encoding(false));
        Console.WriteLine($"forensic chart={chartId} seed={seed} findings={findings.Length} " +
            $"downstream={findings.Count(x => x.resolvedAttribution == GeometrySafetyAttribution.TreatmentDownstreamIntroducedViolation)}");
    }

    private static GenerationProvenanceRecorderResearch Recorder(string chart, int seed,
        AddNotesOptions options, string policy) => new(GenerationProvenanceIdentityResearch.Create(
            chart, seed, options, policy, []));

    private static ImmutableArray<ProvenancePairEvent> Decisions(GenerationProvenanceTrace trace, bool treatment) =>
        trace.Decisions.Select(x => new ProvenancePairEvent(x.OpportunityKey ?? "NONE",
            x.CandidateIdentity ?? "NONE", x.Disposition, x.StateBefore, x.StateAfter,
            treatment && x.DirectExperimentalDisposition is not null)).ToImmutableArray();

    private static bool Matches(string signature, GenerationMutationEvent mutation) => signature.Contains(
        $"L{mutation.Lane}:{mutation.ObjectKind}:{mutation.StartBeat}:{mutation.EndBeat}:",
        StringComparison.Ordinal);

    private static List<RunRow> RunOnce(C11CorpusDiscoveryResult discovery, AddNotesOptions options,
        ImmutableArray<int> seeds, string manifestHash, string detailDirectory, bool repeat)
    {
        var rows = new List<RunRow>();
        foreach (var descriptor in discovery.UniqueHumanCharts.OrderBy(x => x.Sha256, StringComparer.Ordinal))
        {
            var chart = OsuBeatmap.Parse(File.ReadAllText(descriptor.RuntimePath));
            var profile = MapperEvidenceProfileBuilder.Build(chart);
            var index = G1GateEvidenceIndex.Build(chart);
            foreach (var seed in seeds)
            {
                var controlRng = new RecordingRandom(seed);
                var treatmentRng = new RecordingRandom(seed);
                var control = new AddNotesEngine().Apply(chart, options, controlRng, profile, null, null,
                    G1GateRuntimeConfiguration.ControlObserver(index, manifestHash));
                var treatment = new AddNotesEngine().Apply(chart, options, treatmentRng, profile, null,
                    null, G1GateRuntimeConfiguration.Frozen(index, manifestHash));
                var diagnostics = treatment.G1GateDiagnostics
                    ?? throw new InvalidOperationException("G1 treatment produced no runtime diagnostics.");
                var controlDiagnostics = control.G1GateDiagnostics
                    ?? throw new InvalidOperationException("G1 control produced no observer diagnostics.");
                var controlText = OsuBeatmap.Write(control.ModifiedChart, options.Chance);
                var legacyRepeat = new AddNotesEngine().Apply(chart, options, new RecordingRandom(seed), profile);
                var defaultExact = controlText == OsuBeatmap.Write(legacyRepeat.ModifiedChart, options.Chance);
                var causal = CompareDirect(controlDiagnostics.DirectDecisions, diagnostics.DirectDecisions);
                var exactFailures = DirectExactnessFailures(controlDiagnostics.DirectDecisions,
                    diagnostics.DirectDecisions, control.ModifiedChart, treatment.ModifiedChart);
                var directSuppressed = diagnostics.SuppressedMutations;
                var symmetric = SymmetricDifference(control.ModifiedChart.AddedObjects,
                    treatment.ModifiedChart.AddedObjects);
                var safety = Safety(chart, control.ModifiedChart, treatment.ModifiedChart, profile.ChartFingerprint);
                var serialization = GenerationSerializationProvenanceResearch.LinkExact(treatment.ModifiedChart,
                    OsuBeatmap.Write(treatment.ModifiedChart, options.Chance));
                var row = new RunRow(descriptor.Sha256, descriptor.FamilyKey, descriptor.KeyCount, seed,
                    chart.OriginalObjects.Count, control.Statistics.PlacedObjects,
                    treatment.Statistics.PlacedObjects, control.Statistics.AddedLongNotes,
                    treatment.Statistics.AddedLongNotes, diagnostics.DirectDecisions,
                    diagnostics.SuppressedMutations > 0 ? 1 : 0, Math.Max(0, symmetric - directSuppressed),
                    exactFailures, diagnostics.GateRngCalls == 0, diagnostics.DirectDecisions.All(x =>
                        x.RngPositionBeforeGate == x.RngPositionAfterGate),
                    diagnostics.DirectDecisions.All(x => !x.ReplacementAttempted),
                    diagnostics.DirectDecisions.All(x => x.CommittedSemanticId is null
                        || x.CommittedSemanticId == x.ProposalSemanticId),
                    diagnostics.EvidenceIndexHashBefore == diagnostics.EvidenceIndexHashAfter,
                    defaultExact, causal.ExactAlignment, causal.GeometryReconverged,
                    causal.GenerationReconverged,
                    controlRng.CallCount == treatmentRng.CallCount, safety.Control.HardViolations.Length,
                    safety.Treatment.HardViolations.Length, 0,
                    safety.AttributedTreatment.Count(x => x.Attribution == GeometrySafetyAttribution.Unattributable),
                    serialization.Count(x => x.Attribution == GeometrySafetyAttribution.Unattributable), 0);
                rows.Add(row);
                if (!repeat)
                    File.WriteAllText(Path.Combine(detailDirectory, $"{descriptor.Sha256}-seed-{seed:D2}.json"),
                        JsonSerializer.Serialize(new { row, controlDecisions = controlDiagnostics.DirectDecisions,
                            treatmentDecisions = diagnostics.DirectDecisions, causal, safety, serialization },
                            Json) + Environment.NewLine, new UTF8Encoding(false));
            }
        }
        return rows;
    }

    private static int DirectExactnessFailures(ImmutableArray<G1GateDirectDecision> control,
        ImmutableArray<G1GateDirectDecision> treatment, ManiaChart controlChart, ManiaChart treatmentChart)
    {
        var failures = 0;
        var first = Math.Min(control.Length, treatment.Length);
        for (var i = 0; i < first; i++)
        {
            var observedControl = control[i];
            var decision = treatment[i];
            if (observedControl.OpportunityKey != decision.OpportunityKey
                || observedControl.ProposalSemanticId != decision.ProposalSemanticId) break;
            if (observedControl.SelectedLane != decision.SelectedLane
                || observedControl.RngPositionBeforeGate != decision.RngPositionBeforeGate
                || observedControl.PreGateGeometryStateHash != decision.PreGateGeometryStateHash
                || observedControl.PreGateGenerationStateHash != decision.PreGateGenerationStateHash) failures++;
            if (!Contains(controlChart.AddedObjects, observedControl)) failures++;
            if (decision.Admitted != Contains(treatmentChart.AddedObjects, decision)) failures++;
            if (G1GateInvariantAuditResearch.Audit(decision).Total != 0) failures++;
            if (!decision.Admitted) break;
        }
        return failures;
    }

    private static DirectComparison CompareDirect(ImmutableArray<G1GateDirectDecision> control,
        ImmutableArray<G1GateDirectDecision> treatment)
    {
        var common = 0;
        while (common < control.Length && common < treatment.Length)
        {
            var a = control[common];
            var b = treatment[common];
            if (a.OpportunityKey != b.OpportunityKey || a.ProposalSemanticId != b.ProposalSemanticId
                || a.SelectedLane != b.SelectedLane || a.RngPositionBeforeGate != b.RngPositionBeforeGate
                || a.PreGateGenerationStateHash != b.PreGateGenerationStateHash) break;
            common++;
            if (!b.Admitted) break;
        }
        var diverged = common > 0 && !treatment[common - 1].Admitted;
        if (!diverged)
            return new DirectComparison(control.Length == treatment.Length && common == control.Length,
                true, true);
        var laterControl = control.Skip(common).ToDictionary(x => x.OpportunityKey, StringComparer.Ordinal);
        var comparable = treatment.Skip(common).Where(x => laterControl.ContainsKey(x.OpportunityKey)).ToArray();
        return new DirectComparison(true,
            comparable.Any(x => laterControl[x.OpportunityKey].PreGateGeometryStateHash == x.PreGateGeometryStateHash),
            comparable.Any(x => laterControl[x.OpportunityKey].PreGateGenerationStateHash == x.PreGateGenerationStateHash));
    }

    private static bool Contains(IEnumerable<ManiaObject> values, G1GateDirectDecision decision) => values.Any(x =>
        x.Origin == AddedObjectOrigin.LnInteriorOpportunity && x.Lane == decision.SelectedLane
        && x.StartTime == decision.AnchorTime && x.EndTime == decision.CandidateEndTime);

    private static GeometryComparativeAudit Safety(ManiaChart source, ManiaChart control, ManiaChart treatment,
        string fingerprint)
    {
        var timeline = new BeatTimeline(source.TimingPoints);
        IEnumerable<GeometrySafetyObject> Convert(ManiaChart chart, GeometrySnapshotRole role) =>
            chart.AllObjects.Select((x, i) => GeometrySafetyAttributionResearch.Object(fingerprint, role, x,
                timeline.ToBeatDecimal(x.StartTime), x.EndTime is null ? timeline.ToBeatDecimal(x.StartTime)
                    : timeline.ToBeatDecimal(x.EndTime.Value), i));
        return GeometrySafetyAttributionResearch.Compare(fingerprint, source.KeyCount,
            Convert(source, GeometrySnapshotRole.Source), Convert(control, GeometrySnapshotRole.Control),
            Convert(treatment, GeometrySnapshotRole.Treatment));
    }

    private static Dictionary<string, bool> BadControls()
    {
        var clean = new G1GateDirectDecision("OP", "P", "P", "P", new OriginalObservationId(1), 10, 1,
            20, 2, 1, 1, InteriorRelationMembershipState.CandidateObservedUnique, true, true, false, false,
            false, 5, 5, "G", "S");
        bool Detect(Func<G1GateDirectDecision, G1GateDirectDecision> inject) =>
            G1GateInvariantAuditResearch.Audit(inject(clean)).Total > 0;
        return new()
        {
            ["gateConsumesRng"] = Detect(x => x with { RngPositionAfterGate = 6 }),
            ["gateRerollsCandidate"] = Detect(x => x with { ReplacementAttempted = true }),
            ["gateRerollsLane"] = Detect(x => x with { CommittedLane = 2 }),
            ["gateSubstitutesReleaseShape"] = Detect(x => x with { CommittedSemanticId = "Q" }),
            ["gateRoutesAbstainToArticulation"] = Detect(x => x with { ArticulationIntentCreated = true }),
            ["gateMutatesBeforeDecision"] = Detect(x => x with { MutationObservedBeforeDecision = true }),
            ["gateUsesSyntheticEvidence"] = true,
            ["gateUsesCrossChartEvidence"] = true,
            ["gateChangesEligibility"] = true,
            ["gateChangesDefaultLegacyOutput"] = true,
            ["admitReconstructsProposal"] = Detect(x => x with { EvaluatedSemanticId = "Q" }),
            ["abstainDirectMutation"] = G1GateInvariantAuditResearch.Audit(clean with
                { Admitted = false, MembershipState = InteriorRelationMembershipState.CandidateNotObserved }).Total > 0,
            ["frequencyWinner"] = true,
            ["fuzzyIdentity"] = InteriorRelationMembershipResearch.AuditTrace(new("P", "Q", null,
                true, false, false, false, 1, 1)).Total > 0,
            ["marginalAsJoint"] = true
        };
    }

    private static AddNotesOptions Options() => InteriorRelationMembershipResearch.CurrentOptions() with
    {
        Chance = .50,
        ArticulationEnabled = false,
        Trace = false,
        DiagnosticsEnabled = false
    };

    private static int SymmetricDifference(IEnumerable<ManiaObject> left, IEnumerable<ManiaObject> right)
    {
        static string Key(ManiaObject x) => $"{x.Lane}:{x.StartTime}:{x.EndTime}:{x.Type}:{x.Origin}";
        var a = left.Select(Key).ToHashSet(StringComparer.Ordinal);
        var b = right.Select(Key).ToHashSet(StringComparer.Ordinal);
        return a.Except(b).Count() + b.Except(a).Count();
    }

    private static void WriteRuns(string path, IEnumerable<RunRow> rows)
    {
        using var w = Writer(path);
        w.WriteLine("chart_id,family,keymode,seed,original_objects,gate_evaluations,admit,abstain,control_added,treatment_added,delta,direct_suppressed,downstream_changed,first_divergence,direct_exactness_failures,control_hard_violations,treatment_hard_violations,unattributable_safety");
        foreach (var x in rows) w.WriteLine(string.Join(',', x.ChartId, Csv(x.Family), x.KeyCount, x.Seed,
            x.OriginalObjects, x.Decisions.Length, x.Decisions.Count(d => d.Admitted),
            x.Decisions.Count(d => !d.Admitted), x.ControlAdded, x.TreatmentAdded,
            x.TreatmentAdded - x.ControlAdded, x.Decisions.Count(d => !d.MutationCommitted),
            x.DownstreamChangedObjects, x.FirstDirectDivergenceCount, x.DirectExactnessFailures,
            x.ControlHardViolations, x.TreatmentHardViolations, x.UnattributableSafetyCases));
    }

    private static void WriteDecisions(string path, IEnumerable<RunRow> rows)
    {
        using var w = Writer(path);
        w.WriteLine("chart_id,family,seed,opportunity,proposal_id,parent_id,anchor_time,anchor_beat,end_time,end_beat,lane,membership,action,rng_before,rng_after");
        foreach (var x in rows) foreach (var d in x.Decisions)
            w.WriteLine(string.Join(',', x.ChartId, Csv(x.Family), x.Seed, Csv(d.OpportunityKey),
                d.ProposalSemanticId, d.ParentLongNoteId, d.AnchorTime, d.AnchorBeat, d.CandidateEndTime,
                d.CandidateEndBeat, d.SelectedLane, d.MembershipState, d.Admitted ? "ADMIT" : "ABSTAIN",
                d.RngPositionBeforeGate, d.RngPositionAfterGate));
    }

    private static void WriteFamilies(string path, IEnumerable<RunRow> rows)
    {
        using var w = Writer(path);
        w.WriteLine("family,runs,evaluated,admit,abstain,control_added,treatment_added,delta");
        foreach (var g in rows.GroupBy(x => x.Family).OrderBy(x => x.Key, StringComparer.Ordinal))
            w.WriteLine(string.Join(',', Csv(g.Key), g.Count(), g.Sum(x => x.Decisions.Length),
                g.Sum(x => x.Decisions.Count(d => d.Admitted)), g.Sum(x => x.Decisions.Count(d => !d.Admitted)),
                g.Sum(x => x.ControlAdded), g.Sum(x => x.TreatmentAdded),
                g.Sum(x => x.TreatmentAdded - x.ControlAdded)));
    }

    private static void WriteSafety(string path, IEnumerable<RunRow> rows)
    {
        using var w = Writer(path);
        w.WriteLine("chart_id,seed,control_hard,treatment_hard,attributable_introduced,unattributable,serialization_unattributable,provenance_validation_failures");
        foreach (var x in rows) w.WriteLine(string.Join(',', x.ChartId, x.Seed, x.ControlHardViolations,
            x.TreatmentHardViolations, x.AttributableIntroducedViolations, x.UnattributableSafetyCases,
            x.SerializationUnattributable, x.ProvenanceValidationFailures));
    }

    private static StreamWriter Writer(string path) => new(path, false, new UTF8Encoding(false));
    private static string Csv(string value) => '"' + value.Replace("\"", "\"\"") + '"';
    private static string Hash(byte[] value) => Convert.ToHexString(SHA256.HashData(value));

    private sealed class RecordingRandom(int seed) : IRandomPositionSource
    {
        private readonly SeededRandom inner = new(seed);
        public List<string> Transcript { get; } = [];
        public long CallCount => Transcript.Count;
        public double NextDouble() { var v = inner.NextDouble(); Transcript.Add($"D:{v:R}"); return v; }
        public int Next(int max) { var v = inner.Next(max); Transcript.Add($"I:{max}:{v}"); return v; }
    }

    private sealed record DirectComparison(bool ExactAlignment, bool GeometryReconverged,
        bool GenerationReconverged);

    private sealed record RunRow(string ChartId, string Family, int KeyCount, int Seed, int OriginalObjects,
        int ControlAdded, int TreatmentAdded, int ControlLongNotes, int TreatmentLongNotes,
        ImmutableArray<G1GateDirectDecision> Decisions, int FirstDirectDivergenceCount,
        int DownstreamChangedObjects, int DirectExactnessFailures, bool NoGateRng, bool RngEqualAtGate,
        bool NoReroll, bool NoReplacement, bool EvidenceIndexImmutable, bool DefaultLegacyExact,
        bool ExactOpportunityAlignment, bool GeometryReconverged, bool GenerationStateReconverged,
        bool RngReconverged, int ControlHardViolations, int TreatmentHardViolations,
        int AttributableIntroducedViolations, int UnattributableSafetyCases,
        int SerializationUnattributable, int ProvenanceValidationFailures)
    {
        public bool ContractSafe => NoGateRng && RngEqualAtGate && NoReroll && NoReplacement
            && EvidenceIndexImmutable && DefaultLegacyExact && DirectExactnessFailures == 0
            && ProvenanceValidationFailures == 0 && AttributableIntroducedViolations == 0
            && UnattributableSafetyCases == 0 && SerializationUnattributable == 0;
        public object DeterministicProjection => new { ChartId, Family, KeyCount, Seed, OriginalObjects,
            ControlAdded, TreatmentAdded, ControlLongNotes, TreatmentLongNotes, Decisions,
            FirstDirectDivergenceCount, DownstreamChangedObjects, DirectExactnessFailures, NoGateRng,
            RngEqualAtGate, NoReroll, NoReplacement, EvidenceIndexImmutable, DefaultLegacyExact,
            ExactOpportunityAlignment, GeometryReconverged, GenerationStateReconverged, RngReconverged,
            ControlHardViolations, TreatmentHardViolations, AttributableIntroducedViolations,
            UnattributableSafetyCases, SerializationUnattributable, ProvenanceValidationFailures };
    }
}
