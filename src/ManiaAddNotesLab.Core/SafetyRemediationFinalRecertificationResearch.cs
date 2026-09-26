using System.Collections.Immutable;

namespace ManiaAddNotesLab.Core;

public sealed record FinalRecertificationCaseIdentity(
    string Stratum, string ChartId, int Seed, string FrozenArm, string OpportunityKey);
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
        if (source.Length != 215 || source.Select(x => x.Identity).Distinct().Count() != 215
            || source.Any(x => x.HardenedClass is not (Direct or Unreachable or Unresolved)))
            throw new InvalidDataException("Final recertification requires 215 unique measured A/B/C cases without invented classes.");
        var unresolved = source.Where(x => x.HardenedClass == Unresolved).Select(x => x.Identity).ToHashSet();
        if (!d.IsSubsetOf(unresolved))
            throw new InvalidDataException("D evidence may classify only individually measured unresolved identities.");
        return source.Select(x => new FinalRecertificationCase(x.Identity, x.HardenedClass,
            x.HardenedClass == Unresolved && d.Contains(x.Identity)
                ? ConflictingObjectAbsent : x.HardenedClass)).ToImmutableArray();
    }
}
