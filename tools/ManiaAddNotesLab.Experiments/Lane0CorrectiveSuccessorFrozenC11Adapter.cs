using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using ManiaAddNotesLab.Core;

internal sealed record Lane0IntegrationCorpusLocation(
    string LocationId, string RelativePath, string ChartId, string Family,
    int KeyCount, int OriginalObjects);

internal sealed record Lane0IntegrationAdmittedCorpus(
    ImmutableArray<Lane0IntegrationCorpusLocation> LocationEvidence,
    ImmutableArray<Lane0CorrectiveChartInput> ScientificCharts,
    int UniqueOriginalObjects);

internal interface ILane0IntegrationCorpusReader
{
    byte[] ReadExact(string canonicalCorpusRoot, string relativePath);
}

internal interface ILane0IntegrationCorpusCodec
{
    string Sha256(byte[] bytes);
    ManiaChart Parse(byte[] bytes);
}

internal interface ILane0IntegrationFrozenCorpusAdapter
{
    Lane0IntegrationAdmittedCorpus Admit(VerifiedFrozenC11Manifest manifest,
        string postReceiptCorpusRoot);
}

/// <summary>
/// Finite adapter for the frozen C11 manifest. It performs no discovery or enumeration: after
/// the receipt boundary it reads exactly the twelve preregistered relative location slots.
/// </summary>
internal sealed class Lane0CorrectiveSuccessorFrozenC11Adapter : ILane0IntegrationFrozenCorpusAdapter
{
    internal const string DuplicateChartId =
        "322F995448A73713DCBECE3D140F4D516EF687DBC43CC140CFF14948566F906B";
    internal const int ExpectedLocationCount = 12;
    internal const int ExpectedUniqueCount = 11;
    internal const int ExpectedUniqueOriginalObjects = 50_836;

    private readonly ILane0IntegrationCorpusReader reader;
    private readonly ILane0IntegrationCorpusCodec codec;
    private readonly ImmutableArray<Lane0IntegrationCorpusLocation> plan;

    internal Lane0CorrectiveSuccessorFrozenC11Adapter(
        VerifiedFrozenC11Manifest manifest,
        ILane0IntegrationCorpusReader? reader = null,
        ILane0IntegrationCorpusCodec? codec = null,
        ImmutableArray<Lane0IntegrationCorpusLocation>? plan = null)
    {
        this.reader = reader ?? new DirectCorpusReader();
        this.codec = codec ?? new OsuCorpusCodec();
        this.plan = plan ?? BuildOfficialPlan(manifest);
    }

    public Lane0IntegrationAdmittedCorpus Admit(
        VerifiedFrozenC11Manifest manifest, string postReceiptCorpusRoot)
    {
        if (manifest.CanonicalSha256 != FrozenC11ManifestResearch.TrustedCanonicalSha256)
            throw new InvalidDataException("Frozen C11 manifest identity drifted.");
        ValidatePlan(manifest, plan);

        // This is deliberately the first interpretation of CorpusRoot and belongs after receipt.
        var root = Path.GetFullPath(postReceiptCorpusRoot);
        var parsedByContent = new Dictionary<string, ManiaChart>(StringComparer.Ordinal);
        foreach (var location in plan.OrderBy(x => x.LocationId, StringComparer.Ordinal))
        {
            var bytes = reader.ReadExact(root, location.RelativePath);
            var actualId = codec.Sha256(bytes);
            if (actualId != location.ChartId)
                throw new InvalidDataException($"Content identity drifted at {location.LocationId}.");
            var chart = codec.Parse(bytes);
            if (chart.KeyCount != location.KeyCount || chart.OriginalObjects.Count != location.OriginalObjects)
                throw new InvalidDataException($"Frozen metadata drifted at {location.LocationId}.");
            if (parsedByContent.TryGetValue(actualId, out var first))
            {
                if (actualId != DuplicateChartId || first.KeyCount != chart.KeyCount
                    || first.OriginalObjects.Count != chart.OriginalObjects.Count)
                    throw new InvalidDataException("Unexpected or inconsistent duplicate content.");
            }
            else parsedByContent.Add(actualId, chart);
        }

        if (parsedByContent.Count != ExpectedUniqueCount)
            throw new InvalidDataException("Frozen corpus must contain exactly eleven unique contents.");
        var scientific = manifest.Charts
            .OrderBy(x => x.Sha256, StringComparer.Ordinal)
            .Select(x => new Lane0CorrectiveChartInput(x.Sha256, parsedByContent[x.Sha256]))
            .ToImmutableArray();
        return new(plan.OrderBy(x => x.LocationId, StringComparer.Ordinal).ToImmutableArray(),
            scientific, manifest.Charts.Sum(x => x.ObjectCount));
    }

