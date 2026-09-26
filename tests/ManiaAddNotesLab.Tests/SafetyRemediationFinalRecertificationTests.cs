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
    public void MissingDEvidenceRemainsUnresolvedAndExtraneousEvidenceCannotForceClosure()
    {
        var source = Cases();
        var d = source.Where(x => x.HardenedClass == SafetyRemediationFinalRecertificationResearch.Unresolved)
            .Select(x => x.Identity).ToList();

        var partial = SafetyRemediationFinalRecertificationResearch.Finalize(source, d.Take(4));
        Assert.Equal(4, partial.Count(x => x.FinalClass ==
            SafetyRemediationFinalRecertificationResearch.ConflictingObjectAbsent));
        Assert.Single(partial, x => x.FinalClass == SafetyRemediationFinalRecertificationResearch.Unresolved);
        d[^1] = new("primary", "UNEXPECTED", 999, "control", "UNEXPECTED");
        Assert.Throws<InvalidDataException>(() =>
            SafetyRemediationFinalRecertificationResearch.Finalize(source, d));
    }

    [Fact]
    public void HistoricalPartitionDriftCannotBeHiddenByDEvidence()
    {
        var source = Cases().ToList();
        source[0] = (source[0].Identity, "INVENTED_FAVORABLE_CLASS");

        Assert.Throws<InvalidDataException>(() =>
            SafetyRemediationFinalRecertificationResearch.Finalize(source, []));
    }

    [Fact]
    public void PrimaryAndSecondaryCasesWithSameChartSeedAndOpportunityRemainDistinct()
    {
        var source = Cases().ToList();
        var first = source[0];
        source[^1] = (first.Identity with
        {
            Stratum = "secondary-g1-compatibility", FrozenArm = "treatment"
        }, SafetyRemediationFinalRecertificationResearch.Unresolved);

        var result = SafetyRemediationFinalRecertificationResearch.Finalize(source, []);

        Assert.Equal(215, result.Length);
        Assert.Single(result, x => x.Identity.Stratum == "secondary-g1-compatibility");
        source[^1] = first;
        Assert.Throws<InvalidDataException>(() =>
            SafetyRemediationFinalRecertificationResearch.Finalize(source, []));
    }

    private static (FinalRecertificationCaseIdentity Identity, string HardenedClass)[] Cases() =>
        Enumerable.Range(0, 215).Select(i =>
        {
            var classification = i < 188 ? SafetyRemediationFinalRecertificationResearch.Direct
                : i < 210 ? SafetyRemediationFinalRecertificationResearch.Unreachable
                : SafetyRemediationFinalRecertificationResearch.Unresolved;
            return (new FinalRecertificationCaseIdentity("primary", $"CHART-{i}", i,
                "control", $"OP-{i}"), classification);
        }).ToArray();
}
