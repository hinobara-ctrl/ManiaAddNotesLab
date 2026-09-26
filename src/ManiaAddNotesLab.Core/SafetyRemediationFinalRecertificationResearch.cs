using System.Collections.Immutable;

namespace ManiaAddNotesLab.Core;

public sealed record FinalRecertificationCaseIdentity(string ChartId, int Seed, string OpportunityKey);
public sealed record FinalRecertificationCase(
    FinalRecertificationCaseIdentity Identity, string HardenedClass, string FinalClass);

public static class SafetyRemediationFinalRecertificationResearch
{
    public const string Direct = "A_DIRECT_CANONICAL_REJECT";
    public const string Unreachable = "B_CAUSALLY_PROVEN_UNREACHABLE";
    public const string Unresolved = "C_UNRESOLVED";
    public const string ConflictingObjectAbsent = "D_CONFLICTING_OBJECT_ABSENT";

    public static ImmutableArray<FinalRecertificationCase> Finalize(
        IEnumerable<(FinalRecertificationCaseIdentity Identity, string HardenedClass)> hardened,
        IEnumerable<FinalRecertificationCaseIdentity> independentlyDemonstratedD)
    {
        var source = hardened.ToArray();
        var d = independentlyDemonstratedD.ToHashSet();
        if (source.Length != 215 || source.Count(x => x.HardenedClass == Direct) != 188
            || source.Count(x => x.HardenedClass == Unreachable) != 22
            || source.Count(x => x.HardenedClass == Unresolved) != 5)
            throw new InvalidDataException("Final recertification requires the measured 188 A / 22 B / 5 unresolved partition before D evidence.");
        var unresolved = source.Where(x => x.HardenedClass == Unresolved).Select(x => x.Identity).ToHashSet();
        if (d.Count != 5 || !d.SetEquals(unresolved))
            throw new InvalidDataException("D evidence must match all and only the five measured unresolved identities.");
        return source.Select(x => new FinalRecertificationCase(x.Identity, x.HardenedClass,
            x.HardenedClass == Unresolved ? ConflictingObjectAbsent : x.HardenedClass)).ToImmutableArray();
    }
}
