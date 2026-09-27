using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManiaAddNotesLab.Core;

public sealed record GeometryObservationObject(int SnapshotOrder, string Source,
    string? CommitOpportunityKey, int? CommitOpportunityOrder, int Lane, int StartTime, int? EndTime,
    ManiaObjectType Type, int Sequence, AddedObjectOrigin Origin, decimal LatentStartBeat,
    decimal LatentEndBeat, decimal CanonicalStartBeat, decimal CanonicalEndBeat);

public sealed record PassiveTapQuery(int Lane, decimal Beat, bool IsLegal,
    ImmutableArray<int> AllLegalLanes, TapLaneGeometryInspection Inspection,
    string GeometryHashBefore, string GeometryHashAfter, long RngBefore, long RngAfter,
    int CommitsBefore, int CommitsAfter)
{
    public bool NonMutating => GeometryHashBefore == GeometryHashAfter && RngBefore == RngAfter
        && CommitsBefore == CommitsAfter;
}

public static class SafetyOp466GeometryObservationResearch
{
    public static void ValidateCompleteSnapshot(IReadOnlyList<GeometryObservationObject> objects,
        int expectedOriginals, int expectedCommits, string expectedMaterializedHash,
        string actualMaterializedGenerationHash, string expectedLatentHash, string actualLatentHash)
    {
        if (objects.Count(x => x.Source == "original") != expectedOriginals
            || objects.Count(x => x.Source == "synthetic-commit") != expectedCommits)
            throw new InvalidDataException("Geometry snapshot object population is incomplete.");
        if (objects.Any(x => x.Type == ManiaObjectType.LongNote
                && (x.EndTime is null || x.LatentEndBeat <= x.LatentStartBeat
                    || x.CanonicalEndBeat <= x.CanonicalStartBeat)))
            throw new InvalidDataException("LN EndTime/EndBeat or endpoint semantics are incomplete.");
        if (actualMaterializedGenerationHash != expectedMaterializedHash)
            throw new InvalidDataException($"Materialized generation-state fingerprint differs: expected "
                + $"{expectedMaterializedHash}, actual {actualMaterializedGenerationHash}.");
        if (actualLatentHash != expectedLatentHash)
            throw new InvalidDataException($"Latent committed geometry fingerprint differs: expected "
                + $"{expectedLatentHash}, actual {actualLatentHash}.");
    }

    public static PassiveTapQuery Query(LaneGeometryIndex geometry, int lane, decimal beat,
        IRandomPositionSource rng, int commitCount)
    {
        var geometryBefore = geometry.MaterializedStateHash();
        var rngBefore = rng.CallCount;
        var inspection = geometry.InspectTapLane(lane, beat);
        var lanes = geometry.FindLegalTapLanes(beat, new AddNotesStatistics()).ToImmutableArray();
        if (inspection.IsLegal != lanes.Contains(lane))
            throw new InvalidDataException("InspectTapLane and product-equivalent FindLegalTapLanes disagree.");
        return new(lane, beat, lanes.Contains(lane), lanes, inspection, geometryBefore,
            geometry.MaterializedStateHash(), rngBefore, rng.CallCount, commitCount, commitCount);
    }
}

