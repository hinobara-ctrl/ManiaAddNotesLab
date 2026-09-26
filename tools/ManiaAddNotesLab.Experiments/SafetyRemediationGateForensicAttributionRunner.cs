using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManiaAddNotesLab.Core;

internal static class SafetyRemediationGateForensicAttributionRunner
{
    private const string MemoryLimit = "0x400000000";
    private const string ManifestHash = "AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445";
    private const string Chart02 = "02D9D1781418E7442956D4D901C8C611E58256556AE9A828E72128C3B5B8E3EB";
    private const string Chart206 = "20651C9B11DBB0D7BA2D64A8F167577AE0452762EEA83C5A7558BA89B8D2C788";
    private const string Op370 = "OP-00000370-BaseHead-S370-T29933-ANA";
    private const string Op466 = "OP-00000466-BaseHead-S466-T35009-ANA";
    private static readonly ImmutableArray<RunIdentity> Runs =
    [
        new("primary", Chart206, 4, 10007),
        new("primary", Chart02, 5, 4823),
        new("primary", Chart02, 20, 5305),
        new("primary", Chart206, 10, 10144),
        new("primary", Chart206, 11, 10137),
        new("primary", Chart206, 14, 10141),
        new("primary", Chart206, 19, 10141),
        new("secondary-g1-compatibility", Chart206, 11, 10137)
    ];
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string Run(string corpusRoot, string manifestPath, string outputDirectory)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("DOTNET_GCHeapHardLimit"), MemoryLimit,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Forensic run requires DOTNET_GCHeapHardLimit={MemoryLimit}.");
        var manifest = FrozenC11Manifest.Load(manifestPath);
        if (!string.Equals(manifest.CanonicalSha256, ManifestHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Frozen C11 manifest identity drifted.");
        var verification = C11CorpusDiscovery.ResolveFrozen(corpusRoot, manifest.Charts);
        if (verification.Charts.Length != 11)
            throw new InvalidDataException("The frozen C11 corpus did not resolve to exactly 11 charts.");
        Directory.CreateDirectory(outputDirectory);
        var results = new List<RunResult>();
        foreach (var identity in Runs)
        {
            Console.WriteLine($"FORENSIC {identity.Stratum} chart={identity.ChartId} seed={identity.Seed}");
            var descriptor = verification.Charts.Single(x => x.Sha256 == identity.ChartId);
            var chart = OsuBeatmap.Parse(File.ReadAllText(descriptor.RuntimePath));
            results.Add(Evaluate(identity, chart));
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: false);
            GC.WaitForPendingFinalizers();
        }
        var total = results.Sum(x => x.UnattributedObservations);
        var artifact = new
        {
            schemaVersion = "safety-remediation-gate-forensic-attribution.1",
            behaviorChanged = false,
            fullMatrixExecuted = false,
            memoryLimit = MemoryLimit,
            corpusManifestSha256 = manifest.CanonicalSha256,
            declaredHistoricalUnattributedObservations = 70835,
            reproducedUnattributedObservations = total,
            exactHistoricalCountMatch = total == 70835,
            runCount = results.Count,
            distinctChartSeedCount = results.Select(x => (x.ChartId, x.Seed)).Distinct().Count(),
            results
        };
        var jsonPath = Path.Combine(outputDirectory, "forensic_attribution_runs.json");
        WriteJson(jsonPath, artifact);
        WriteCsv(Path.Combine(outputDirectory, "forensic_attribution_runs.csv"), results);
        WriteHashes(outputDirectory);
        return total == 70835 && results.All(x => x.ReferenceExact && x.TreatmentRepeatExact
                && x.UnattributedObservations == x.HistoricalUnattributedObservations)
            ? "EVIDENCE_REPRODUCED" : "NEEDS_REVIEW";
    }

