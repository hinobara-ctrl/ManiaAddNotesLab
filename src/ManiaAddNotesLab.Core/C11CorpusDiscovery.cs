using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ManiaAddNotesLab.Core;

public sealed record C11CorpusChartDescriptor(
    string RuntimePath,
    string RelativePath,
    string Sha256,
    string Artist,
    string Title,
    string Creator,
    string Version,
    string FamilyKey,
    int KeyCount,
    int ObjectCount,
    int TapCount,
    int LongNoteCount,
    int TimingPointCount,
    double MinimumBpm,
    double MaximumBpm,
    ImmutableArray<string> DuplicateRelativePaths);

public sealed record C11CorpusDiscoveryResult(
    int OsuFileCount,
    int ValidManiaFileCount,
    int InvalidFileCount,
    int GeneratedOutputCount,
    int HumanOriginalLocationCount,
    int ExactDuplicateLocationCount,
    ImmutableArray<C11CorpusChartDescriptor> UniqueHumanCharts);

public sealed record C11FrozenCorpusExpectation(
    string Sha256,
    string FamilyKey,
    int KeyCount,
    int ObjectCount);

public sealed record C11FrozenUnexpectedContent(string RelativePath, string Sha256);

public sealed record C11FrozenDuplicateContent(
    string Sha256,
    ImmutableArray<string> RelativePaths);

public sealed record C11FrozenCorpusVerification(
    string CorpusRoot,
    int OsuFileCount,
    ImmutableArray<C11CorpusChartDescriptor> Charts,
    ImmutableArray<string> MissingHashes,
    ImmutableArray<C11FrozenUnexpectedContent> UnexpectedContents,
    ImmutableArray<string> InvalidFiles,
    ImmutableArray<string> MetadataMismatches,
    ImmutableArray<C11FrozenDuplicateContent> Duplicates)
{
    public bool IsExact => MissingHashes.IsEmpty && UnexpectedContents.IsEmpty
        && InvalidFiles.IsEmpty && MetadataMismatches.IsEmpty;
}

/// <summary>Read-only C1.1 corpus discovery. It never writes, moves, or copies source files.</summary>
public static partial class C11CorpusDiscovery
{
    public static C11CorpusDiscoveryResult Discover(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var fullRoot = Path.GetFullPath(root);
        if (!Directory.Exists(fullRoot)) throw new DirectoryNotFoundException(fullRoot);
        var valid = new List<(string Path, string Relative, string Hash, ManiaChart Chart)>();
        var invalid = 0;
        var generated = 0;
        var fileCount = 0;
        foreach (var path in Directory.EnumerateFiles(fullRoot, "*.osu", SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            fileCount++;
            try
            {
                var bytes = File.ReadAllBytes(path);
                var chart = OsuBeatmap.Parse(System.Text.Encoding.UTF8.GetString(bytes));
                if (GeneratedName().IsMatch(Path.GetFileNameWithoutExtension(path)))
                {
                    generated++;
                    continue;
                }
                valid.Add((path, Path.GetRelativePath(fullRoot, path).Replace('\\', '/'),
                    Convert.ToHexString(SHA256.HashData(bytes)), chart));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                or InvalidDataException or FormatException or OverflowException)
            {
                invalid++;
            }
        }

        var unique = valid.GroupBy(x => x.Hash, StringComparer.OrdinalIgnoreCase).Select(group =>
        {
            var first = group.OrderBy(x => x.Relative, StringComparer.OrdinalIgnoreCase).First();
            var artist = Metadata(first.Chart, "Artist");
            var title = Metadata(first.Chart, "Title");
            var creator = Metadata(first.Chart, "Creator");
            var version = Metadata(first.Chart, "Version");
            return new C11CorpusChartDescriptor(first.Path, first.Relative, first.Hash, artist, title,
                creator, version, Family(artist, title, creator), first.Chart.KeyCount,
                first.Chart.OriginalObjects.Count,
                first.Chart.OriginalObjects.Count(x => x.Type == ManiaObjectType.Tap),
                first.Chart.OriginalObjects.Count(x => x.Type == ManiaObjectType.LongNote),
                first.Chart.TimingPoints.Count,
                first.Chart.TimingPoints.Min(x => 60000.0 / (double)x.BeatLength),
                first.Chart.TimingPoints.Max(x => 60000.0 / (double)x.BeatLength),
                group.Select(x => x.Relative).OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToImmutableArray());
        }).OrderBy(x => x.FamilyKey, StringComparer.Ordinal).ThenBy(x => x.Version, StringComparer.Ordinal)
            .ToImmutableArray();
        return new C11CorpusDiscoveryResult(fileCount, fileCount - invalid, invalid, generated,
            valid.Count, valid.Count - unique.Length, unique);
    }

    /// <summary>
    /// Resolves an explicitly supplied frozen corpus without searching any parent beatmap library.
    /// Hashing happens before parsing, so unexpected content is never retained as a chart.
    /// Exact duplicate locations are reported but do not change the unique population.
    /// </summary>
    public static C11FrozenCorpusVerification VerifyFrozen(string root,
        IEnumerable<C11FrozenCorpusExpectation> expectations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(expectations);
        var fullRoot = Path.GetFullPath(root);
        if (!Directory.Exists(fullRoot))
            throw new DirectoryNotFoundException($"Frozen C11 corpus directory does not exist: {fullRoot}");

        var expected = expectations.ToDictionary(x => x.Sha256.ToUpperInvariant(),
            StringComparer.OrdinalIgnoreCase);
        if (expected.Count == 0) throw new ArgumentException("Frozen C11 manifest contains no charts.",
            nameof(expectations));
        var matches = expected.Keys.ToDictionary(x => x, _ => new List<(string Path, string Relative)>(),
            StringComparer.OrdinalIgnoreCase);
        var unexpected = ImmutableArray.CreateBuilder<C11FrozenUnexpectedContent>();
        var invalid = ImmutableArray.CreateBuilder<string>();
        var descriptors = new Dictionary<string, C11CorpusChartDescriptor>(StringComparer.OrdinalIgnoreCase);
        var metadataMismatches = ImmutableArray.CreateBuilder<string>();
        var fileCount = 0;

        foreach (var path in Directory.EnumerateFiles(fullRoot, "*.osu", SearchOption.AllDirectories)
                     .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            fileCount++;
            var relative = Path.GetRelativePath(fullRoot, path).Replace('\\', '/');
            try
            {
                var bytes = File.ReadAllBytes(path);
                var hash = Convert.ToHexString(SHA256.HashData(bytes));
                if (!expected.TryGetValue(hash, out var requirement))
                {
                    unexpected.Add(new(relative, hash));
                    continue;
                }

                matches[hash].Add((path, relative));
                if (descriptors.ContainsKey(hash)) continue;
                var chart = OsuBeatmap.Parse(Encoding.UTF8.GetString(bytes));
                var descriptor = Describe(path, relative, hash, chart, [relative]);
                descriptors.Add(hash, descriptor);
                if (descriptor.KeyCount != requirement.KeyCount
                    || descriptor.ObjectCount != requirement.ObjectCount
                    || !string.Equals(descriptor.FamilyKey, requirement.FamilyKey,
                        StringComparison.Ordinal))
                    metadataMismatches.Add($"{hash}: expected family={requirement.FamilyKey}, "
                        + $"keys={requirement.KeyCount}, objects={requirement.ObjectCount}; observed "
                        + $"family={descriptor.FamilyKey}, keys={descriptor.KeyCount}, "
                        + $"objects={descriptor.ObjectCount}");
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                or InvalidDataException or FormatException or OverflowException)
            {
                invalid.Add($"{relative}: {exception.GetType().Name}: {exception.Message}");
            }
        }

        var missing = expected.Keys.Where(x => matches[x].Count == 0)
            .Order(StringComparer.Ordinal).ToImmutableArray();
        var duplicates = matches.Where(x => x.Value.Count > 1).OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => new C11FrozenDuplicateContent(x.Key, x.Value.Select(y => y.Relative)
                .Order(StringComparer.OrdinalIgnoreCase).ToImmutableArray())).ToImmutableArray();
        var charts = descriptors.Values.OrderBy(x => x.Sha256, StringComparer.Ordinal).Select(x =>
        {
            var locations = matches[x.Sha256].OrderBy(y => y.Relative, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return x with
            {
                RuntimePath = locations[0].Path,
                RelativePath = locations[0].Relative,
                DuplicateRelativePaths = locations.Select(y => y.Relative).ToImmutableArray()
            };
        }).ToImmutableArray();
        return new(fullRoot, fileCount, charts, missing,
            unexpected.OrderBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase).ToImmutableArray(),
            invalid.ToImmutable(), metadataMismatches.ToImmutable(), duplicates);
    }

