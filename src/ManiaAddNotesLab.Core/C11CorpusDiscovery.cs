using System.Collections.Immutable;
using System.Security.Cryptography;
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
        var files = Directory.EnumerateFiles(fullRoot, "*.osu", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var path in files)
        {
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
        return new C11CorpusDiscoveryResult(files.Length, files.Length - invalid, invalid, generated,
            valid.Count, valid.Count - unique.Length, unique);
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
