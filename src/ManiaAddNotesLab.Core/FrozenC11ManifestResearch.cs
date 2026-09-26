using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ManiaAddNotesLab.Core;

public sealed record VerifiedFrozenC11Manifest(
    string CanonicalSha256,
    ImmutableArray<C11FrozenCorpusExpectation> Charts);

public static class FrozenC11ManifestResearch
{
    public const string TrustedCanonicalSha256 =
        "AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445";

    private static readonly JsonSerializerOptions PublishedJson = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static VerifiedFrozenC11Manifest Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        RequireObjectShape(root, "C11 manifest artifact", "manifest", "canonicalSha256");
        var manifest = root.GetProperty("manifest");
        ValidateManifestShape(manifest);
        var declaredHash = RequiredString(root, "canonicalSha256", "C11 manifest artifact");
        var actualHash = ComputeCanonicalSha256(manifest);
        if (!string.Equals(actualHash, declaredHash, StringComparison.Ordinal))
            throw new InvalidDataException(
                $"C11 manifest canonical content hash {actualHash} does not match declared hash {declaredHash}.");
        if (!string.Equals(actualHash, TrustedCanonicalSha256, StringComparison.Ordinal))
            throw new InvalidDataException(
                $"C11 manifest canonical content hash {actualHash} does not match trusted historical identity {TrustedCanonicalSha256}.");

        var charts = manifest.GetProperty("charts").EnumerateArray().Select(x =>
            new C11FrozenCorpusExpectation(
                RequiredString(x, "chartId", "C11 chart"),
                RequiredString(x, "family", "C11 chart"),
                x.GetProperty("keyCount").GetInt32(),
                x.GetProperty("originalObjects").GetInt32())).ToImmutableArray();
        return new(actualHash, charts);
    }

    public static string ComputeCanonicalSha256(JsonElement manifest)
    {
        ValidateManifestShape(manifest);
        var charts = manifest.GetProperty("charts").EnumerateArray().Select(x => new
        {
            chartId = RequiredString(x, "chartId", "C11 chart"),
            family = RequiredString(x, "family", "C11 chart"),
            KeyCount = x.GetProperty("keyCount").GetInt32(),
            originalObjects = x.GetProperty("originalObjects").GetInt32()
        }).ToArray();
        var seeds = manifest.GetProperty("seeds").EnumerateArray().Select(x => x.GetInt32())
            .ToImmutableArray();
        var options = manifest.GetProperty("options").Deserialize<AddNotesOptions>(PublishedJson)
            ?? throw new InvalidDataException("C11 manifest options produced no value.");
        var canonical = new
        {
            schemaVersion = RequiredString(manifest, "schemaVersion", "C11 manifest"),
            corpusFingerprint = RequiredString(manifest, "corpusFingerprint", "C11 manifest"),
            charts,
            seeds,
            options,
            controlPolicy = RequiredString(manifest, "controlPolicy", "C11 manifest"),
            treatmentPolicy = RequiredString(manifest, "treatmentPolicy", "C11 manifest"),
            purpose = RequiredString(manifest, "purpose", "C11 manifest"),
            articulation = RequiredString(manifest, "articulation", "C11 manifest"),
            unrelatedTreatments = RequiredString(manifest, "unrelatedTreatments", "C11 manifest")
        };
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(canonical)));
    }

    private static void ValidateManifestShape(JsonElement manifest)
    {
        RequireObjectShape(manifest, "C11 manifest", "schemaVersion", "corpusFingerprint", "charts",
            "seeds", "options", "controlPolicy", "treatmentPolicy", "purpose", "articulation",
            "unrelatedTreatments");
        foreach (var chart in manifest.GetProperty("charts").EnumerateArray())
            RequireObjectShape(chart, "C11 chart", "chartId", "family", "keyCount", "originalObjects");
        foreach (var seed in manifest.GetProperty("seeds").EnumerateArray()) seed.GetInt32();
        RequireObjectShape(manifest.GetProperty("options"), "C11 options",
            "chance", "startMs", "endMs", "lnWindowBeats", "sourceAffinityMultiplier",
            "distanceDecayPerBeat", "minimumDistanceWeight", "densityGraceColumns",
            "densityDecayPerColumn", "verticalDensityMode", "verticalDensityScaleMode",
            "contextualDensityNormalizationEnabled", "contextualDensityScaleMode",
            "microDensityWindowBeats", "contextDensityWindowBeats", "burstDensityRatioThreshold",
            "burstSpanOneBeatFactor", "burstSpanTwoBeatFactor", "burstSpanThreeBeatFactor",
            "useLocalLaneGap", "laneGapWindowBeats", "laneGapMinimumSupport",
            "fallbackMinimumLaneGapBeats", "mapRelativeSnapEnabled", "interiorLnOpportunitiesEnabled",
            "maxInteriorOpportunitiesPerSource", "interiorMinimumSourceBeats", "interiorLengthRatio",
            "interiorAbsoluteLongBeats", "interiorMinimumContextLnCount",
            "interiorMinimumSupportedAnchors", "interiorEligibilityMode", "interiorContextMode",
            "articulationEnabled", "articulationMaxNonHeldColumns", "maxArticulationsPerOriginalLn",
            "retriggerGapWindowBeats", "retriggerGapMinimumSupport", "trace", "diagnosticsEnabled");
    }

    private static string RequiredString(JsonElement parent, string property, string context) =>
        parent.GetProperty(property).GetString()
        ?? throw new InvalidDataException($"{context} has no {property}.");

    private static void RequireObjectShape(JsonElement element, string context, params string[] expected)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"{context} must be an object.");
        var actual = element.EnumerateObject().Select(x => x.Name).ToArray();
        var missing = expected.Except(actual, StringComparer.Ordinal).ToArray();
        var unexpected = actual.Except(expected, StringComparer.Ordinal).ToArray();
        if (missing.Length != 0 || unexpected.Length != 0 || actual.Length != expected.Length)
            throw new InvalidDataException($"{context} shape drifted; missing=[{string.Join(',', missing)}], "
                + $"unexpected=[{string.Join(',', unexpected)}].");
    }
}
