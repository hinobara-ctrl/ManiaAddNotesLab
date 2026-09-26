using System.Text.Json;
using ManiaAddNotesLab.Core;

internal sealed record FrozenC11Manifest(
    string CanonicalSha256,
    IReadOnlyList<C11FrozenCorpusExpectation> Charts)
{
    public static FrozenC11Manifest Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var manifest = root.GetProperty("manifest");
        var declaredHash = root.GetProperty("canonicalSha256").GetString()
            ?? throw new InvalidDataException("C11 manifest has no canonicalSha256.");
        var charts = manifest.GetProperty("charts").EnumerateArray().Select(x =>
            new C11FrozenCorpusExpectation(
                x.GetProperty("chartId").GetString()
                    ?? throw new InvalidDataException("C11 chart has no chartId."),
                x.GetProperty("family").GetString()
                    ?? throw new InvalidDataException("C11 chart has no family."),
                x.GetProperty("keyCount").GetInt32(),
                x.GetProperty("originalObjects").GetInt32())).ToArray();
        return new(declaredHash.ToUpperInvariant(), charts);
    }
}

internal static class FrozenC11CorpusRunner
{
    public static C11FrozenCorpusVerification Verify(string corpusRoot, string manifestPath)
    {
        var manifest = FrozenC11Manifest.Load(manifestPath);
        var result = C11CorpusDiscovery.ResolveFrozen(corpusRoot, manifest.Charts);
        Console.WriteLine($"C11 frozen corpus verified: unique={result.Charts.Length}, "
            + $"locations={result.OsuFileCount}, duplicates={result.Duplicates.Length}, "
            + $"manifest={manifest.CanonicalSha256}");
        foreach (var duplicate in result.Duplicates)
            Console.WriteLine($"C11 exact duplicate {duplicate.Sha256}: "
                + string.Join(", ", duplicate.RelativePaths));
        return result;
    }

    public static void CompareWithLegacyDiscovery(string corpusRoot, string manifestPath)
    {
        var frozen = Verify(corpusRoot, manifestPath);
        var ids = frozen.Charts.Select(x => x.Sha256).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var legacy = C11CorpusDiscovery.Discover(corpusRoot).UniqueHumanCharts
            .Where(x => ids.Contains(x.Sha256)).OrderBy(x => x.Sha256, StringComparer.Ordinal).ToArray();
        var current = frozen.Charts.OrderBy(x => x.Sha256, StringComparer.Ordinal).ToArray();
        if (legacy.Length != current.Length) throw new InvalidDataException(
            $"Legacy/frozen C11 population differs: legacy={legacy.Length}, frozen={current.Length}.");
        for (var i = 0; i < current.Length; i++)
        {
            var left = legacy[i];
            var right = current[i];
            if (left.Sha256 != right.Sha256 || left.FamilyKey != right.FamilyKey
                || left.KeyCount != right.KeyCount || left.ObjectCount != right.ObjectCount
                || left.TapCount != right.TapCount || left.LongNoteCount != right.LongNoteCount
                || left.TimingPointCount != right.TimingPointCount
                || left.MinimumBpm != right.MinimumBpm || left.MaximumBpm != right.MaximumBpm)
                throw new InvalidDataException($"Legacy/frozen descriptor mismatch at {right.Sha256}.");
        }
        Console.WriteLine("C11 legacy-versus-frozen comparison PASS: hashes, authored content identity, "
            + "metadata, object denominators and timing descriptors are exact for 11/11 charts.");
    }
}