internal static class SafetyOp466GeometryObservationRunner
{
    private const string MemoryLimit = "0x400000000";
    private const string ChartId = "20651C9B11DBB0D7BA2D64A8F167577AE0452762EEA83C5A7558BA89B8D2C788";
    private const string Op185 = "OP-00000185-BaseHead-S185-T16399-ANA";
    private const string Op466 = "OP-00000466-BaseHead-S466-T35009-ANA";
    private const int Seed = 4;
    private const int TargetOrder = 468;
    private const int TargetLane = 2;
    private const int TargetTime = 35009;
    private const string ExpectedImplementation = "B7AA67D389AE71A775397997BCEC3185CD0E763ED19E12770CE03D1C18AF2835";
    private const string ExpectedCorpus = "AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445";
    private const string AOutput = "462307AE599D390CCC56595CCBE9ABAB02D36EF35AC4E00A69D270963438E999";
    private const string BOutput = "5902D77132A36F1DBF8286FA41731FD6F62403BA4CEA8A2EFC3B5C2F4986671B";
    private const string SharedRng = "8F5589B0045254ECCAE68DFD3E01037D8C5771CA6BD7081132B173C2B6762DED";
    private const string ADiagnostics = "26804F0A5753C75738C7CC14286CDA090C847292242649CFBBD3CCE534D74875";
    private const string BDiagnostics = "A82C7CD93DC6B38ACCF211AD7AEB198A76492ABCE285F2AE188965DD463E0478";
    private static readonly SingleRngInterventionPlan BPlan = new(260, 4, 2, 3);
    private static readonly string[] ImplementationFiles =
    [
        "src/ManiaAddNotesLab.Core/AddNotesEngine.cs", "src/ManiaAddNotesLab.Core/BeatTimeline.cs",
        "src/ManiaAddNotesLab.Core/ChartAnalysis.cs", "src/ManiaAddNotesLab.Core/LaneGeometryIndex.cs",
        "src/ManiaAddNotesLab.Core/Model.cs", "src/ManiaAddNotesLab.Core/OsuBeatmap.cs",
        "src/ManiaAddNotesLab.Core/SafetyRemediationGateResearch.cs"
    ];
    private static readonly string[] HarnessFiles =
    [
        "tools/ManiaAddNotesLab.Experiments/Program.cs",
        "tools/ManiaAddNotesLab.Experiments/SafetyOp466GeometryObservationRunner.cs",
        "tests/ManiaAddNotesLab.Tests/SafetyOp466GeometryObservationTests.cs",
        "tests/ManiaAddNotesLab.Tests/ManiaAddNotesLab.Tests.csproj"
    ];
    private static readonly string[] HistoricalFiles =
    [
        "docs/safety_op185_op466_counterfactual_contract.json",
        ".artifacts/safety_op185_op466_counterfactual/summary.json",
        ".artifacts/safety_op185_op466_counterfactual/op466_result.json",
        ".artifacts/safety_op185_op466_counterfactual/rng_transcripts.json",
        ".artifacts/safety_op185_op466_counterfactual/sha256sums.txt",
        ".artifacts/safety_op466_candidate_admission/admission_decisions.json",
        ".artifacts/safety_op466_candidate_admission/no_interference.json",
        ".artifacts/safety_op466_candidate_admission/sha256sums.txt",
        ".artifacts/safety_op466_jules_handoff/manifest.json",
        ".artifacts/safety_op466_jules_handoff/sha256sums.txt",
        ".artifacts/safety_op466_jules_handoff.zip"
    ];
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    private static readonly JsonSerializerOptions Canonical = new(Json) { WriteIndented = false };

    public static string Prepare(string root, string manifestPath, string contractPath, string artifacts)
    {
        var implementation = Fingerprint(root, ImplementationFiles);
        if (implementation != ExpectedImplementation) throw new InvalidDataException("Behavioral identity drifted.");
        var corpus = FrozenC11ManifestResearch.Load(manifestPath);
        if (corpus.CanonicalSha256 != ExpectedCorpus) throw new InvalidDataException("C11 identity drifted.");
        var historical = HistoricalFiles.Select(path => new HistoricalInput(path,
            FileHash(Path.Combine(root, Native(path))))).ToImmutableArray();
        var contract = new Contract("safety-op466-geometry-observation-contract.3", GitHead(root),
            implementation, Fingerprint(root, HarnessFiles), corpus.CanonicalSha256, MemoryLimit,
            ChartId, Seed, Op185, Op466, TargetOrder, TargetLane, TargetTime,
            "Canonical A normal; canonical B authentic Next(4)=2 delivered once as index 3 at RNG position 260; chance .50; articulation OFF; G1 absent.",
            "All original TimedManiaObjects plus every observer-captured committed TimedManiaObject before OP-466, retaining durable times, latent beats, provenance and commit order.",
            "Snapshot is valid only if materialized and latent accumulator hashes equal the historical parent, commit identities pair exactly, output/RNG/diagnostics are frozen-exact, and OP-466 parent is order 468 at RNG 659.",
            "Build product-equivalent canonical LaneGeometryIndex, then compare InspectTapLane(2, canonicalBeat) with FindLegalTapLanes(canonicalBeat); no insert or candidate construction.",
            "Observer output/RNG/diagnostics/sequence/commits/states exact; observer RNG calls zero; query preserves geometry hash, RNG count, commits and objects.",
            ["LANE_2_LEGAL", "LANE_2_ILLEGAL", "GEOMETRY_INCOMPLETE", "INVALID", "BLOCKED"],
            ["identity drift", "historical mismatch", "snapshot hash mismatch", "observer interference",
             "opportunity mismatch", "unexpected intervention", "query mutation"], historical);
        var hash = Hash(JsonSerializer.Serialize(contract, Canonical));
        var artifact = new ContractArtifact(contract, hash,
            "UTF-8 System.Text.Json camelCase; ordered arrays; no timestamps or private paths");
        Directory.CreateDirectory(Path.GetDirectoryName(contractPath)!);
        Directory.CreateDirectory(artifacts);
        WriteJson(contractPath, artifact); WriteJson(Path.Combine(artifacts, "frozen_contract.json"), artifact);
        return hash;
    }

