using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.VisualBasic.FileIO;
using ManiaAddNotesLab.Core;

internal static class SafetyRemediationGateUnresolvedFollowupRunner
{
    private static readonly string[] ImplementationFiles =
    [
        "src/ManiaAddNotesLab.Core/AddNotesEngine.cs",
        "src/ManiaAddNotesLab.Core/BeatTimeline.cs",
        "src/ManiaAddNotesLab.Core/ChartAnalysis.cs",
        "src/ManiaAddNotesLab.Core/LaneGeometryIndex.cs",
        "src/ManiaAddNotesLab.Core/Model.cs",
        "src/ManiaAddNotesLab.Core/OsuBeatmap.cs",
        "src/ManiaAddNotesLab.Core/SafetyRemediationGateResearch.cs"
    ];
    private static readonly string[] HarnessFiles =
    [
        "src/ManiaAddNotesLab.Core/C11CorpusDiscovery.cs",
        "src/ManiaAddNotesLab.Core/SafetyRemediationGateHardeningResearch.cs",
        "tests/ManiaAddNotesLab.Tests/PhaseC11WitnessAgreementTests.cs",
        "tests/ManiaAddNotesLab.Tests/PhaseSafetyRemediationGateTests.cs",
        "tests/ManiaAddNotesLab.Tests/PhaseSafetyRemediationGateHardeningTests.cs",
        "tools/ManiaAddNotesLab.Experiments/FrozenC11CorpusRunner.cs",
        "tools/ManiaAddNotesLab.Experiments/Program.cs",
        "tools/ManiaAddNotesLab.Experiments/SafetyRemediationGateHardeningRunner.cs",
        "tools/ManiaAddNotesLab.Experiments/SafetyRemediationGateUnresolvedFollowupRunner.cs"
    ];
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string Prepare(string repositoryRoot, string manifestPath, string contractPath)
    {
        var manifest = FrozenC11Manifest.Load(manifestPath);
        var contract = new FollowupContract("safety-remediation-gate-followup-contract.1",
            "SAFETY.REMEDIATION.GATE focused classifier/C11/unresolved follow-up", GitHead(repositoryRoot),
            Snapshot(repositoryRoot, ImplementationFiles), Snapshot(repositoryRoot, HarnessFiles),
            manifest.CanonicalSha256, "0x400000000", 5, false,
            "Historical 188/22/5 and NEEDS_REVIEW remain immutable; follow-up mechanisms are additive.",
            "No defaults, remediation, G1, lane selection, AddChance, HardValidity or canonical authority changes.");
        var hash = Hash(JsonSerializer.Serialize(contract, new JsonSerializerOptions(Json)
            { WriteIndented = false }));
        Directory.CreateDirectory(Path.GetDirectoryName(contractPath)!);
        File.WriteAllText(contractPath, JsonSerializer.Serialize(new FollowupContractArtifact(contract, hash), Json)
            + Environment.NewLine, new UTF8Encoding(false));
        return hash;
    }