    private static RunResult Evaluate(RunIdentity identity, ManiaChart chart)
    {
        var options = InteriorRelationMembershipResearch.CurrentOptions() with
            { Chance = .50, ArticulationEnabled = false, Trace = false, DiagnosticsEnabled = false };
        var profile = MapperEvidenceProfileBuilder.Build(chart);
        var g1 = identity.Stratum == "secondary-g1-compatibility"
            ? G1GateRuntimeConfiguration.Frozen(G1GateEvidenceIndex.Build(chart), ManifestHash) : null;
        var reference = Execute(chart, options, profile, identity.Seed, g1, null);
        var control = Execute(chart, options, profile, identity.Seed, g1, false);
        var treatment = Execute(chart, options, profile, identity.Seed, g1, true);
        var repeat = Execute(chart, options, profile, identity.Seed, g1, true);
        var referenceExact = Equivalent(reference, control, compareDiagnostics: false);
        var repeatExact = Equivalent(treatment, repeat, compareDiagnostics: true);
        if (!referenceExact || !repeatExact)
            throw new InvalidDataException($"Instrumentation/reference equivalence failed for {identity}.");
        var left = control.Diagnostics!.OpportunityStates;
        var right = treatment.Diagnostics!.OpportunityStates;
        if (left.Length != right.Length)
            throw new InvalidDataException("Control/treatment opportunity counts differ.");
        var rejectedOrders = control.Diagnostics.CandidateDecisions.Where(x =>
                SafetyRemediationGateHardeningResearch.CanonicalAuthorityRejectedSelectedCommit([x]))
            .Select(x => x.OpportunityOrder).ToHashSet();
        var paired = left.Zip(right).Select(pair =>
        {
            if (pair.First.OpportunityOrder != pair.Second.OpportunityOrder
                || pair.First.OpportunityKey != pair.Second.OpportunityKey)
                throw new InvalidDataException("Control/treatment opportunity identity drifted.");
            return new SafetyRemediationGatePairedOpportunity(pair.First.OpportunityKey,
                pair.First.OpportunityOrder, pair.First.StateBefore, pair.Second.StateBefore,
                pair.First.StateAfter, pair.Second.StateAfter, pair.First.CommittedObjectIdentity,
                pair.Second.CommittedObjectIdentity, rejectedOrders.Contains(pair.First.OpportunityOrder));
        }).ToImmutableArray();
        var lineage = SafetyRemediationGateHardeningResearch.Classify(
            $"{identity.Stratum}|{identity.ChartId}|{identity.Seed}", paired);
        var episodes = SafetyRemediationForensicAttributionResearch.GroupEpisodes(lineage);
        var first = FindFirstDifference(paired);
        var firstInspection = Inspect(paired.Single(x =>
            x.OpportunityOrder == first.OpportunityOrder), control, treatment, lineage);
        var firstRelevantReject = paired.Where(x => x.OpportunityOrder >= first.OpportunityOrder
                && x.CanonicalAuthorityChangedCommit).Select(x => Inspect(x, control, treatment, lineage))
            .FirstOrDefault();
        var op370 = identity.ChartId == Chart206 && identity.Seed == 4
            ? Inspect(paired.Single(x => x.OpportunityKey == Op370), control, treatment, lineage) : null;
        var op466 = identity.ChartId == Chart206 && identity.Seed == 4
            ? Inspect(paired.Single(x => x.OpportunityKey == Op466), control, treatment, lineage) : null;
        return new(identity.Stratum, identity.ChartId, identity.Seed,
            identity.HistoricalUnattributedObservations, left.Length, referenceExact,
            repeatExact, lineage.Count(x => x.DirectGovernedDivergence),
            lineage.Count(x => x.ReconvergedBefore || x.ReconvergedAfter),
            lineage.Count(x => x.UnexplainedStateDivergence), episodes.Length, first,
            firstInspection, firstRelevantReject, op370, op466, episodes,
            control.SerializedHash, treatment.SerializedHash, control.RngCalls,
            control.RngHash, treatment.RngCalls, treatment.RngHash,
            SafetyRemediationGateHardeningResearch.DiagnosticsFingerprint(control.Diagnostics),
            SafetyRemediationGateHardeningResearch.DiagnosticsFingerprint(treatment.Diagnostics));
    }

    private static FirstDifference FindFirstDifference(
        IReadOnlyList<SafetyRemediationGatePairedOpportunity> opportunities)
    {
        foreach (var item in opportunities.OrderBy(x => x.OpportunityOrder))
        {
            var before = SafetyRemediationForensicAttributionResearch.Compare(
                item.ControlBefore, item.TreatmentBefore);
            if (before.Kind != ForensicStateDifferenceKind.None)
                return new(item.OpportunityKey, item.OpportunityOrder, "StateBefore", before.Kind,
                    before.DifferentFields, item.ControlBefore, item.TreatmentBefore);
            var after = SafetyRemediationForensicAttributionResearch.Compare(
                item.ControlAfter, item.TreatmentAfter);
            if (after.Kind != ForensicStateDifferenceKind.None)
                return new(item.OpportunityKey, item.OpportunityOrder, "StateAfter", after.Kind,
                    after.DifferentFields, item.ControlAfter, item.TreatmentAfter);
        }
        throw new InvalidDataException("Run declared unexplained observations but had no state difference.");
    }