    public static string Run(string root, string corpusRoot, string manifestPath, string contractPath, string output)
    {
        RequireMemory();
        var artifact = JsonSerializer.Deserialize<ContractArtifact>(File.ReadAllText(contractPath), Json)
            ?? throw new InvalidDataException("Observation contract cannot be decoded.");
        var contract = artifact.Contract;
        if (Hash(JsonSerializer.Serialize(contract, Canonical)) != artifact.CanonicalSha256
            || contract.RepositoryEntryHead != GitHead(root)
            || contract.ImplementationSnapshotSha256 != Fingerprint(root, ImplementationFiles)
            || contract.HarnessSnapshotSha256 != Fingerprint(root, HarnessFiles))
            throw new InvalidDataException("Frozen contract, implementation or harness drifted.");
        foreach (var item in contract.HistoricalInputs)
            if (FileHash(Path.Combine(root, Native(item.Path))) != item.Sha256)
                throw new InvalidDataException($"Historical input drifted: {item.Path}");
        var manifest = FrozenC11ManifestResearch.Load(manifestPath);
        if (manifest.CanonicalSha256 != contract.CorpusManifestSha256) throw new InvalidDataException("Corpus drifted.");
        var corpus = C11CorpusDiscovery.ResolveFrozen(corpusRoot, manifest.Charts);
        var descriptor = corpus.Charts.Single(x => x.Sha256 == ChartId);
        var chartText = File.ReadAllText(descriptor.RuntimePath);
        var a = Execute(chartText, null);
        var b = Execute(chartText, BPlan);
        ValidateReference(a, false); ValidateReference(b, true);
        var snapA = BuildSnapshot(a, "A"); var snapB = BuildSnapshot(b, "B");
        Directory.CreateDirectory(output);
        WriteJson(Path.Combine(output, "source_inventory.json"), new
        {
            schemaVersion = "safety-op466-geometry-source-inventory.1", contract = artifact.CanonicalSha256,
            repositoryHead = contract.RepositoryEntryHead, contract.ImplementationSnapshotSha256,
            contract.HarnessSnapshotSha256, contract.CorpusManifestSha256, chart = ChartId, seed = Seed,
            contract.HistoricalInputs
        });
        WriteJson(Path.Combine(output, "preflight_controls.json"), new
        {
            schemaVersion = "safety-op466-geometry-preflight.1", fixturesValidatedByTests = true,
            independentChartInstances = !ReferenceEquals(a.Chart, b.Chart),
            independentProfiles = !ReferenceEquals(a.Profile, b.Profile),
            independentObservers = !ReferenceEquals(a.Observer, b.Observer),
            aObserverRngCalls = a.Observer.RngCalls, bObserverRngCalls = b.Observer.RngCalls,
            correctOpportunity = snapA.Target.OpportunityKey == Op466 && snapB.Target.OpportunityKey == Op466,
            exactPreRollState = snapA.Target.StateBefore.RootRngPosition == 659
                && snapB.Target.StateBefore.RootRngPosition == 659
        });
        WriteJson(Path.Combine(output, "arm_identities.json"), new { armA = Identity(a), armB = Identity(b) });
        WriteJson(Path.Combine(output, "no_interference.json"), new
        {
            armA = NoInterference(a, false), armB = NoInterference(b, true),
            sharedAuthenticRoll = a.Random.AuthenticCalls[659],
            effectiveChance = snapA.EffectiveChance,
            probabilityAbstain = a.Random.AuthenticCalls[659].DoubleValue >= snapA.EffectiveChance
                && b.Random.AuthenticCalls[659].DoubleValue >= snapB.EffectiveChance
        });
        WriteJson(Path.Combine(output, "snapshot_A.json"), snapA);
        WriteJson(Path.Combine(output, "snapshot_B.json"), snapB);

        // Official lane-2 queries occur only here, after the frozen contract and snapshot validation.
        var queryA = SafetyOp466GeometryObservationResearch.Query(snapA.Geometry, TargetLane,
            snapA.CanonicalBeat, a.Random, snapA.Commits.Length);
        var queryB = SafetyOp466GeometryObservationResearch.Query(snapB.Geometry, TargetLane,
            snapB.CanonicalBeat, b.Random, snapB.Commits.Length);
        if (!queryA.NonMutating || !queryB.NonMutating) throw new InvalidDataException("Passive query mutated state.");
        WriteJson(Path.Combine(output, "lane2_query_A.json"), queryA);
        WriteJson(Path.Combine(output, "lane2_query_B.json"), queryB);
        var outcomeA = queryA.IsLegal ? "LANE_2_LEGAL" : "LANE_2_ILLEGAL";
        var outcomeB = queryB.IsLegal ? "LANE_2_LEGAL" : "LANE_2_ILLEGAL";
        WriteJson(Path.Combine(output, "scientific_summary.json"), new
        {
            schemaVersion = "safety-op466-geometry-observation-summary.1", contract = artifact.CanonicalSha256,
            armA = new { outcome = outcomeA, queryA.Inspection },
            armB = new { outcome = outcomeB, queryB.Inspection },
            interpretation = "Hypothetical canonical geometry only. Historical A/B still abstained probabilistically before candidate construction; this does not show the target would have been selected or committed.",
            historicalOp466 = "C_UNRESOLVED", globalStatus = "NEEDS_REVIEW", promotion = "NOT_AUTHORIZED",
            behaviorChanged = false, fullMatrixExecuted = false
        });
        WriteHashes(output);
        return $"A={outcomeA};B={outcomeB}";
    }

