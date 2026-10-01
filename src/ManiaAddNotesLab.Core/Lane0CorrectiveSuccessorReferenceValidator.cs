using System.Collections.Immutable;

namespace ManiaAddNotesLab.Core;

public sealed record Lane0CorrectiveSuccessorReference(
    string ChartId,
    string ScientificFamily,
    string ExpectedManifestFamily,
    int Keymode,
    int HistoricallySupportedCount);

public sealed record Lane0CorrectiveSuccessorReferenceValidation(
    bool IsValid,
    ImmutableArray<string> Errors);

public static class Lane0CorrectiveSuccessorReferenceValidator
{
    public const string CorrectedSpringIdentity =
        "E73B098AA0CB6FA3BECE45E9551EF225412F5E01F6398184093A40B944F0AAE0";
    public const string HistoricalInvalidSpringIdentity =
        "E73B098A4D4C99D716D2C53B3EA3A4BFD1A75059881F30DB416631B5EDB972D4";
    public const string SecondHistoricalIdentity =
        "20651C9B11DBB0D7BA2D64A8F167577AE0452762EEA83C5A7558BA89B8D2C788";
    public const int ExpectedHistoricalOperationalCount = 11;

    private static readonly ImmutableDictionary<string, Lane0CorrectiveSuccessorReference> Expected =
        new Dictionary<string, Lane0CorrectiveSuccessorReference>(StringComparer.Ordinal)
        {
            [SecondHistoricalIdentity] = new(SecondHistoricalIdentity, "G1_INTERIOR_SPATIAL",
                "KIKUO | KARA KARA KARA NO KARA | XNETT", 7, 9),
            [CorrectedSpringIdentity] = new(CorrectedSpringIdentity, "G1_INTERIOR_SPATIAL",
                "NJK RECORD FEAT. 3L | SPRING OF DREAMS | ITZBENJA616", 7, 2)
        }.ToImmutableDictionary(StringComparer.Ordinal);

    public static ImmutableArray<Lane0CorrectiveSuccessorReference> FrozenReferences =>
        [Expected[SecondHistoricalIdentity], Expected[CorrectedSpringIdentity]];

    /// <summary>
    /// Execution-facing validation. Manifest identity and contents arrive as one verified value;
    /// callers cannot independently substitute either half of the trust decision.
    /// </summary>
    public static Lane0CorrectiveSuccessorReferenceValidation Validate(
        VerifiedFrozenC11Manifest manifest,
        IEnumerable<Lane0CorrectiveSuccessorReference> references,
        int expectedTotalHistoricalOperationalCount)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return ValidateCore(manifest.CanonicalSha256, manifest.Charts, references,
            expectedTotalHistoricalOperationalCount);
    }

    // Historical Phase 1 surface retained for its published static-validation tests. Future
    // execution code must use the VerifiedFrozenC11Manifest overload above.
    public static Lane0CorrectiveSuccessorReferenceValidation Validate(
        string manifestCanonicalSha256,
        IEnumerable<C11FrozenCorpusExpectation> manifestCharts,
        IEnumerable<Lane0CorrectiveSuccessorReference> references,
        int expectedTotalHistoricalOperationalCount)
        => ValidateCore(manifestCanonicalSha256, manifestCharts, references,
            expectedTotalHistoricalOperationalCount);

    private static Lane0CorrectiveSuccessorReferenceValidation ValidateCore(
        string manifestCanonicalSha256,
        IEnumerable<C11FrozenCorpusExpectation> manifestCharts,
        IEnumerable<Lane0CorrectiveSuccessorReference> references,
        int expectedTotalHistoricalOperationalCount)
    {
        var errors = ImmutableArray.CreateBuilder<string>();
        var charts = manifestCharts.ToArray();
        var values = references.ToArray();

        if (manifestCanonicalSha256 != FrozenC11ManifestResearch.TrustedCanonicalSha256)
            errors.Add("Frozen public C11 manifest identity is not trusted.");
        if (expectedTotalHistoricalOperationalCount != ExpectedHistoricalOperationalCount)
            errors.Add("Historical operational total must remain 11.");
        if (values.Any(x => x.ChartId == HistoricalInvalidSpringIdentity))
            errors.Add("Historical invalid Spring identity B cannot be a successor reference.");

        foreach (var duplicate in values.GroupBy(x => x.ChartId, StringComparer.Ordinal)
                     .Where(x => x.Count() != 1))
            errors.Add($"Successor chart identity must be unique: {duplicate.Key}.");

        var actualIds = values.Select(x => x.ChartId).ToHashSet(StringComparer.Ordinal);
        foreach (var missing in Expected.Keys.Where(x => !actualIds.Contains(x)))
            errors.Add($"Expected historical G1 chart is missing: {missing}.");
        foreach (var extra in actualIds.Where(x => !Expected.ContainsKey(x)))
            errors.Add($"Unexpected historical G1 chart: {extra}.");

        foreach (var reference in values)
        {
            if (!Expected.TryGetValue(reference.ChartId, out var expected)) continue;
            if (reference != expected)
                errors.Add($"Successor reference metadata or distribution drifted: {reference.ChartId}.");
            var matches = charts.Where(x => x.Sha256 == reference.ChartId).ToArray();
            if (matches.Length != 1)
            {
                errors.Add($"Successor reference must identify exactly one public manifest chart: {reference.ChartId}.");
                continue;
            }
            if (matches[0].FamilyKey != reference.ExpectedManifestFamily)
                errors.Add($"Public manifest family mismatch: {reference.ChartId}.");
            if (matches[0].KeyCount != reference.Keymode)
                errors.Add($"Public manifest keymode mismatch: {reference.ChartId}.");
        }

        if (values.Sum(x => x.HistoricallySupportedCount) != expectedTotalHistoricalOperationalCount)
            errors.Add("Historical G1 distribution does not sum to the frozen total.");

        return new(errors.Count == 0, errors.ToImmutable());
    }
}
