using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>Deterministic successor artifact serialization; contains no authority or root logic.</summary>
internal static class Lane0CorrectiveSuccessorPackage
{
    internal static readonly ImmutableArray<string> ArtifactNames =
    [
        "chart_family_keymode_results.csv", "execution_identity.json",
        "g1_historical_operational_cases.csv", "g1_state_transitions.csv", "integrity.json",
        "scientific_summary.json", "sha256sums.txt", "successor_references.json"
    ];

    internal static ImmutableSortedDictionary<string, byte[]> Build(
        Lane0CorrectiveEvaluationResult result, Lane0SuccessorEvaluationContext context)
    {
        if (result.SchemaVersion != Lane0CorrectiveSuccessorEvaluationRunner.SchemaVersion)
            throw new InvalidDataException("Only successor scientific results may enter the successor package.");
        var package = ImmutableSortedDictionary.CreateBuilder<string, byte[]>(StringComparer.Ordinal);
        package["scientific_summary.json"] = JsonBytes(result);
        package["chart_family_keymode_results.csv"] = CsvBytes(
            "chart_id,keymode,family,historical_structural,historical_joint_support,historical_operational,historical_operational_supported,corrected_structural,corrected_joint_support,corrected_operational,corrected_operational_supported,corrected_integrity_failures",
            result.Charts.Select(x => string.Join(',', x.ChartId, x.KeyCount, x.Family,
                x.Historical.Structural, x.Historical.JointSupport, x.Historical.Operational,
                x.Historical.OperationalSupported, x.Corrected.Structural, x.Corrected.JointSupport,
                x.Corrected.Operational, x.Corrected.OperationalSupported,
                x.Corrected.IntegrityFailures)));
        package["g1_state_transitions.csv"] = CsvBytes(
            "chart_id,keymode,occurrence_id,group_id,historical_state,corrected_state,historical_joint_supported,corrected_joint_supported,historical_operational,corrected_operational,transition_kind",
            result.G1Transitions.Select(x => string.Join(',', x.ChartId, x.KeyCount, x.OccurrenceId,
                x.GroupId, x.HistoricalState, x.CorrectedState, x.HistoricalJointSupported,
                x.CorrectedJointSupported, x.HistoricalOperational, x.CorrectedOperational,
                x.TransitionKind)));
        package["g1_historical_operational_cases.csv"] = CsvBytes(
            "chart_id,keymode,occurrence_id,historical_state,corrected_state,corrected_supported",
            result.HistoricalOperationalG1Cases.Select(x => string.Join(',', x.ChartId, x.KeyCount,
                x.OccurrenceId, x.HistoricalState, x.CorrectedState, x.CorrectedSupported)));
        package["integrity.json"] = JsonBytes(new
        {
            result.Outcome, result.IntegrityFailures, result.BlockedReasons, result.ResearchRngCalls,
            result.BehaviorChanged, result.InputsUnchanged
        });
        package["execution_identity.json"] = JsonBytes(new
        {
            schemaVersion = Lane0CorrectiveSuccessorEvaluationRunner.SchemaVersion,
            context.ApprovedPublicHead, context.Phase1ContractSha256, context.Phase2ContractSha256,
            context.ManifestSha256, context.ScientificImplementationSha256, context.HarnessSha256,
            context.PackageVerifierSha256, context.FrozenDependenciesSha256,
            scientificOutcome = result.Outcome
        });
        package["successor_references.json"] = JsonBytes(new
        {
            historicalInvalidSpringB = ManiaAddNotesLab.Core.Lane0CorrectiveSuccessorReferenceValidator
                .HistoricalInvalidSpringIdentity,
            activeReferences = context.References.OrderBy(x => x.ChartId, StringComparer.Ordinal).ToArray(),
            historicalDistribution = "9+2=11"
        });
        var semantic = package.ToImmutable();
        package["sha256sums.txt"] = Encoding.UTF8.GetBytes(string.Join('\n', semantic.Select(pair =>
            $"{Convert.ToHexString(SHA256.HashData(pair.Value))}  {pair.Key}")) + "\n");
        var complete = package.ToImmutable();
        Verify(complete);
        return complete;
    }

    internal static void Verify(ImmutableSortedDictionary<string, byte[]> package)
    {
        if (!package.Keys.SequenceEqual(ArtifactNames))
            throw new InvalidDataException("Successor package filenames or ordinal order drifted.");
        var expected = string.Join('\n', package.Where(x => x.Key != "sha256sums.txt").Select(pair =>
            $"{Convert.ToHexString(SHA256.HashData(pair.Value))}  {pair.Key}")) + "\n";
        if (!package["sha256sums.txt"].AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(expected)))
            throw new InvalidDataException("Successor package checksum manifest drifted.");
    }

    internal static bool ByteIdentical(ImmutableSortedDictionary<string, byte[]> left,
        ImmutableSortedDictionary<string, byte[]> right) => left.Keys.SequenceEqual(right.Keys)
        && left.All(pair => pair.Value.AsSpan().SequenceEqual(right[pair.Key]));

    private static byte[] JsonBytes(object value) => Encoding.UTF8.GetBytes(
        JsonSerializer.Serialize(value, new JsonSerializerOptions
        {
            WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n");
    private static byte[] CsvBytes(string header, IEnumerable<string> rows) => Encoding.UTF8.GetBytes(
        string.Join('\n', new[] { header }.Concat(rows)) + "\n");
}