    private static Execution Execute(string text, SingleRngInterventionPlan? plan)
    {
        var chart = OsuBeatmap.Parse(text); var profile = MapperEvidenceProfileBuilder.Build(chart);
        var options = InteriorRelationMembershipResearch.CurrentOptions() with
            { Chance = .50, ArticulationEnabled = false, Trace = false, DiagnosticsEnabled = false };
        var random = new SingleRngInterventionRandom(Seed, plan);
        var observer = new SafetyRemediationPlacementObserverResearch();
        var gate = SafetyRemediationGateRuntimeConfiguration.Frozen(true,
            compactDiagnostics: false, retainCandidateDecisions: true);
        var result = new AddNotesEngine().Apply(chart, options, random, profile, null, null, null, observer, gate);
        var diagnostics = result.SafetyRemediationGateDiagnostics!;
        return new(chart, profile, options, random, observer, result,
            Hash(OsuBeatmap.Write(result.ModifiedChart, options.Chance)),
            SafetyRemediationGateHardeningResearch.DiagnosticsFingerprint(diagnostics));
    }

    private static Snapshot BuildSnapshot(Execution execution, string arm)
    {
        var diagnostics = execution.Result.SafetyRemediationGateDiagnostics!;
        var target = diagnostics.OpportunityStates.Single(x => x.OpportunityKey == Op466);
        var proposals = execution.Observer.Build(); var proposalIndex = 0; var commits = new List<Commit>();
        foreach (var state in diagnostics.OpportunityStates)
        {
            if (state.CommittedObjectIdentity is null) continue;
            var proposal = proposals[proposalIndex++];
            if (LatentIdentity(proposal) != state.CommittedObjectIdentity)
                throw new InvalidDataException("Observer proposal does not pair with diagnostics commit identity.");
            if (state.OpportunityOrder < TargetOrder)
                commits.Add(new(state.OpportunityKey, state.OpportunityOrder, proposal));
        }
        if (proposalIndex != proposals.Length) throw new InvalidDataException("Observer/commit count mismatch.");
        var timeline = new BeatTimeline(execution.Chart.TimingPoints);
        var analysis = new OriginalChartAnalysis(execution.Chart, timeline);
        var playable = new CanonicalPlayableGeometry(timeline);
        var geometry = new LaneGeometryIndex(execution.Chart.KeyCount,
            analysis.Objects.Select(playable.Project).ToArray());
        foreach (var commit in commits) geometry.Insert(playable.Project(commit.Value));
        var objects = analysis.Objects.Select((x, i) => SnapshotObject(i, "original", null, null, x, playable))
            .Concat(commits.Select((x, i) => SnapshotObject(analysis.Objects.Count + i,
                "synthetic-commit", x.OpportunityKey, x.OpportunityOrder, x.Value, playable))).ToImmutableArray();
        var latent = AccumulatorHash(analysis.Objects.Select(LatentIdentity)
            .Concat(commits.Select(x => LatentIdentity(x.Value))));
        var opportunitySequenceHash = Hash(string.Join('\n',
            diagnostics.OpportunityStates.OrderBy(x => x.OpportunityOrder).Select(x => x.OpportunityKey)));
        var geometryHash = geometry.MaterializedStateHash();
        var materializedGenerationHash = Hash(string.Join('|',
            geometryHash, target.StateBefore.RootRngPosition, target.StateBefore.StageRngPosition,
            target.StateBefore.OpportunityCursor, target.StateBefore.PendingArticulationHash,
            opportunitySequenceHash));
        SafetyOp466GeometryObservationResearch.ValidateCompleteSnapshot(objects, analysis.Objects.Count,
            commits.Count, target.StateBefore.MaterializedGenerationStateHash, materializedGenerationHash,
            target.StateBefore.LatentCommittedGeometryHash, latent);
        var source = analysis.Objects.Single(x => x.Object.Sequence == 466 && x.Object.StartTime == TargetTime);
        var density = new AddNotesEngine().AnalyzeDensity(execution.Chart, execution.Options, TargetTime);
        var originalGeometry = new LaneGeometryIndex(execution.Chart.KeyCount, analysis.Objects);
        var columns = originalGeometry.CountSimultaneousHeadColumns(source.StartBeat);
        var vertical = new AddNotesEngine().AnalyzeVerticalDensity(execution.Chart.KeyCount, columns, execution.Options);
        var effective = Math.Min(execution.Options.Chance,
            execution.Options.Chance * vertical.ChordFactor * density.ContextualFactor);
        return new(arm, target, source.StartBeat, playable.Beat(TargetTime), effective,
            objects, commits.ToImmutableArray(), geometry, geometryHash, materializedGenerationHash,
            latent, opportunitySequenceHash, Hash(JsonSerializer.Serialize(objects, Canonical)));
    }

