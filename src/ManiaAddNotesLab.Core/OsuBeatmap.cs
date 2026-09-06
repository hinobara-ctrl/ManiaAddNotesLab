using System.Globalization;
using System.Text;

namespace ManiaAddNotesLab.Core;

public static class OsuBeatmap
{
    public static ManiaChart Parse(string text)
    {
        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n').ToList();
        var section = "";
        int? mode = null, keys = null;
        var timings = new List<TimingPoint>();
        var objects = new List<ManiaObject>();
        var sequence = 0;

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith('[') && line.EndsWith(']')) { section = line; continue; }
            if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal)) continue;

            if (section == "[General]" && TryValue(line, "Mode", out var modeText))
                mode = int.Parse(modeText, CultureInfo.InvariantCulture);
            else if (section == "[Difficulty]" && TryValue(line, "CircleSize", out var keyText))
                keys = (int)Math.Round(double.Parse(keyText, CultureInfo.InvariantCulture));
            else if (section == "[TimingPoints]")
            {
                var fields = line.Split(',');
                if (fields.Length >= 2 && decimal.TryParse(fields[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var time)
                    && decimal.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var beatLength)
                    && beatLength > 0)
                    timings.Add(new TimingPoint(time, beatLength));
            }
            else if (section == "[HitObjects]")
            {
                if (keys is null) throw new InvalidDataException("CircleSize must appear before HitObjects.");
                if (keys is < 1 or > 18) throw new InvalidDataException("CircleSize/keymode must be between 1K and 18K.");
                var fields = line.Split(',');
                if (fields.Length < 5) throw new InvalidDataException($"Invalid hit object: {line}");
                var x = int.Parse(fields[0], CultureInfo.InvariantCulture);
                var start = int.Parse(fields[2], CultureInfo.InvariantCulture);
                var typeBits = int.Parse(fields[3], CultureInfo.InvariantCulture);
                var lane = Math.Clamp((int)((long)x * keys.Value / 512), 0, keys.Value - 1);
                if ((typeBits & 128) != 0)
                {
                    if (fields.Length < 6) throw new InvalidDataException($"Invalid long note: {line}");
                    var endText = fields[5].Split(':')[0];
                    var end = int.Parse(endText, CultureInfo.InvariantCulture);
                    if (end <= start) throw new InvalidDataException($"Long note end must follow its head: {line}");
                    objects.Add(new ManiaObject(lane, start, ManiaObjectType.LongNote, end, false, raw, sequence++));
                }
                else
                    objects.Add(new ManiaObject(lane, start, ManiaObjectType.Tap, null, false, raw, sequence++));
            }
        }

        if (mode != 3) throw new InvalidDataException("Only osu!mania beatmaps (Mode:3) are supported.");
        if (keys is null or < 1 or > 18) throw new InvalidDataException("CircleSize/keymode must be between 1K and 18K.");
        if (timings.Count == 0) throw new InvalidDataException("No uninherited timing points were found.");

        return new ManiaChart { KeyCount = keys.Value, Lines = lines, OriginalObjects = objects, TimingPoints = timings };
    }

    public static string Write(ManiaChart chart, double chance)
    {
        var output = new List<string>();
        var inHitObjects = false;
        var wroteObjects = false;
        var versionSuffix = $" [ADD {(chance * 100).ToString("0.##", CultureInfo.InvariantCulture)}]";

        void WriteObjects()
        {
            if (wroteObjects) return;
            foreach (var obj in chart.AllObjects.OrderBy(o => o.StartTime).ThenBy(o => o.IsSynthetic).ThenBy(o => o.Sequence))
                output.Add(obj.IsSynthetic ? FormatSynthetic(obj, chart.KeyCount) : obj.RawLine!);
            wroteObjects = true;
        }

        foreach (var raw in chart.Lines)
        {
            var trimmed = raw.Trim();
            if (trimmed == "[HitObjects]")
            {
                output.Add(raw);
                inHitObjects = true;
                continue;
            }
            if (inHitObjects && trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                WriteObjects();
                inHitObjects = false;
            }
            if (inHitObjects) continue;

            if (TryValue(trimmed, "BeatmapID", out _)) output.Add("BeatmapID:0");
            else if (TryValue(trimmed, "Version", out var version)) output.Add($"Version:{version}{versionSuffix}");
            else output.Add(raw);
        }
        if (inHitObjects) WriteObjects();
        return string.Join(Environment.NewLine, output).TrimEnd() + Environment.NewLine;
    }

    private static string FormatSynthetic(ManiaObject obj, int keys)
    {
        var x = Math.Clamp((int)Math.Floor((obj.Lane + 0.5) * 512d / keys), 0, 511);
        return obj.Type == ManiaObjectType.Tap
            ? $"{x},192,{obj.StartTime},1,0,0:0:0:0:"
            : $"{x},192,{obj.StartTime},128,0,{obj.EndTime}:0:0:0:0:";
    }

    private static bool TryValue(string line, string key, out string value)
    {
        var prefix = key + ":";
        if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = line[prefix.Length..].Trim();
            return true;
        }
        value = "";
        return false;
    }
}