    public static C11FrozenCorpusVerification ResolveFrozen(string root,
        IEnumerable<C11FrozenCorpusExpectation> expectations)
    {
        var result = VerifyFrozen(root, expectations);
        if (result.IsExact) return result;
        var details = new List<string>();
        if (!result.MissingHashes.IsEmpty)
            details.Add("missing hashes: " + string.Join(", ", result.MissingHashes));
        if (!result.UnexpectedContents.IsEmpty)
            details.Add("unexpected content: " + string.Join(", ", result.UnexpectedContents
                .Select(x => $"{x.RelativePath} ({x.Sha256})")));
        if (!result.InvalidFiles.IsEmpty)
            details.Add("invalid files: " + string.Join("; ", result.InvalidFiles));
        if (!result.MetadataMismatches.IsEmpty)
            details.Add("metadata mismatches: " + string.Join("; ", result.MetadataMismatches));
        throw new InvalidDataException("Frozen C11 corpus verification failed; " + string.Join("; ", details));
    }

    private static C11CorpusChartDescriptor Describe(string path, string relative, string hash,
        ManiaChart chart, ImmutableArray<string> locations)
    {
        var artist = Metadata(chart, "Artist");
        var title = Metadata(chart, "Title");
        var creator = Metadata(chart, "Creator");
        var version = Metadata(chart, "Version");
        return new(path, relative, hash, artist, title, creator, version,
            Family(artist, title, creator), chart.KeyCount, chart.OriginalObjects.Count,
            chart.OriginalObjects.Count(x => x.Type == ManiaObjectType.Tap),
            chart.OriginalObjects.Count(x => x.Type == ManiaObjectType.LongNote),
            chart.TimingPoints.Count,
            chart.TimingPoints.Min(x => 60000.0 / (double)x.BeatLength),
            chart.TimingPoints.Max(x => 60000.0 / (double)x.BeatLength), locations);
    }

    private static string Metadata(ManiaChart chart, string key)
    {
        var prefix = key + ":";
        var line = chart.Lines.FirstOrDefault(x => x.TrimStart().StartsWith(prefix,
            StringComparison.OrdinalIgnoreCase));
        return line is null ? "" : line[(line.IndexOf(':') + 1)..].Trim();
    }

    private static string Family(params string[] fields) => string.Join(" | ", fields
        .Select(value => Regex.Replace(value.Trim(), @"\s+", " ").ToUpperInvariant()));

    [GeneratedRegex(@"\[ADD\s+[^\]]+\]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GeneratedName();
}
