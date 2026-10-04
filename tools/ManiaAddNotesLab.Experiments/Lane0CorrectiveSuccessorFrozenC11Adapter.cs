using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ManiaAddNotesLab.Core;

internal sealed record Lane0IntegrationCorpusLocation(
    string LocationId, string RelativePath, string ChartId, string Family,
    int KeyCount, int OriginalObjects, string DuplicateRole = "UNIQUE");

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
    internal const string PathAuthorityRelativePath =
        "docs/lane_0_corrective_successor_frozen_c11_explicit_path_authority.json";
    internal const string TrustedPathAuthoritySha256 =
        "DA86B97DA2E0309BE4C5FC035E54397846EA5B2DBA6989BDEDE4424939EDF884";
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
        ILane0IntegrationCorpusReader reader,
        ILane0IntegrationCorpusCodec codec,
        ImmutableArray<Lane0IntegrationCorpusLocation> plan)
    {
        this.reader = reader;
        this.codec = codec;
        this.plan = plan;
        ValidatePlan(manifest, plan);
    }

    internal static Lane0CorrectiveSuccessorFrozenC11Adapter CreateOfficial(
        VerifiedFrozenC11Manifest manifest, string repositoryRoot) => new(manifest,
            new DirectCorpusReader(), new OsuCorpusCodec(),
            LoadFrozenPathAuthority(repositoryRoot, manifest));

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

    internal static ImmutableArray<Lane0IntegrationCorpusLocation> LoadFrozenPathAuthority(
        string repositoryRoot, VerifiedFrozenC11Manifest manifest)
    {
        var path = Path.Combine(repositoryRoot,
            PathAuthorityRelativePath.Replace('/', Path.DirectorySeparatorChar));
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        RequireShape(root, "path authority artifact", "authority", "canonicalSha256", "canonicalization");
        var authority = root.GetProperty("authority");
        RequireShape(authority, "path authority", "schemaVersion", "historicalFrozenCorpus",
            "locationCount", "uniqueContentCount", "duplicateContentSha256",
            "uniqueOriginalObjects", "keymodes", "locations");
        var actualHash = CanonicalJsonHash(authority);
        var declaredHash = RequiredString(root, "canonicalSha256");
        if (actualHash != declaredHash || actualHash != TrustedPathAuthoritySha256)
            throw new InvalidDataException(
                $"Frozen explicit-path authority canonical hash drifted: actual={actualHash}, declared={declaredHash}.");
        if (RequiredString(authority, "schemaVersion") !=
            "lane-0-corrective-successor-frozen-c11-explicit-path-authority.1"
            || RequiredString(authority, "historicalFrozenCorpus") != ".artifacts/f2-1-corpus"
            || authority.GetProperty("locationCount").GetInt32() != ExpectedLocationCount
            || authority.GetProperty("uniqueContentCount").GetInt32() != ExpectedUniqueCount
            || RequiredString(authority, "duplicateContentSha256") != DuplicateChartId
            || authority.GetProperty("uniqueOriginalObjects").GetInt32() != ExpectedUniqueOriginalObjects
            || !authority.GetProperty("keymodes").EnumerateArray().Select(x => x.GetInt32())
                .SequenceEqual(new[] { 4, 7, 10 }))
            throw new InvalidDataException("Frozen explicit-path authority invariants drifted.");
        var result = authority.GetProperty("locations").EnumerateArray().Select(item =>
        {
            RequireShape(item, "path authority location", "locationId", "relativePath",
                "chartSha256", "family", "keyCount", "originalObjects", "duplicateRole");
            return new Lane0IntegrationCorpusLocation(RequiredString(item, "locationId"),
                RequiredString(item, "relativePath"), RequiredString(item, "chartSha256"),
                RequiredString(item, "family"), item.GetProperty("keyCount").GetInt32(),
                item.GetProperty("originalObjects").GetInt32(), RequiredString(item, "duplicateRole"));
        }).ToImmutableArray();
        ValidatePlan(manifest, result);
        return result;
    }

    internal static void ValidatePlan(VerifiedFrozenC11Manifest manifest,
        ImmutableArray<Lane0IntegrationCorpusLocation> candidate,
        ImmutableArray<Lane0IntegrationCorpusLocation>? frozenAuthority = null)
    {
        if (frozenAuthority is { } expectedAuthority
            && !candidate.OrderBy(x => x.LocationId, StringComparer.Ordinal)
                .SequenceEqual(expectedAuthority.OrderBy(x => x.LocationId, StringComparer.Ordinal)))
            throw new InvalidDataException("Frozen exact path membership drifted.");
        if (candidate.Length != ExpectedLocationCount)
            throw new InvalidDataException("Frozen corpus path membership must contain twelve locations.");
        if (candidate.Select(x => x.LocationId).Distinct(StringComparer.Ordinal).Count() != candidate.Length
            || candidate.Select(x => x.RelativePath).Distinct(StringComparer.Ordinal).Count() != candidate.Length)
            throw new InvalidDataException("Frozen corpus location identities and paths must be unique.");
        if (candidate.Any(x => Path.IsPathFullyQualified(x.RelativePath)
            || x.RelativePath.Split(['/', '\\']).Contains("..", StringComparer.Ordinal)))
            throw new InvalidDataException("Frozen corpus paths must be bounded relative paths.");
        if (candidate.Any(x => x.RelativePath.Contains('\\') || !x.RelativePath.EndsWith(".osu",
            StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Frozen corpus paths must be normalized .osu relative paths.");
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
            var roles = group.Select(x => x.DuplicateRole).Order(StringComparer.Ordinal).ToArray();
            if (group.Key == DuplicateChartId)
            {
                if (!roles.SequenceEqual(new[] { "DUPLICATE_PRIMARY", "DUPLICATE_SECONDARY" }))
                    throw new InvalidDataException("Frozen duplicate roles drifted.");
            }
            else if (!roles.SequenceEqual(new[] { "UNIQUE" }))
                throw new InvalidDataException("Unique content duplicate role drifted.");
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

    internal static string CanonicalJsonHash(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) WriteCanonical(writer, element);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal))
                { writer.WritePropertyName(property.Name); WriteCanonical(writer, property.Value); }
                writer.WriteEndObject(); break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray()) WriteCanonical(writer, item);
                writer.WriteEndArray(); break;
            case JsonValueKind.String: writer.WriteStringValue(element.GetString()); break;
            case JsonValueKind.Number: element.WriteTo(writer); break;
            case JsonValueKind.True: writer.WriteBooleanValue(true); break;
            case JsonValueKind.False: writer.WriteBooleanValue(false); break;
            case JsonValueKind.Null: writer.WriteNullValue(); break;
            default: throw new InvalidDataException("Unsupported canonical JSON value.");
        }
    }

    private static string RequiredString(JsonElement parent, string name) =>
        parent.GetProperty(name).GetString()
        ?? throw new InvalidDataException($"Path authority {name} is null.");

    private static void RequireShape(JsonElement element, string context, params string[] expected)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"{context} must be an object.");
        var actual = element.EnumerateObject().Select(x => x.Name).ToArray();
        if (actual.Length != expected.Length
            || expected.Except(actual, StringComparer.Ordinal).Any()
            || actual.Except(expected, StringComparer.Ordinal).Any())
            throw new InvalidDataException($"{context} shape drifted.");
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