    internal static ImmutableArray<Lane0IntegrationCorpusLocation> BuildOfficialPlan(
        VerifiedFrozenC11Manifest manifest)
    {
        var result = ImmutableArray.CreateBuilder<Lane0IntegrationCorpusLocation>();
        var index = 0;
        foreach (var chart in manifest.Charts.OrderBy(x => x.Sha256, StringComparer.Ordinal))
        {
            result.Add(new($"LOCATION_{index++:D2}", $"admitted/{chart.Sha256}.osu",
                chart.Sha256, chart.FamilyKey, chart.KeyCount, chart.ObjectCount));
            if (chart.Sha256 == DuplicateChartId)
                result.Add(new($"LOCATION_{index++:D2}", $"admitted/{chart.Sha256}.duplicate.osu",
                    chart.Sha256, chart.FamilyKey, chart.KeyCount, chart.ObjectCount));
        }
        return result.ToImmutable();
    }

    internal static void ValidatePlan(VerifiedFrozenC11Manifest manifest,
        ImmutableArray<Lane0IntegrationCorpusLocation> candidate)
    {
        if (candidate.Length != ExpectedLocationCount)
            throw new InvalidDataException("Frozen corpus path membership must contain twelve locations.");
        if (candidate.Select(x => x.LocationId).Distinct(StringComparer.Ordinal).Count() != candidate.Length
            || candidate.Select(x => x.RelativePath).Distinct(StringComparer.Ordinal).Count() != candidate.Length)
            throw new InvalidDataException("Frozen corpus location identities and paths must be unique.");
        if (candidate.Any(x => Path.IsPathFullyQualified(x.RelativePath)
            || x.RelativePath.Split(['/', '\\']).Contains("..", StringComparer.Ordinal)))
            throw new InvalidDataException("Frozen corpus paths must be bounded relative paths.");
        var expected = manifest.Charts.ToDictionary(x => x.Sha256, StringComparer.Ordinal);
        if (expected.Count != ExpectedUniqueCount
            || manifest.Charts.Sum(x => x.ObjectCount) != ExpectedUniqueOriginalObjects)
            throw new InvalidDataException("Frozen manifest unique-content invariants drifted.");
        foreach (var group in candidate.GroupBy(x => x.ChartId, StringComparer.Ordinal))
        {
            if (!expected.TryGetValue(group.Key, out var chart))
                throw new InvalidDataException("Frozen corpus plan contains an unknown content identity.");
            var expectedCount = group.Key == DuplicateChartId ? 2 : 1;
            if (group.Count() != expectedCount)
                throw new InvalidDataException("Frozen duplicate relationship drifted.");
            if (group.Any(x => x.Family != chart.FamilyKey || x.KeyCount != chart.KeyCount
                || x.OriginalObjects != chart.ObjectCount))
                throw new InvalidDataException("Frozen family/keymode/object metadata drifted.");
        }
        if (candidate.Select(x => x.ChartId).Distinct(StringComparer.Ordinal).Count() != ExpectedUniqueCount)
            throw new InvalidDataException("Frozen corpus plan must contain eleven unique contents.");
        var duplicate = expected[DuplicateChartId];
        if (duplicate.FamilyKey != "ORGT | DESTINY | BAIO" || duplicate.KeyCount != 7
            || duplicate.ObjectCount != 1653)
            throw new InvalidDataException("Frozen duplicate metadata drifted.");
        if (!manifest.Charts.Select(x => x.KeyCount).ToHashSet().SetEquals([4, 7, 10]))
            throw new InvalidDataException("Frozen keymode set drifted.");
    }

    private sealed class DirectCorpusReader : ILane0IntegrationCorpusReader
    {
        public byte[] ReadExact(string canonicalCorpusRoot, string relativePath)
        {
            var full = Path.GetFullPath(Path.Combine(canonicalCorpusRoot, relativePath));
            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var prefix = Path.TrimEndingDirectorySeparator(canonicalCorpusRoot)
                + Path.DirectorySeparatorChar;
            if (!full.StartsWith(prefix, comparison))
                throw new InvalidDataException("Admitted corpus path escaped its canonical root.");
            return File.ReadAllBytes(full);
        }
    }

    private sealed class OsuCorpusCodec : ILane0IntegrationCorpusCodec
    {
        public string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
        public ManiaChart Parse(byte[] bytes) => OsuBeatmap.Parse(Encoding.UTF8.GetString(bytes));
    }
}