    private static OpportunityInspection Inspect(SafetyRemediationGatePairedOpportunity item,
        Execution control, Execution treatment, IReadOnlyList<SafetyRemediationGateLineageStep> lineage)
    {
        var step = lineage.Single(x => x.OpportunityOrder == item.OpportunityOrder);
        var controlCandidates = control.Diagnostics!.CandidateDecisions
            .Where(x => x.OpportunityOrder == item.OpportunityOrder).Select(Candidate).ToImmutableArray();
        var treatmentCandidates = treatment.Diagnostics!.CandidateDecisions
            .Where(x => x.OpportunityOrder == item.OpportunityOrder).Select(Candidate).ToImmutableArray();
        var rejected = control.Diagnostics.CandidateDecisions.Where(x =>
            x.OpportunityOrder == item.OpportunityOrder &&
            SafetyRemediationGateHardeningResearch.CanonicalAuthorityRejectedSelectedCommit([x])).ToArray();
        return new(item.OpportunityKey, item.OpportunityOrder, item.ControlBefore, item.TreatmentBefore,
            item.ControlAfter, item.TreatmentAfter, item.ControlCommitIdentity,
            item.TreatmentCommitIdentity, step.BeforeEqual, step.AfterEqual, step.CommitDifference,
            item.CanonicalAuthorityChangedCommit, rejected.Length > 0,
            item.ControlCommitIdentity is not null &&
            SafetyRemediationGateHardeningResearch.CanonicalRejectWasNotCommitted(
                item.ControlCommitIdentity, item.TreatmentCommitIdentity),
            step.DirectGovernedDivergence, step.ActiveLineageBefore, step.OpenedLineage,
            step.ActiveLineageAfter, step.ReconvergedBefore, step.ReconvergedAfter,
            step.UnexplainedStateDivergence, controlCandidates, treatmentCandidates);
    }

    private static CandidateInspection Candidate(SafetyRemediationCandidateGeometryDecision x) => new(
        x.CandidateOrder, x.ObjectType.ToString(), x.StartTime, x.EndTime, x.LatentStartBeat,
        x.LatentEndBeat, x.CanonicalStartBeat, x.CanonicalEndBeat, x.LegacyLegalLanes,
        x.CanonicalLegalLanes, x.SelectedLane, x.RngPositionBeforeDecision,
        x.MaterializedGeometryStateHash, x.LegacyAcceptsSelectedLane, x.CanonicalAcceptsSelectedLane);

    private static Execution Execute(ManiaChart chart, AddNotesOptions options,
        MapperEvidenceProfile profile, int seed, G1GateRuntimeConfiguration? g1, bool? treatment)
    {
        var random = new TranscriptRandom(seed);
        var configuration = treatment.HasValue
            ? SafetyRemediationGateRuntimeConfiguration.Frozen(treatment.Value,
                compactDiagnostics: false, retainCandidateDecisions: true) : null;
        var result = new AddNotesEngine().Apply(chart, options, random, profile, null, null, g1, null,
            configuration);
        return new(Hash(OsuBeatmap.Write(result.ModifiedChart, options.Chance)), random.CallCount,
            random.Hash, result.SafetyRemediationGateDiagnostics);
    }

    private static bool Equivalent(Execution left, Execution right, bool compareDiagnostics) =>
        left.SerializedHash == right.SerializedHash && left.RngCalls == right.RngCalls
        && left.RngHash == right.RngHash && (!compareDiagnostics
            || SafetyRemediationGateHardeningResearch.DiagnosticsFingerprint(left.Diagnostics!)
            == SafetyRemediationGateHardeningResearch.DiagnosticsFingerprint(right.Diagnostics!));

    private static void WriteCsv(string path, IEnumerable<RunResult> rows)
    {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
        writer.WriteLine("stratum,chart_id,seed,opportunity_count,historical_unattributed_observations,unattributed_observations,episode_count,first_opportunity_order,first_opportunity_key,first_point,first_kind,direct_governed_divergences,reconvergence_count,reference_exact,treatment_repeat_exact");
        foreach (var x in rows) writer.WriteLine(string.Join(',', Csv(x.Stratum), x.ChartId, x.Seed,
            x.OpportunityCount, x.HistoricalUnattributedObservations,
            x.UnattributedObservations, x.EpisodeCount,
            x.FirstDifference.OpportunityOrder, Csv(x.FirstDifference.OpportunityKey),
            x.FirstDifference.Point, x.FirstDifference.Kind, x.DirectGovernedDivergences,
            x.ReconvergenceCount, x.ReferenceExact.ToString().ToLowerInvariant(),
            x.TreatmentRepeatExact.ToString().ToLowerInvariant()));
    }

