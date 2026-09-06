using ManiaAddNotesLab.Core;

namespace ManiaAddNotesLab.Tests;

public sealed class OsuBeatmapTests
{
    private const string Input = """
osu file format v14

[General]
Mode:3

[Metadata]
Title:Parser fixture
Version:Original
BeatmapID:99

[Difficulty]
CircleSize:4

[TimingPoints]
0,500,4,2,0,100,1,0
1000,-50,4,2,0,100,0,0

[HitObjects]
64,192,500,1,0,0:0:0:0:
192,192,1000,128,0,1500:0:0:0:0:
""";

    [Fact]
    public void ParserReadsAndWriterPreservesOriginalObjectsAndNeutralizesMetadata()
    {
        var chart = OsuBeatmap.Parse(Input);
        Assert.Equal(4, chart.KeyCount);
        Assert.Equal(ManiaObjectType.Tap, chart.OriginalObjects[0].Type);
        Assert.Equal(ManiaObjectType.LongNote, chart.OriginalObjects[1].Type);
        Assert.Equal(1500, chart.OriginalObjects[1].EndTime);

        var written = OsuBeatmap.Write(chart, .3);
        Assert.Contains("Title:Parser fixture", written);
        Assert.Contains("Version:Original [ADD 30]", written);
        Assert.Contains("BeatmapID:0", written);
        Assert.Contains("64,192,500,1,0,0:0:0:0:", written);
        Assert.Contains("192,192,1000,128,0,1500:0:0:0:0:", written);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(19)]
    public void ParserRejectsUnsupportedKeymode(int keys)
    {
        var input = Input.Replace("CircleSize:4", $"CircleSize:{keys}");
        Assert.Throws<InvalidDataException>(() => OsuBeatmap.Parse(input));
    }
}
