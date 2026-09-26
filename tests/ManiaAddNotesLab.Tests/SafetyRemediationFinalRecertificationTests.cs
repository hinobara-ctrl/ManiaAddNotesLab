using ManiaAddNotesLab.Core;

namespace ManiaAddNotesLab.Tests;

public sealed class SafetyRemediationFinalRecertificationTests
{
    [Fact]
    public void ExactMeasuredPartitionAndFiveMatchingDIdentitiesCloseTo188_22_5()
    {
        var source = Cases();
        var d = source.Where(x => x.HardenedClass == SafetyRemediationFinalRecertificationResearch.Unresolved)
            .Select(x => x.Identity).ToArray();

        var result = SafetyRemediationFinalRecertificationResearch.Finalize(source, d);

        Assert.Equal(188, result.Count(x => x.FinalClass == SafetyRemediationFinalRecertificationResearch.Direct));
        Assert.Equal(22, result.Count(x => x.FinalClass == SafetyRemediationFinalRecertificationResearch.Unreachable));
        Assert.Equal(5, result.Count(x => x.FinalClass == SafetyRemediationFinalRecertificationResearch.ConflictingObjectAbsent));
        Assert.DoesNotContain(result, x => x.FinalClass == SafetyRemediationFinalRecertificationResearch.Unresolved);
    }

    [Fact]
    public void MissingOrExtraneousDEvidenceCannotForceClosure()
    {
        var source = Cases();
        var d = source.Where(x => x.HardenedClass == SafetyRemediationFinalRecertificationResearch.Unresolved)
            .Select(x => x.Identity).ToList();

        Assert.Throws<InvalidDataException>(() =>
            SafetyRemediationFinalRecertificationResearch.Finalize(source, d.Take(4)));
        d[^1] = new("UNEXPECTED", 999, "UNEXPECTED");
        Assert.Throws<InvalidDataException>(() =>
            SafetyRemediationFinalRecertificationResearch.Finalize(source, d));
    }

    [Fact]
    public void HistoricalPartitionDriftCannotBeHiddenByDEvidence()
    {
        var source = Cases().ToList();
        source[0] = (source[0].Identity, SafetyRemediationFinalRecertificationResearch.Unresolved);
        var d = source.Where(x => x.HardenedClass == SafetyRemediationFinalRecertificationResearch.Unresolved)
            .Select(x => x.Identity);

        Assert.Throws<InvalidDataException>(() =>
            SafetyRemediationFinalRecertificationResearch.Finalize(source, d));
    }

    private static (FinalRecertificationCaseIdentity Identity, string HardenedClass)[] Cases() =>
        Enumerable.Range(0, 215).Select(i =>
        {
            var classification = i < 188 ? SafetyRemediationFinalRecertificationResearch.Direct
                : i < 210 ? SafetyRemediationFinalRecertificationResearch.Unreachable
                : SafetyRemediationFinalRecertificationResearch.Unresolved;
            return (new FinalRecertificationCaseIdentity($"CHART-{i}", i, $"OP-{i}"), classification);
        }).ToArray();
}