    private static GeometryObservationObject SnapshotObject(int order, string source, string? key,
        int? opportunityOrder, TimedManiaObject value, CanonicalPlayableGeometry playable)
    {
        var canonical = playable.Project(value);
        return new(order, source, key, opportunityOrder, value.Object.Lane, value.Object.StartTime,
            value.Object.EndTime, value.Object.Type, value.Object.Sequence, value.Object.Origin,
            value.StartBeat, value.EndBeat, canonical.StartBeat, canonical.EndBeat);
    }

    private static void ValidateReference(Execution value, bool intervention)
    {
        var expectedOutput = intervention ? BOutput : AOutput;
        var expectedDiagnostics = intervention ? BDiagnostics : ADiagnostics;
        if (value.Output != expectedOutput || value.Random.CallCount != 13730
            || value.Random.AuthenticTranscriptSha256 != SharedRng || value.Diagnostics != expectedDiagnostics
            || value.Observer.RngCalls != 0 || value.Random.AuthenticCalls[659].DoubleValue != 0.7305439518441185)
            throw new InvalidDataException("Observed arm differs from frozen reference.");
        if (intervention ? value.Random.Interventions.Length != 1 : value.Random.Interventions.Length != 0)
            throw new InvalidDataException("Intervention count drifted.");
        if (intervention && value.Random.Interventions.Single() != new SingleRngInterventionEvent(260, 4, 2, 3))
            throw new InvalidDataException("OP-185 intervention identity drifted.");
    }

    private static object Identity(Execution x) => new
    {
        x.Output, rngCalls = x.Random.CallCount, rngSha256 = x.Random.AuthenticTranscriptSha256,
        diagnosticsSha256 = x.Diagnostics, commits = x.Observer.Build().Length,
        interventions = x.Random.Interventions
    };
    private static object NoInterference(Execution x, bool b) => new
    {
        outputExact = x.Output == (b ? BOutput : AOutput), rngCallsExact = x.Random.CallCount == 13730,
        rngExact = x.Random.AuthenticTranscriptSha256 == SharedRng,
        diagnosticsExact = x.Diagnostics == (b ? BDiagnostics : ADiagnostics),
        observerRngCallsZero = x.Observer.RngCalls == 0,
        interventionExact = b ? x.Random.Interventions.SequenceEqual([new(260, 4, 2, 3)])
            : x.Random.Interventions.IsEmpty
    };

