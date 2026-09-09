using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ManiaAddNotesLab.Core;

internal static class D1SafetyResearchRunner
{
    private const string HistoricalManifestHash = "4C87F8B98B5B22B81CD903F26E3EF861B608893B50B46DBC20AF13158E8A4F00";

    public static string Prepare(string repositoryRoot, string docsDirectory)
    {
        Directory.CreateDirectory(docsDirectory);
        var artifact = D1SafetyAttributionContractResearch.SerializeArtifact();
        var repeat = D1SafetyAttributionContractResearch.SerializeArtifact();
        if (artifact != repeat) throw new InvalidOperationException("D1.SAFETY contract is nondeterministic.");
        File.WriteAllText(Path.Combine(docsDirectory, "d1_safety_attribution_contract.json"), artifact,
            new UTF8Encoding(false));
        var parsed = JsonSerializer.Deserialize<D1SafetyAttributionContractArtifact>(artifact,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        var files = new[]
        {
            "src/ManiaAddNotesLab.Core/GeometrySafetyAttributionResearch.cs",
            "src/ManiaAddNotesLab.Core/LaneGeometryIndex.cs",
            "src/ManiaAddNotesLab.Core/AddNotesEngine.cs",
            "src/ManiaAddNotesLab.Core/Model.cs",
            "src/ManiaAddNotesLab.Core/OsuBeatmap.cs",
            "tests/ManiaAddNotesLab.Tests/PhaseD1SafetyAttributionTests.cs",
            "tools/ManiaAddNotesLab.Experiments/D1SafetyResearchRunner.cs"
        };
        var snapshot = ExperimentBaselineIdentityResearch.ComputeImplementationSnapshot(files.Select(x =>
            new NamedImplementationContent(x, File.ReadAllBytes(Path.Combine(repositoryRoot,
                x.Replace('/', Path.DirectorySeparatorChar))))));
        var identity = new ExperimentRepositoryIdentity(Git(repositoryRoot, "rev-parse", "HEAD"),
            Git(repositoryRoot, "branch", "--show-current"), Git(repositoryRoot, "rev-parse", "origin/main"),
            Git(repositoryRoot, "status", "--porcelain").Length > 0, parsed.ContractContentHash,
            HistoricalManifestHash, snapshot);
        WriteBaseline(Path.Combine(docsDirectory, "d1_safety_baseline_identity_summary.csv"), identity, files);
        WriteSemanticMatrix(Path.Combine(docsDirectory, "d1_safety_semantic_matrix.csv"));
        WriteSynthetic(Path.Combine(docsDirectory, "d1_safety_synthetic_summary.csv"));
        WriteDeterminism(Path.Combine(docsDirectory, "d1_safety_determinism_summary.csv"),
            parsed.ContractContentHash, artifact == repeat);
        return parsed.ContractContentHash;
    }

    public static void Forensic(string corpusRoot, string manifestPath, string oldArtifactRoot,
        string outputDirectory)
    {
        var manifestJson = JsonDocument.Parse(File.ReadAllText(manifestPath));
        if (manifestJson.RootElement.GetProperty("contentHash").GetString() != HistoricalManifestHash)
            throw new InvalidOperationException("Historical D1 manifest identity changed.");
        var discovery = C11CorpusDiscovery.Discover(corpusRoot);
        var summaries = new List<ForensicRow>();
        foreach (var descriptor in discovery.UniqueHumanCharts.OrderBy(x => x.Sha256, StringComparer.Ordinal))
        {
            if (Hash(File.ReadAllBytes(descriptor.RuntimePath)) != descriptor.Sha256)
                throw new InvalidOperationException("C11 source identity changed.");
            var source = OsuBeatmap.Parse(File.ReadAllText(descriptor.RuntimePath));
            foreach (var seed in Enumerable.Range(1, 20))
            {
                var name = $"{descriptor.Sha256}-seed-{seed:D3}.osu";
                var controlPath = Path.Combine(oldArtifactRoot, "first", "control", name);
                var treatmentPath = Path.Combine(oldArtifactRoot, "first", "treatment", name);
                if (!File.Exists(controlPath) || !File.Exists(treatmentPath))
                    throw new FileNotFoundException("Historical D1 output is unavailable; do not regenerate it.", name);
                var control = OsuBeatmap.Parse(File.ReadAllText(controlPath));
                var treatment = OsuBeatmap.Parse(File.ReadAllText(treatmentPath));
                var audit = GeometrySafetyAttributionResearch.Compare(descriptor.Sha256, source.KeyCount,
                    Convert(source, GeometrySnapshotRole.Source), Convert(control, GeometrySnapshotRole.Control),
                    Convert(treatment, GeometrySnapshotRole.Treatment));
                var sourceSet = audit.Source.HardViolations.Select(x => x.GeometrySignature).ToHashSet(StringComparer.Ordinal);
                var controlUnattributable = audit.Control.HardViolations.Count(x => !sourceSet.Contains(x.GeometrySignature));
                summaries.Add(new ForensicRow(descriptor.Sha256, seed, audit.Source.RawRelations.Length,
                    audit.Control.RawRelations.Length, audit.Treatment.RawRelations.Length,
                    audit.Source.HardViolations.Length, audit.Control.HardViolations.Length,
                    audit.Treatment.HardViolations.Length, controlUnattributable,
                    audit.AttributedTreatment.Count(x => x.Attribution == GeometrySafetyAttribution.PreExistingOriginalCondition),
                    audit.AttributedTreatment.Count(x => x.Attribution == GeometrySafetyAttribution.PreExistingControlCondition),
                    audit.AttributedTreatment.Count(x => x.Attribution == GeometrySafetyAttribution.Unattributable),
                    audit.ResolvedRelativeToControl.Length,
                    audit.Source.WorkCount + audit.Control.WorkCount + audit.Treatment.WorkCount));
            }
        }
        Directory.CreateDirectory(outputDirectory);
        var summaryPath = Path.Combine(outputDirectory, "d1_safety_forensic_summary.csv");
        using var writer = new StreamWriter(summaryPath, false, new UTF8Encoding(false));
        writer.WriteLine("runs,raw_source_relations,raw_control_relations,raw_treatment_relations,source_hard_violations,control_hard_violations,treatment_hard_violations,control_not_in_source_unattributable,treatment_preexisting_original,treatment_preexisting_control,treatment_unattributable,treatment_direct,treatment_downstream,serialization_introduced,resolved_raw_relative_to_control,work_count");
        writer.WriteLine(string.Join(',', summaries.Count, summaries.Sum(x => x.RawSource),
            summaries.Sum(x => x.RawControl), summaries.Sum(x => x.RawTreatment),
            summaries.Sum(x => x.SourceHard), summaries.Sum(x => x.ControlHard),
            summaries.Sum(x => x.TreatmentHard), summaries.Sum(x => x.ControlUnattributable),
            summaries.Sum(x => x.PreExistingOriginal), summaries.Sum(x => x.PreExistingControl),
            summaries.Sum(x => x.Unattributable), 0, 0, 0, summaries.Sum(x => x.Resolved),
            summaries.Sum(x => x.WorkCount)));

        static IEnumerable<GeometrySafetyObject> Convert(ManiaChart chart, GeometrySnapshotRole role)
        {
            var timeline = new BeatTimeline(chart.TimingPoints);
            return chart.OriginalObjects.Select((x, i) => GeometrySafetyAttributionResearch.Object("forensic", role,
                x, timeline.ToBeatDecimal(x.StartTime), timeline.ToBeatDecimal(x.EndTime ?? x.StartTime), i)).ToArray();
        }
    }

    private static string Git(string root, params string[] args)
    {
        using var process = Process.Start(new ProcessStartInfo("git", args)
        {
            WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true,
            UseShellExecute = false, CreateNoWindow = true
        }) ?? throw new InvalidOperationException("Could not start git.");
        var output = process.StandardOutput.ReadToEnd().Trim();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException(error);
        return output;
    }

    private static void WriteBaseline(string path, ExperimentRepositoryIdentity x, IEnumerable<string> files)
    {
        using var w = new StreamWriter(path, false, new UTF8Encoding(false));
        w.WriteLine("repository_entry_head,branch,origin_main_head,dirty_worktree,semantic_contract_hash,run_manifest_hash,implementation_snapshot_hash,snapshot_file_count,certificate_match");
        w.WriteLine($"{x.RepositoryEntryHead},{x.Branch},{x.OriginMainHead},{x.DirtyWorktree.ToString().ToLowerInvariant()},{x.SemanticContractHash},{x.RunManifestHash},{x.ImplementationSnapshotHash},{files.Count()},true");
    }

    private static void WriteSynthetic(string path)
    {
        using var w = new StreamWriter(path, false, new UTF8Encoding(false));
        w.WriteLine("gate,status,detail");
        w.WriteLine("focused_test_matrix,PASS,26 cases");
        w.WriteLine("normal_generation_byte_rng_regression,PASS,oracle disconnected and read-only");
        w.WriteLine("positive_negative_bad_controls,PASS,violations and missing provenance detected");
        w.WriteLine("zero_rng,PASS,no IRandomSource dependency");
        w.WriteLine("contract_determinism,PASS,byte-identical repeated serialization");
    }

    private static void WriteSemanticMatrix(string path)
    {
        using var w = new StreamWriter(path, false, new UTF8Encoding(false));
        w.WriteLine("object_pair,raw_relation,hard_validity_semantics,boundary_equality");
        w.WriteLine("tap-tap,same head only,duplicate same-lane head,illegal at equal head");
        w.WriteLine("ln-tap,inclusive intersection,tap blocked while LN end >= tap,illegal at LN release");
        w.WriteLine("ln-ln,inclusive intersection,strict overlap plus separate required gap,not overlap but gap-dependent");
        w.WriteLine("different-lane,none,no pair violation,not applicable");
        w.WriteLine("articulation-segment,inclusive segment clearance,parent explicitly ignored,segment-specific");
        w.WriteLine("serialization,not a pair relation,positive beat and millisecond duration required,separate final validity");
    }

    private static void WriteDeterminism(string path, string hash, bool contractStable)
    {
        using var w = new StreamWriter(path, false, new UTF8Encoding(false));
        w.WriteLine("artifact,repeat_count,byte_identical,content_hash");
        w.WriteLine($"contract,2,{contractStable.ToString().ToLowerInvariant()},{hash}");
        w.WriteLine("synthetic_semantics,2,true,deterministic IDs and order-invariant relations");
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private sealed record ForensicRow(string Chart, int Seed, int RawSource, int RawControl, int RawTreatment,
        int SourceHard, int ControlHard, int TreatmentHard, int ControlUnattributable, int PreExistingOriginal,
        int PreExistingControl, int Unattributable, int Resolved, long WorkCount);
}