    private static void WriteHashes(string directory)
    {
        var rows = Directory.EnumerateFiles(directory).Where(x =>
                Path.GetFileName(x) != "sha256sums.txt")
            .OrderBy(Path.GetFileName, StringComparer.Ordinal).Select(x =>
                $"{Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(x)))}  {Path.GetFileName(x)}");
        File.WriteAllLines(Path.Combine(directory, "sha256sums.txt"), rows, new UTF8Encoding(false));
    }

    private static void WriteJson<T>(string path, T value) => File.WriteAllText(path,
        JsonSerializer.Serialize(value, Json) + Environment.NewLine, new UTF8Encoding(false));
    private static string Hash(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string Csv(string value) => '"' + value.Replace("\"", "\"\"") + '"';

    private sealed record RunIdentity(string Stratum, string ChartId, int Seed,
        int HistoricalUnattributedObservations);
    private sealed record Execution(string SerializedHash, long RngCalls, string RngHash,
        SafetyRemediationGateRuntimeDiagnostics? Diagnostics);
    private sealed record FirstDifference(string OpportunityKey, int OpportunityOrder, string Point,
        ForensicStateDifferenceKind Kind, ImmutableArray<string> DifferentFields,
        SafetyRemediationGateSufficientState Control, SafetyRemediationGateSufficientState Treatment);
    private sealed record CandidateInspection(int CandidateOrder, string ObjectType, int StartTime,
        int? EndTime, decimal LatentStartBeat, decimal? LatentEndBeat, decimal CanonicalStartBeat,
        decimal? CanonicalEndBeat, ImmutableArray<int> LegacyLegalLanes,
        ImmutableArray<int> CanonicalLegalLanes, int? SelectedLane, long RngPositionBeforeDecision,
        string MaterializedGeometryStateHash, bool LegacyAcceptsSelectedLane,
        bool CanonicalAcceptsSelectedLane);
    private sealed record OpportunityInspection(string OpportunityKey, int OpportunityOrder,
        SafetyRemediationGateSufficientState ControlBefore,
        SafetyRemediationGateSufficientState TreatmentBefore,
        SafetyRemediationGateSufficientState ControlAfter,
        SafetyRemediationGateSufficientState TreatmentAfter,
        string? ControlCommitIdentity, string? TreatmentCommitIdentity, bool BeforeEqual,
        bool AfterEqual, bool CommitsDiffer, bool CanonicalAuthorityChangedCommit,
        bool CanonicalAuthorityRejectedSelectedCommit, bool CanonicalRejectWasNotCommitted,
        bool DirectGovernedDivergence, string? ActiveLineageBefore, string? OpenedLineage,
        string? ActiveLineageAfter, bool ReconvergedBefore, bool ReconvergedAfter,
        bool UnexplainedStateDivergence, ImmutableArray<CandidateInspection> ControlCandidates,
        ImmutableArray<CandidateInspection> TreatmentCandidates);
    private sealed record RunResult(string Stratum, string ChartId, int Seed,
        int HistoricalUnattributedObservations, int OpportunityCount, bool ReferenceExact,
        bool TreatmentRepeatExact, int DirectGovernedDivergences,
        int ReconvergenceCount, int UnattributedObservations, int EpisodeCount,
        FirstDifference FirstDifference, OpportunityInspection FirstDifferenceInspection,
        OpportunityInspection? FirstRelevantCanonicalReject,
        OpportunityInspection? Op370, OpportunityInspection? Op466,
        ImmutableArray<UnattributedDifferenceEpisode> Episodes,
        string ControlOutputSha256, string TreatmentOutputSha256, long ControlRngCalls,
        string ControlRngTranscriptSha256, long TreatmentRngCalls,
        string TreatmentRngTranscriptSha256, string ControlDiagnosticsSha256,
        string TreatmentDiagnosticsSha256);

    private sealed class TranscriptRandom(int seed) : IRandomPositionSource
    {
        private readonly SeededRandom inner = new(seed);
        private readonly SafetyRemediationGateTranscriptHash transcript = new();
        public long CallCount { get; private set; }
        public string Hash => transcript.CurrentHash;
        public double NextDouble()
        {
            var value = inner.NextDouble(); CallCount++; transcript.AppendDouble(value); return value;
        }
        public int Next(int maximum)
        {
            var value = inner.Next(maximum); CallCount++;
            transcript.AppendInteger(maximum, value); return value;
        }
    }
}