    private static string LatentIdentity(TimedManiaObject value) =>
        $"{value.Object.Lane}|{value.Object.StartTime}|{value.Object.EndTime}|{value.Object.Type}|" +
        $"{value.Object.Sequence}|{value.Object.Origin}|{value.StartBeat.ToString(CultureInfo.InvariantCulture)}|" +
        value.EndBeat.ToString(CultureInfo.InvariantCulture);
    private static string AccumulatorHash(IEnumerable<string> rows)
    {
        var value = new byte[32];
        foreach (var row in rows)
        {
            var digest = SHA256.HashData(Encoding.UTF8.GetBytes(row)); var carry = 0;
            for (var i = value.Length - 1; i >= 0; i--)
            { var sum = value[i] + digest[i] + carry; value[i] = (byte)sum; carry = sum >> 8; }
        }
        return Convert.ToHexString(value);
    }
    private static string Fingerprint(string root, IEnumerable<string> paths) =>
        ExperimentBaselineIdentityResearch.ComputeImplementationSnapshot(paths.Select(path =>
            new NamedImplementationContent(path, File.ReadAllBytes(Path.Combine(root, Native(path))))));
    private static string GitHead(string root)
    {
        var head = File.ReadAllText(Path.Combine(root, ".git", "HEAD")).Trim();
        return head.StartsWith("ref: ", StringComparison.Ordinal)
            ? File.ReadAllText(Path.Combine(root, ".git", Native(head[5..]))).Trim() : head;
    }
    private static void RequireMemory()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("DOTNET_GCHeapHardLimit"), MemoryLimit,
                StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("16 GiB heap limit required.");
    }
    private static string Native(string path) => path.Replace('/', Path.DirectorySeparatorChar);
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string FileHash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static void WriteJson<T>(string path, T value) => File.WriteAllText(path,
        JsonSerializer.Serialize(value, Json) + Environment.NewLine, new UTF8Encoding(false));
    private static void WriteHashes(string directory)
    {
        var rows = Directory.EnumerateFiles(directory).Where(x => Path.GetFileName(x) != "sha256sums.txt")
            .OrderBy(Path.GetFileName, StringComparer.Ordinal).Select(x => $"{FileHash(x)}  {Path.GetFileName(x)}");
        File.WriteAllLines(Path.Combine(directory, "sha256sums.txt"), rows, new UTF8Encoding(false));
    }

    private sealed record HistoricalInput(string Path, string Sha256);
    private sealed record Contract(string SchemaVersion, string RepositoryEntryHead,
        string ImplementationSnapshotSha256, string HarnessSnapshotSha256, string CorpusManifestSha256,
        string MemoryLimit, string ChartId, int Seed, string InterventionOpportunity,
        string TargetOpportunity, int TargetOrder, int TargetLane, int TargetTime,
        string Arms, string CompleteGeometryState, string ValidSnapshot, string LaneQuery,
        string NonInterference, ImmutableArray<string> Outcomes, ImmutableArray<string> StopConditions,
        ImmutableArray<HistoricalInput> HistoricalInputs);
    private sealed record ContractArtifact(Contract Contract, string CanonicalSha256, string Canonicalization);
    private sealed record Commit(string OpportunityKey, int OpportunityOrder, TimedManiaObject Value);
    private sealed record Execution(ManiaChart Chart, MapperEvidenceProfile Profile, AddNotesOptions Options,
        SingleRngInterventionRandom Random, SafetyRemediationPlacementObserverResearch Observer,
        AddNotesResult Result, string Output, string Diagnostics);
    private sealed record Snapshot(string Arm, SafetyRemediationGateOpportunityState Target,
        decimal LatentBeat, decimal CanonicalBeat, double EffectiveChance,
        ImmutableArray<GeometryObservationObject> Objects, ImmutableArray<Commit> Commits,
        [property: JsonIgnore] LaneGeometryIndex Geometry, string GeometryMaterializedHash,
        string MaterializedGenerationStateHash, string LatentHash, string OpportunitySequenceHash,
        string ObjectsSha256);
}