    public static string Run(string repositoryRoot, string corpusRoot, string casesPath,
        string manifestPath, string contractPath, string outputPath)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("DOTNET_GCHeapHardLimit"), "0x400000000",
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Focused follow-up requires DOTNET_GCHeapHardLimit=0x400000000.");
        var artifact = JsonSerializer.Deserialize<FollowupContractArtifact>(File.ReadAllText(contractPath), Json)
            ?? throw new InvalidDataException("Follow-up contract could not be decoded.");
        var canonicalContract = Hash(JsonSerializer.Serialize(artifact.Contract,
            new JsonSerializerOptions(Json) { WriteIndented = false }));
        if (canonicalContract != artifact.CanonicalSha256
            || artifact.Contract.RepositoryEntryHead != GitHead(repositoryRoot)
            || artifact.Contract.ImplementationSnapshotSha256 != Snapshot(repositoryRoot, ImplementationFiles)
            || artifact.Contract.HarnessSnapshotSha256 != Snapshot(repositoryRoot, HarnessFiles))
            throw new InvalidDataException("Follow-up contract or frozen code identities drifted.");
        var manifest = FrozenC11Manifest.Load(manifestPath);
        if (manifest.CanonicalSha256 != artifact.Contract.CorpusManifestSha256)
            throw new InvalidDataException("C11 manifest drifted from the follow-up contract.");
        var corpus = C11CorpusDiscovery.ResolveFrozen(corpusRoot, manifest.Charts);
        var cases = ReadCases(casesPath);
        if (cases.Length != 5) throw new InvalidDataException(
            $"Expected exactly five historical C_UNRESOLVED cases; observed {cases.Length}.");
        var rows = new List<UnresolvedFinding>();
        foreach (var frozen in cases.OrderBy(x => x.ChartId, StringComparer.Ordinal).ThenBy(x => x.Seed))
        {
            Console.WriteLine($"SAFETY.REMEDIATION.GATE focused unresolved chart={frozen.ChartId} "
                + $"seed={frozen.Seed} target={frozen.OpportunityKey}");
            var descriptor = corpus.Charts.Single(x => x.Sha256 == frozen.ChartId);
            var chart = OsuBeatmap.Parse(File.ReadAllText(descriptor.RuntimePath));
            rows.Add(Evaluate(chart, frozen));
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: false);
            GC.WaitForPendingFinalizers();
        }
        var explained = rows.Count(x => x.CausalMechanismDemonstrated);
        var resultArtifact = new
        {
            schemaVersion = "safety-remediation-gate-unresolved-followup.1",
            contractSha256 = artifact.CanonicalSha256,
            implementationSnapshotSha256 = artifact.Contract.ImplementationSnapshotSha256,
            harnessSnapshotSha256 = artifact.Contract.HarnessSnapshotSha256,
            repositoryEntryHead = artifact.Contract.RepositoryEntryHead,
            historicalState = new { direct = 188, causallyUnreachable = 22, unresolved = 5,
                outcome = "NEEDS_REVIEW", retrospectiveClassificationChanged = false },
            corpus = new { manifest = manifest.CanonicalSha256, uniqueCharts = corpus.Charts.Length,
                physicalLocations = corpus.OsuFileCount, exactDuplicateHashes = corpus.Duplicates.Length },
            execution = new { pairs = rows.Count, executionMemoryLimit = "0x400000000",
                fullMatrixExecuted = false, defaultsChanged = false, hardValidityChanged = false },
            result = new { causallyDemonstrated = explained, stillUnresolved = rows.Count - explained,
                proposedMechanisms = rows.Select(x => x.ProposedMechanism).Distinct().Order().ToArray() },
            cases = rows
        };
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(resultArtifact, Json) + Environment.NewLine,
            new UTF8Encoding(false));
        return explained == rows.Count ? "FIVE_MECHANISMS_DEMONSTRATED" : "PARTIAL_NEEDS_REVIEW";
    }

    private static UnresolvedFinding Evaluate(ManiaChart chart, FrozenCase frozen)
    {
        var options = InteriorRelationMembershipResearch.CurrentOptions() with
            { Chance = .50, ArticulationEnabled = false, Trace = false, DiagnosticsEnabled = false };
        var profile = MapperEvidenceProfileBuilder.Build(chart);
        var control = Execute(chart, options, profile, frozen.Seed, false);
        var controlReference = ExecuteReference(chart, options, profile, frozen.Seed);
        var controlReferenceExact = control.Serialized == controlReference.Serialized
            && control.RngCalls == controlReference.RngCalls && control.RngHash == controlReference.RngHash;
        var treatment = Execute(chart, options, profile, frozen.Seed, true);
        var treatmentRepeat = Execute(chart, options, profile, frozen.Seed, true);
        var treatmentDeterministic = treatment.Serialized == treatmentRepeat.Serialized
            && treatment.RngCalls == treatmentRepeat.RngCalls && treatment.RngHash == treatmentRepeat.RngHash
            && SafetyRemediationGateHardeningResearch.DiagnosticsFingerprint(treatment.Result
                .SafetyRemediationGateDiagnostics!) == SafetyRemediationGateHardeningResearch
                .DiagnosticsFingerprint(treatmentRepeat.Result.SafetyRemediationGateDiagnostics!);

        var target = control.Result.ModifiedChart.AddedObjects.SingleOrDefault(x =>
            x.Lane == frozen.Lane && x.StartTime == frozen.StartTime && x.EndTime == frozen.EndTime);
        if (target is null) throw new InvalidDataException("Historical target was not committed by control.");
        var treatmentTarget = treatment.Result.ModifiedChart.AddedObjects.SingleOrDefault(x =>
            SameIdentity(x, target));
        if (treatmentTarget is null) throw new InvalidDataException(
            "Historical C_UNRESOLVED target was not identically committed by treatment.");

        var timeline = new BeatTimeline(chart.TimingPoints);
        var fingerprint = MapperEvidenceProfileBuilder.ComputeFingerprint(chart);
        var controlReparsed = OsuBeatmap.Parse(control.Serialized);
        var treatmentReparsed = OsuBeatmap.Parse(treatment.Serialized);
        var controlObjects = SafetyObjects(controlReparsed, fingerprint,
            GeometrySnapshotRole.Control, timeline);
        var treatmentObjects = SafetyObjects(treatmentReparsed, fingerprint,
            GeometrySnapshotRole.Treatment, timeline);
        var controlAudit = GeometrySafetyAttributionResearch.AuditSnapshot(fingerprint, chart.KeyCount,
            controlObjects);
        var treatmentAudit = GeometrySafetyAttributionResearch.AuditSnapshot(fingerprint, chart.KeyCount,
            treatmentObjects);
        var parsedTarget = controlReparsed.OriginalObjects.Single(x => x.Lane == frozen.Lane
            && x.StartTime == frozen.StartTime && x.EndTime == frozen.EndTime);
        var controlTargetObject = controlObjects.Single(x => SameMaterialized(x.Object, parsedTarget));
        var targetRelations = controlAudit.RawRelations.Where(x => x.LeftObjectId == controlTargetObject.Id
            || x.RightObjectId == controlTargetObject.Id).Select(x => x.GeometrySignature)
            .ToHashSet(StringComparer.Ordinal);
        var violations = controlAudit.HardViolations.Where(x =>
            x.Violation == GeometryHardViolationKind.TapOnHeldLongNote
            && targetRelations.Contains(x.GeometrySignature)).ToArray();
        if (violations.Length == 0) throw new InvalidDataException(
            "Independent HardValidity audit did not reproduce the historical target violation.");
        var controlById = controlObjects.ToDictionary(x => x.Id, StringComparer.Ordinal);
        var blockers = violations.Select(finding =>
        {
            var relation = controlAudit.RawRelations.Single(x => x.GeometrySignature == finding.GeometrySignature);
            var blockerId = relation.LeftObjectId == controlTargetObject.Id
                ? relation.RightObjectId : relation.LeftObjectId;
            var parsedBlocker = controlById[blockerId].Object;
            return control.Result.ModifiedChart.AllObjects.Single(x => SameMaterialized(x, parsedBlocker));
        }).Distinct().ToArray();

        var pairs = control.States.Zip(treatment.States, (left, right) =>
            new SafetyRemediationGatePairedOpportunity(left.OpportunityKey, left.OpportunityOrder,
                SafetyRemediationGateHardeningResearch.LineageState(left.StateBeforeHash,
                    left.StateBeforeComplete, left.OpportunityOrder),
                SafetyRemediationGateHardeningResearch.LineageState(right.StateBeforeHash,
                    right.StateBeforeComplete, right.OpportunityOrder),
                SafetyRemediationGateHardeningResearch.LineageState(left.StateAfterHash,
                    left.StateAfterComplete, left.OpportunityOrder + 1),
                SafetyRemediationGateHardeningResearch.LineageState(right.StateAfterHash,
                    right.StateAfterComplete, right.OpportunityOrder + 1),
                left.CommittedObjectIdentity, right.CommittedObjectIdentity,
                control.Decisions.Where(x => x.OpportunityOrder == left.OpportunityOrder)
                    .Any(x => SafetyRemediationGateHardeningResearch
                        .CanonicalAuthorityRejectedSelectedCommit([x])))).ToArray();
        var lineage = SafetyRemediationGateHardeningResearch.Classify(
            $"{frozen.ChartId}|{frozen.Seed}|primary", pairs);
        var targetState = control.States.Single(x => x.OpportunityKey == frozen.OpportunityKey);
        var targetStep = lineage.Single(x => x.OpportunityOrder == targetState.OpportunityOrder);
        var parsedTreatmentTarget = treatmentReparsed.OriginalObjects.Single(x =>
            x.Lane == frozen.Lane && x.StartTime == frozen.StartTime && x.EndTime == frozen.EndTime);
        var treatmentTargetObject = treatmentObjects.Single(x => SameMaterialized(x.Object,
            parsedTreatmentTarget));
        var treatmentRelations = treatmentAudit.RawRelations.Where(x =>
            x.LeftObjectId == treatmentTargetObject.Id || x.RightObjectId == treatmentTargetObject.Id)
            .Select(x => x.GeometrySignature).ToHashSet(StringComparer.Ordinal);
        var treatmentViolation = treatmentAudit.HardViolations.Any(x =>
            x.Violation == GeometryHardViolationKind.TapOnHeldLongNote
            && treatmentRelations.Contains(x.GeometrySignature));

        var blockerRows = blockers.Select(blocker => Blocker(blocker, treatment.Result.ModifiedChart,
            control, treatment, lineage, targetStep, timeline)).ToArray();
        var demonstrated = !treatmentViolation && blockerRows.Length == violations.Length
            && blockerRows.All(x => x.ExactObjectAbsentOrChanged && x.GovernedLineageEstablished
                && x.NoReconvergenceBeforeTarget);
        var proposed = demonstrated
            ? blockerRows.All(x => x.TreatmentExactObject is null)
                ? "D_CONFLICTING_OBJECT_ABSENT"
                : "D_CONFLICTING_OBJECT_ABSENT_OR_CHANGED"
            : "C_UNRESOLVED";
        return new(frozen.ChartId, frozen.Seed, frozen.OpportunityKey, Identity(target),
            controlReferenceExact, treatmentDeterministic, violations.Length,
            !treatmentViolation, targetStep.ActiveLineageBefore is not null,
            blockerRows, proposed, demonstrated,
            demonstrated
                ? "The historical tap commits identically, while every independently audited control blocker is absent or changed in treatment under an unreconverged canonical-governed lineage."
                : "The focused evidence is insufficient for a new causal classification; preserve C_UNRESOLVED.");
    }

    private static BlockerFinding Blocker(ManiaObject blocker, ManiaChart treatmentChart,
        ForensicExecution control, ForensicExecution treatment,
        ImmutableArray<SafetyRemediationGateLineageStep> lineage,
        SafetyRemediationGateLineageStep targetStep, BeatTimeline timeline)
    {
        var prefix = CommitPrefix(blocker);
        var controlState = control.States.SingleOrDefault(x =>
            x.CommittedObjectIdentity?.StartsWith(prefix, StringComparison.Ordinal) == true);
        var exactTreatment = treatmentChart.AllObjects.SingleOrDefault(x => SameIdentity(x, blocker));
        var alternatives = treatmentChart.AllObjects.Where(x => x.Sequence == blocker.Sequence
            && x.Origin == blocker.Origin && x.Type == blocker.Type && !SameIdentity(x, blocker))
            .Select(Identity).Order(StringComparer.Ordinal).ToArray();
        SafetyRemediationGateCompactOpportunityState? treatmentState = null;
        SafetyRemediationGateLineageStep? blockerStep = null;
        if (controlState is not null)
        {
            treatmentState = treatment.States[controlState.OpportunityOrder];
            blockerStep = lineage[controlState.OpportunityOrder];
        }
        var lineageId = blockerStep?.OpenedLineage ?? blockerStep?.ActiveLineageBefore
            ?? targetStep.ActiveLineageBefore;
        var origin = lineageId is null ? null : lineage.SingleOrDefault(x => x.OpenedLineage == lineageId);
        var noReconvergence = origin is not null && targetStep.OpportunityOrder >= origin.OpportunityOrder
            && !lineage.Where(x => x.OpportunityOrder >= origin.OpportunityOrder
                    && x.OpportunityOrder <= targetStep.OpportunityOrder)
                .Any(x => x.ReconvergedBefore || x.ReconvergedAfter)
            && targetStep.ActiveLineageBefore == lineageId;
        var treatmentCommit = treatmentState?.CommittedObjectIdentity;
        return new(Identity(blocker), Signature(blocker, timeline), blocker.Origin.ToString(),
            controlState?.OpportunityKey, controlState?.OpportunityOrder,
            treatmentCommit, exactTreatment is null ? null : Identity(exactTreatment), alternatives,
            exactTreatment is null || alternatives.Length > 0,
            lineageId, origin?.OpportunityKey, origin?.DirectGovernedDivergence == true,
            noReconvergence);
    }

    private static ForensicExecution Execute(ManiaChart chart, AddNotesOptions options,
        MapperEvidenceProfile profile, int seed, bool treatment)
    {
        var sink = new CaptureSink();
        var random = new TranscriptRandom(seed);
        var result = new AddNotesEngine().Apply(chart, options, random, profile, null, null, null, null,
            SafetyRemediationGateRuntimeConfiguration.Frozen(treatment, compactDiagnostics: true,
                retainCandidateDecisions: false, opportunitySink: sink));
        return new(result, OsuBeatmap.Write(result.ModifiedChart, options.Chance), random.CallCount,
            random.Hash, sink.States.ToImmutableArray(), sink.Decisions.ToImmutableArray());
    }

    private static ReferenceExecution ExecuteReference(ManiaChart chart, AddNotesOptions options,
        MapperEvidenceProfile profile, int seed)
    {
        var random = new TranscriptRandom(seed);
        var result = new AddNotesEngine().Apply(chart, options, random, profile);
        return new(OsuBeatmap.Write(result.ModifiedChart, options.Chance), random.CallCount, random.Hash);
    }

    private static GeometrySafetyObject[] SafetyObjects(ManiaChart chart, string fingerprint,
        GeometrySnapshotRole role, BeatTimeline timeline) => chart.AllObjects.Select((x, i) =>
        GeometrySafetyAttributionResearch.Object(fingerprint, role, x, timeline.ToBeatDecimal(x.StartTime),
            x.Type == ManiaObjectType.Tap ? timeline.ToBeatDecimal(x.StartTime)
                : timeline.ToBeatDecimal(x.EndTime!.Value), i)).ToArray();

    private static string Signature(ManiaObject value, BeatTimeline timeline)
    {
        var start = timeline.ToBeatDecimal(value.StartTime).ToString(CultureInfo.InvariantCulture);
        var end = (value.Type == ManiaObjectType.Tap ? timeline.ToBeatDecimal(value.StartTime)
            : timeline.ToBeatDecimal(value.EndTime!.Value)).ToString(CultureInfo.InvariantCulture);
        return $"L{value.Lane}:{value.Type}:{start}:{end}:{value.StartTime}:"
            + (value.EndTime?.ToString(CultureInfo.InvariantCulture) ?? "TAP");
    }

    private static string Identity(ManiaObject x) =>
        $"{x.Lane}|{x.StartTime}|{x.EndTime?.ToString(CultureInfo.InvariantCulture) ?? "TAP"}|"
        + $"{x.Type}|{x.Sequence}|{x.Origin}";
    private static string CommitPrefix(ManiaObject x) =>
        $"{x.Lane}|{x.StartTime}|{x.EndTime?.ToString(CultureInfo.InvariantCulture) ?? ""}|"
        + $"{x.Type}|{x.Sequence}|{x.Origin}|";
    private static bool SameIdentity(ManiaObject a, ManiaObject b) => a.Lane == b.Lane
        && a.StartTime == b.StartTime && a.EndTime == b.EndTime && a.Type == b.Type
        && a.Sequence == b.Sequence && a.Origin == b.Origin;
    private static bool SameMaterialized(ManiaObject a, ManiaObject b) => a.Lane == b.Lane
        && a.StartTime == b.StartTime && a.EndTime == b.EndTime && a.Type == b.Type;

    private static string Snapshot(string root, IEnumerable<string> paths) =>
        ExperimentBaselineIdentityResearch.ComputeImplementationSnapshot(paths.Select(path =>
            new NamedImplementationContent(path, File.ReadAllBytes(Path.Combine(root,
                path.Replace('/', Path.DirectorySeparatorChar))))));
    private static string GitHead(string root)
    {
        var head = File.ReadAllText(Path.Combine(root, ".git", "HEAD")).Trim();
        if (!head.StartsWith("ref: ", StringComparison.Ordinal)) return head;
        return File.ReadAllText(Path.Combine(root, ".git", head[5..].Replace('/',
            Path.DirectorySeparatorChar))).Trim();
    }
    private static string Hash(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static ImmutableArray<FrozenCase> ReadCases(string path)
    {
        using var parser = new TextFieldParser(path) { TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true };
        parser.SetDelimiters(",");
        var header = parser.ReadFields() ?? throw new InvalidDataException("Missing case CSV header.");
        var index = header.Select((x, i) => (x, i)).ToDictionary(x => x.x, x => x.i);
        var rows = ImmutableArray.CreateBuilder<FrozenCase>();
        while (!parser.EndOfData)
        {
            var row = parser.ReadFields()!;
            if (row[index["hardened_class"]] != "C_UNRESOLVED") continue;
            rows.Add(new(row[index["chart_id"]], int.Parse(row[index["seed"]],
                    CultureInfo.InvariantCulture), row[index["opportunity_key"]],
                int.Parse(row[index["lane"]], CultureInfo.InvariantCulture),
                int.Parse(row[index["start_time"]], CultureInfo.InvariantCulture),
                string.IsNullOrEmpty(row[index["end_time"]]) ? null
                    : int.Parse(row[index["end_time"]], CultureInfo.InvariantCulture)));
        }
        return rows.ToImmutable();
    }

    private sealed class CaptureSink : ISafetyRemediationGateOpportunitySink
    {
        public List<SafetyRemediationCandidateGeometryDecision> Decisions { get; } = [];
        public List<SafetyRemediationGateCompactOpportunityState> States { get; } = [];
        public void ObserveCandidate(SafetyRemediationCandidateGeometryDecision decision) => Decisions.Add(decision);
        public void Observe(SafetyRemediationGateCompactOpportunityState state) => States.Add(state);
    }

    private sealed class TranscriptRandom(int seed) : IRandomPositionSource
    {
        private readonly SeededRandom inner = new(seed);
        private readonly SafetyRemediationGateTranscriptHash transcript = new();
        public long CallCount { get; private set; }
        public string Hash => transcript.CurrentHash;
        public double NextDouble() { var value = inner.NextDouble(); CallCount++;
            transcript.AppendDouble(value); return value; }
        public int Next(int maximum) { var value = inner.Next(maximum); CallCount++;
            transcript.AppendInteger(maximum, value); return value; }
    }

    private sealed record FrozenCase(string ChartId, int Seed, string OpportunityKey, int Lane,
        int StartTime, int? EndTime);
    private sealed record ForensicExecution(AddNotesResult Result, string Serialized, long RngCalls,
        string RngHash, ImmutableArray<SafetyRemediationGateCompactOpportunityState> States,
        ImmutableArray<SafetyRemediationCandidateGeometryDecision> Decisions);
    private sealed record ReferenceExecution(string Serialized, long RngCalls, string RngHash);
    private sealed record BlockerFinding(string ControlObject, string GeometrySignature, string Provenance,
        string? ControlCommitOpportunity, int? ControlCommitOrder, string? TreatmentCommitAtSameOpportunity,
        string? TreatmentExactObject, string[] TreatmentSameProvenanceAlternatives,
        bool ExactObjectAbsentOrChanged, string? LineageId, string? GovernedOriginOpportunity,
        bool GovernedLineageEstablished, bool NoReconvergenceBeforeTarget);
    private sealed record UnresolvedFinding(string ChartId, int Seed, string OpportunityKey,
        string HistoricalTarget, bool ControlReferenceExact, bool TreatmentDeterministic,
        int IndependentControlViolations, bool IndependentTreatmentViolationAbsent,
        bool TargetHasActivePriorLineage, BlockerFinding[] ConflictingObjects,
        string ProposedMechanism, bool CausalMechanismDemonstrated, string Conclusion);
    private sealed record FollowupContract(string SchemaVersion, string Scope,
        string RepositoryEntryHead, string ImplementationSnapshotSha256, string HarnessSnapshotSha256,
        string CorpusManifestSha256, string ExecutionMemoryLimit, int FocusedPairCount,
        bool FullMatrixAuthorized, string HistoricalClassificationRule, string ProhibitedChanges);
    private sealed record FollowupContractArtifact(FollowupContract Contract, string CanonicalSha256);
}
