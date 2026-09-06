using ManiaAddNotesLab.Core;

namespace ManiaAddNotesLab.Tests;

public sealed class BeatTimelineTests
{
    [Fact]
    public void SnapUsesTimingSectionOffsetAndSupportedDivisors()
    {
        var timeline = new BeatTimeline([new TimingPoint(1000, 500)]);
        Assert.Equal(1125, timeline.SnapToSupportedDivision(1117));
        Assert.True(timeline.IsOnSupportedDivision(1125));
        Assert.False(timeline.IsOnSupportedDivision(1117));
    }
}
