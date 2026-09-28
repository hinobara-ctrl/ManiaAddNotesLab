using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManiaAddNotesLab.Core;

internal sealed record Lane0CorrectiveChartInput(string ChartId, ManiaChart Chart);

internal sealed record Lane0PopulationSummary(
    int Structural, int Holdouts, int Operational, int ComparableContext, int JointSupport,
    int JointUnique, int JointAmongAlternatives, int Contradiction, int NoContext,
    int MarginalOnly, int AmbiguousIntegrity, int OperationalSupported, int IntegrityFailures);

internal sealed record Lane0FamilyCriterion(
    string Family, int SupportedCharts, int SupportedKeymodes, int OperationalSupported, bool Passed);

internal sealed record Lane0CorrectiveChartResult(
    string ChartId, int KeyCount, string Family,
    Lane0PopulationSummary Historical, Lane0PopulationSummary Corrected);

internal sealed record Lane0G1Transition(
    string ChartId, int KeyCount, string OccurrenceId, string GroupId,
    string HistoricalState, string CorrectedState,
    bool HistoricalJointSupported, bool CorrectedJointSupported,
    int HistoricalComparableDonors, int CorrectedComparableDonors,
    int HistoricalMatchingJointDonors, int CorrectedMatchingJointDonors,
    bool HistoricalOperational, bool CorrectedOperational, string TransitionKind,
    int DonorsConsidered, int Eligible, int TemporalExclusion, int FutureHeldIdentityExclusion,
    int BothTemporalAndIdentity, int UniqueFutureHeldExcluded, int UniqueExcluded);

internal sealed record Lane0TransitionCount(string HistoricalState, string CorrectedState, int Count);

internal sealed record Lane0HistoricalOperationalCase(
    string ChartId, int KeyCount, string OccurrenceId, string HistoricalState, string CorrectedState,
    int DonorsConsidered, int Eligible, int TemporalExclusion, int FutureHeldIdentityExclusion,
    int BothTemporalAndIdentity, int UniqueFutureHeldExcluded, int UniqueExcluded, bool CorrectedSupported);

internal sealed record Lane0HistoricalCaseRule(
    int ExpectedCount, ImmutableSortedDictionary<string, int> ExpectedChartDistribution, int ExpectedKeymode);

internal sealed record Lane0FrozenFamilySnapshot(
    int Structural, int ComparableContext, int JointSupport, int JointUnique,
    int JointAmongAlternatives, int Contradiction, int NoContext, int Operational,
    int OperationalSupported, int SupportedCharts, int SupportedKeymodes);

internal sealed record Lane0FrozenSnapshot(
    Lane0FrozenFamilySnapshot Rice, Lane0FrozenFamilySnapshot G1);

internal sealed record Lane0CorrectiveEvaluationOptions(
    bool PrerequisitesComplete,
    Lane0FrozenSnapshot? RequiredHistoricalSnapshot = null,
    Lane0HistoricalCaseRule? RequiredHistoricalG1Cases = null);

internal sealed record Lane0CorrectiveEvaluationResult(
    string SchemaVersion, string Outcome,
    ImmutableArray<Lane0CorrectiveChartResult> Charts,
    ImmutableArray<Lane0FamilyCriterion> Criteria,
    ImmutableArray<Lane0G1Transition> G1Transitions,
    ImmutableArray<Lane0TransitionCount> G1TransitionMatrix,
    ImmutableArray<Lane0HistoricalOperationalCase> HistoricalOperationalG1Cases,
    ImmutableArray<string> IntegrityFailures,
    ImmutableArray<string> BlockedReasons,
    int ResearchRngCalls,
    bool BehaviorChanged,
    bool InputsUnchanged);

internal sealed record Lane0G1BranchComparison(
    ImmutableArray<Lane0G1Transition> Transitions,
    ImmutableArray<string> IntegrityFailures);

/// <summary>
/// Research-only corrective core. It receives already-loaded charts, performs no discovery or network/file input,
/// consumes no RNG and has no CLI call site. Historical and corrected G1 branches share one base census.
/// </summary>
internal static class Lane0CorrectiveEvaluationRunner
{
    internal const string SchemaVersion = "lane-0-corrective-evaluation-core.1";
    internal static readonly Lane0FrozenSnapshot FrozenHistoricalSnapshot = new(
        new(40360, 39596, 37080, 693, 36387, 2516, 764, 40360, 37080, 11, 3),
        new(16881, 9540, 3373, 192, 3181, 6167, 7341, 288, 11, 5, 2));
    internal static readonly Lane0HistoricalCaseRule FrozenHistoricalG1CaseRule = new(11,
        ImmutableSortedDictionary<string, int>.Empty.WithComparers(StringComparer.Ordinal)
            .Add("20651C9B11DBB0D7BA2D64A8F167577AE0452762EEA83C5A7558BA89B8D2C788", 9)
            .Add("E73B098A4D4C99D716D2C53B3EA3A4BFD1A75059881F30DB416631B5EDB972D4", 2), 7);
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static Lane0CorrectiveEvaluationResult Evaluate(
        IEnumerable<Lane0CorrectiveChartInput> inputs, Lane0CorrectiveEvaluationOptions options)
    {
        var charts = inputs.OrderBy(x => x.ChartId, StringComparer.Ordinal).ToArray();
        var rows = ImmutableArray.CreateBuilder<Lane0CorrectiveChartResult>();
        var transitions = ImmutableArray.CreateBuilder<Lane0G1Transition>();
        var failures = ImmutableArray.CreateBuilder<string>();
        var fingerprints = new List<(ManiaChart Chart, string Before)>();
        var rng = 0;

        foreach (var input in charts)
        {
            var before = MapperEvidenceProfileBuilder.ComputeFingerprint(input.Chart);
            fingerprints.Add((input.Chart, before));

            var rice = BuildFrozenRiceOccurrences(input.ChartId, ChordCompletionResearch.Evaluate(input.Chart));
            var riceTrials = Lane0SpatialResearch.EvaluateAll(rice);
            var riceSummary = Summarize(rice, riceTrials, rice.Select(x => x.OccurrenceId).ToHashSet(
                StringComparer.Ordinal));
            rows.Add(new(input.ChartId, input.Chart.KeyCount, "RICE_HEAD_COMPLETION",
                riceSummary, riceSummary));

            var census = InteriorRelationFeasibilityResearch.Evaluate(input.Chart);
            rng += census.ResearchRngCalls;
            var historical = Lane0FeasibilityRunner.BuildG1Occurrences(input.ChartId, census,
                reproduceHistoricalDefect: true);
            var corrected = Lane0FeasibilityRunner.BuildG1Occurrences(input.ChartId, census,
                reproduceHistoricalDefect: false);
            var operationalAnchors = census.CurrentGateAudit.Where(x => x.CurrentOpportunity)
                .Select(x => x.AnchorId).ToHashSet(StringComparer.Ordinal);
            var historicalOperational = historical.Where(x => operationalAnchors.Contains(x.GroupId))
                .Select(x => x.OccurrenceId).ToHashSet(StringComparer.Ordinal);
            var correctedOperational = corrected.Where(x => operationalAnchors.Contains(x.GroupId))
                .Select(x => x.OccurrenceId).ToHashSet(StringComparer.Ordinal);
            if (!historicalOperational.SetEquals(correctedOperational))
                failures.Add($"{input.ChartId}: operational G1 identity set drifted.");

            var comparison = CompareG1Branches(historical, corrected, historicalOperational,
                correctedOperational);
            failures.AddRange(comparison.IntegrityFailures.Select(x => $"{input.ChartId}: {x}"));
            transitions.AddRange(comparison.Transitions);

            var historicalTrials = Lane0SpatialResearch.EvaluateAll(historical,
                requireTemporalIntegrity: false);
            var correctedTrials = Lane0SpatialResearch.EvaluateAll(corrected,
                requireTemporalIntegrity: true);
            rows.Add(new(input.ChartId, input.Chart.KeyCount, "G1_INTERIOR_SPATIAL",
                Summarize(historical, historicalTrials, historicalOperational),
                Summarize(corrected, correctedTrials, correctedOperational)));
        }

        var unchanged = fingerprints.All(x => x.Before == MapperEvidenceProfileBuilder.ComputeFingerprint(x.Chart));
        if (!unchanged) failures.Add("Input chart fingerprint changed.");
        if (rng != 0) failures.Add($"Research RNG calls must be zero, actual={rng}.");

        var orderedRows = rows.OrderBy(x => x.ChartId, StringComparer.Ordinal)
            .ThenBy(x => x.Family, StringComparer.Ordinal).ToImmutableArray();
        var orderedTransitions = transitions.OrderBy(x => x.ChartId, StringComparer.Ordinal)
            .ThenBy(x => x.OccurrenceId, StringComparer.Ordinal).ToImmutableArray();
        var criteria = BuildCriteria(orderedRows);
        var blocked = ImmutableArray.CreateBuilder<string>();
        ValidateRequiredScientificInputs(options, blocked, failures);
        var scientificInputsComplete = options.PrerequisitesComplete
            && options.RequiredHistoricalSnapshot is not null
            && options.RequiredHistoricalG1Cases is not null;
        if (scientificInputsComplete)
            failures.AddRange(ValidateHistoricalSnapshot(orderedRows, options.RequiredHistoricalSnapshot!));
        var historicalCases = ResolveHistoricalCases(orderedTransitions,
            scientificInputsComplete ? options.RequiredHistoricalG1Cases : null, failures);
        if (!RiceSentinelMatches(orderedRows)) failures.Add("RICE_SENTINEL_NON_INTERFERENCE_FAILURE");

        var outcome = Classify(failures.Count == 0, blocked.Count > 0,
            criteria.Single(x => x.Family == "RICE_HEAD_COMPLETION").Passed,
            criteria.Single(x => x.Family == "G1_INTERIOR_SPATIAL").Passed);
        var matrix = orderedTransitions.GroupBy(x => (x.HistoricalState, x.CorrectedState))
            .OrderBy(x => x.Key.HistoricalState, StringComparer.Ordinal)
            .ThenBy(x => x.Key.CorrectedState, StringComparer.Ordinal)
            .Select(x => new Lane0TransitionCount(x.Key.HistoricalState, x.Key.CorrectedState, x.Count()))
            .ToImmutableArray();
        return new(SchemaVersion, outcome, orderedRows, criteria, orderedTransitions, matrix,
            historicalCases, failures.Order(StringComparer.Ordinal).ToImmutableArray(),
            blocked.Order(StringComparer.Ordinal).ToImmutableArray(), rng, false, unchanged);
    }

    internal static Lane0G1BranchComparison CompareG1Branches(
        ImmutableArray<Lane0Occurrence> historical,
        ImmutableArray<Lane0Occurrence> corrected,
        IReadOnlySet<string>? historicalOperational = null,
        IReadOnlySet<string>? correctedOperational = null)
    {
        historicalOperational ??= new HashSet<string>(StringComparer.Ordinal);
        correctedOperational ??= new HashSet<string>(StringComparer.Ordinal);
        var failures = ImmutableArray.CreateBuilder<string>();
        var oldById = IndexUnique(historical, "historical", failures);
        var newById = IndexUnique(corrected, "corrected", failures);
        if (!oldById.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(newById.Keys))
            failures.Add("Historical/corrected occurrence ID sets differ.");
        var oldUnique = oldById.Values.OrderBy(x => x.OccurrenceId, StringComparer.Ordinal).ToImmutableArray();
        var newUnique = newById.Values.OrderBy(x => x.OccurrenceId, StringComparer.Ordinal).ToImmutableArray();
        var oldTrials = Lane0SpatialResearch.EvaluateAll(oldUnique, requireTemporalIntegrity: false)
            .ToDictionary(x => x.OccurrenceId, StringComparer.Ordinal);
        var newTrials = Lane0SpatialResearch.EvaluateAll(newUnique, requireTemporalIntegrity: true)
            .ToDictionary(x => x.OccurrenceId, StringComparer.Ordinal);
        var result = ImmutableArray.CreateBuilder<Lane0G1Transition>();
        foreach (var id in oldById.Keys.Intersect(newById.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var oldValue = oldById[id];
            var newValue = newById[id];
            if (!StructurallyEquivalent(oldValue, newValue))
                failures.Add($"{id}: structural drift outside AnchorTime/FutureHeldObservationIds.");
            var oldTrial = oldTrials[id];
            var newTrial = newTrials[id];
            var audit = newTrial.Leakage;
            result.Add(new(newValue.ChartId, newValue.KeyCount, id, newValue.GroupId,
                oldTrial.State.ToString(), newTrial.State.ToString(),
                oldTrial.IndependentSupported, newTrial.IndependentSupported,
                oldTrial.ComparableDonors, newTrial.ComparableDonors,
                oldTrial.MatchingJointDonors, newTrial.MatchingJointDonors,
                historicalOperational.Contains(id), correctedOperational.Contains(id),
                $"{oldTrial.State}->{newTrial.State}", audit.DonorsConsidered,
                audit.Eligible.Length, audit.TemporalExclusion, audit.FutureHeldIdentityExclusion,
                audit.BothTemporalAndIdentity, audit.UniqueFutureHeldExcluded, audit.UniqueExcluded));
            if (!audit.PartitionValid) failures.Add($"{id}: donor partition invalid.");
        }
        return new(result.ToImmutable(), failures.ToImmutable());
    }

    internal static string Classify(bool valid, bool blocked, bool ricePassed, bool g1Passed) =>
        !valid ? "INVALID" : blocked ? "BLOCKED"
        : ricePassed && g1Passed ? "FEASIBILITY_DEMONSTRATED" : "LIMITED_PARK";

    internal static bool ValidateRiceSentinel(Lane0PopulationSummary historical,
        Lane0PopulationSummary corrected) => historical == corrected;

    internal static ImmutableArray<Lane0HistoricalOperationalCase> ResolveHistoricalCases(
        ImmutableArray<Lane0G1Transition> transitions, Lane0HistoricalCaseRule? rule,
        ImmutableArray<string>.Builder failures)
    {
        var cases = transitions.Where(x => x.HistoricalOperational && x.HistoricalJointSupported)
            .OrderBy(x => x.ChartId, StringComparer.Ordinal).ThenBy(x => x.OccurrenceId, StringComparer.Ordinal)
            .Select(x => new Lane0HistoricalOperationalCase(x.ChartId, x.KeyCount, x.OccurrenceId,
                x.HistoricalState, x.CorrectedState, x.DonorsConsidered, x.Eligible,
                x.TemporalExclusion, x.FutureHeldIdentityExclusion, x.BothTemporalAndIdentity,
                x.UniqueFutureHeldExcluded, x.UniqueExcluded, x.CorrectedJointSupported)).ToImmutableArray();
        if (rule is null) return cases;
        if (cases.Length != rule.ExpectedCount)
            failures.Add($"Historical operational G1 case count mismatch: {cases.Length}/{rule.ExpectedCount}.");
        var distribution = cases.GroupBy(x => x.ChartId, StringComparer.Ordinal)
            .ToImmutableSortedDictionary(x => x.Key, x => x.Count(), StringComparer.Ordinal);
        if (!distribution.SequenceEqual(rule.ExpectedChartDistribution))
            failures.Add("Historical operational G1 chart distribution mismatch.");
        if (cases.Any(x => x.KeyCount != rule.ExpectedKeymode))
            failures.Add("Historical operational G1 keymode mismatch.");
        return cases;
    }

    internal static ImmutableSortedDictionary<string, byte[]> SerializeArtifacts(
        Lane0CorrectiveEvaluationResult result)
    {
        var artifacts = ImmutableSortedDictionary.CreateBuilder<string, byte[]>(StringComparer.Ordinal);
        artifacts["scientific_summary.json"] = JsonBytes(result);
        artifacts["chart_family_keymode_results.csv"] = CsvBytes(
            "chart_id,keymode,family,historical_structural,historical_holdouts,historical_with_context,historical_joint_unique,historical_joint_among_alternatives,historical_contradiction,historical_no_context,historical_marginal_only,historical_independent_supported,historical_operational,historical_operational_independent_supported,historical_integrity_failures,corrected_structural,corrected_holdouts,corrected_with_context,corrected_joint_unique,corrected_joint_among_alternatives,corrected_contradiction,corrected_no_context,corrected_marginal_only,corrected_independent_supported,corrected_operational,corrected_operational_independent_supported,corrected_integrity_failures",
            result.Charts.Select(x => string.Join(',', x.ChartId, x.KeyCount, x.Family,
                x.Historical.Structural, x.Historical.Holdouts, x.Historical.ComparableContext,
                x.Historical.JointUnique, x.Historical.JointAmongAlternatives, x.Historical.Contradiction,
                x.Historical.NoContext, x.Historical.MarginalOnly, x.Historical.JointSupport,
                x.Historical.Operational, x.Historical.OperationalSupported, x.Historical.IntegrityFailures,
                x.Corrected.Structural, x.Corrected.Holdouts, x.Corrected.ComparableContext,
                x.Corrected.JointUnique, x.Corrected.JointAmongAlternatives, x.Corrected.Contradiction,
                x.Corrected.NoContext, x.Corrected.MarginalOnly, x.Corrected.JointSupport,
                x.Corrected.Operational, x.Corrected.OperationalSupported, x.Corrected.IntegrityFailures)));
        artifacts["g1_state_transitions.csv"] = CsvBytes(
            "chart_id,keymode,occurrence_id,group_id,historical_state,corrected_state,historical_joint_supported,corrected_joint_supported,historical_comparable_donors,corrected_comparable_donors,historical_matching_joint_donors,corrected_matching_joint_donors,temporal_exclusion,future_held_identity_exclusion,both_temporal_and_identity,unique_future_held_excluded,unique_excluded,historical_operational,corrected_operational,transition_kind",
            result.G1Transitions.Select(x => string.Join(',', x.ChartId, x.KeyCount, x.OccurrenceId,
                x.GroupId, x.HistoricalState, x.CorrectedState, x.HistoricalJointSupported,
                x.CorrectedJointSupported, x.HistoricalComparableDonors, x.CorrectedComparableDonors,
                x.HistoricalMatchingJointDonors, x.CorrectedMatchingJointDonors, x.TemporalExclusion,
                x.FutureHeldIdentityExclusion, x.BothTemporalAndIdentity, x.UniqueFutureHeldExcluded,
                x.UniqueExcluded, x.HistoricalOperational, x.CorrectedOperational, x.TransitionKind)));
        artifacts["g1_historical_operational_cases.csv"] = CsvBytes(
            "chart_id,keymode,occurrence_id,historical_state,corrected_state,donors_considered,eligible,temporal_exclusion,future_held_identity_exclusion,both_temporal_and_identity,unique_future_held_excluded,unique_excluded,corrected_supported",
            result.HistoricalOperationalG1Cases.Select(x => string.Join(',', x.ChartId, x.KeyCount,
                x.OccurrenceId, x.HistoricalState, x.CorrectedState, x.DonorsConsidered, x.Eligible,
                x.TemporalExclusion, x.FutureHeldIdentityExclusion, x.BothTemporalAndIdentity,
                x.UniqueFutureHeldExcluded, x.UniqueExcluded, x.CorrectedSupported)));
        artifacts["integrity.json"] = JsonBytes(new
        {
            result.Outcome, result.IntegrityFailures, result.BlockedReasons, result.ResearchRngCalls,
            result.BehaviorChanged, result.InputsUnchanged
        });
        return artifacts.ToImmutable();
    }

    internal static void WriteArtifacts(Lane0CorrectiveEvaluationResult result, string outputDirectory)
    {
        var artifacts = SerializeArtifacts(result);
        var checksums = string.Join('\n', artifacts.Select(x =>
            $"{Convert.ToHexString(SHA256.HashData(x.Value))}  {x.Key}")) + "\n";
        if (Directory.Exists(outputDirectory)
            && Directory.EnumerateFileSystemEntries(outputDirectory).Any())
            throw new InvalidOperationException(
                "Output directory must be nonexistent or completely empty.");
        Directory.CreateDirectory(outputDirectory);
        foreach (var pair in artifacts) File.WriteAllBytes(Path.Combine(outputDirectory, pair.Key), pair.Value);
        File.WriteAllText(Path.Combine(outputDirectory, "sha256sums.txt"), checksums,
            new UTF8Encoding(false));
    }

    internal static void ValidateRequiredScientificInputs(Lane0CorrectiveEvaluationOptions options,
        ImmutableArray<string>.Builder blocked, ImmutableArray<string>.Builder failures)
    {
        if (!options.PrerequisitesComplete)
            blocked.Add("Required future publication/authorization inputs are absent.");
        if (options.RequiredHistoricalSnapshot is null)
            blocked.Add("Required frozen historical snapshot was not supplied.");
        if (options.RequiredHistoricalG1Cases is null)
            blocked.Add("Required frozen historical G1 case rule was not supplied.");

        if (!options.PrerequisitesComplete || options.RequiredHistoricalSnapshot is null
            || options.RequiredHistoricalG1Cases is null) return;
        if (options.RequiredHistoricalSnapshot != FrozenHistoricalSnapshot)
            failures.Add("Supplied frozen historical snapshot contradicts the canonical snapshot.");
        if (!HistoricalCaseRuleEquals(options.RequiredHistoricalG1Cases, FrozenHistoricalG1CaseRule))
            failures.Add("Supplied frozen historical G1 case rule contradicts the canonical rule.");
    }

    private static bool HistoricalCaseRuleEquals(Lane0HistoricalCaseRule actual,
        Lane0HistoricalCaseRule expected) => actual.ExpectedCount == expected.ExpectedCount
        && actual.ExpectedKeymode == expected.ExpectedKeymode
        && actual.ExpectedChartDistribution.SequenceEqual(expected.ExpectedChartDistribution);

    private static ImmutableArray<Lane0Occurrence> BuildFrozenRiceOccurrences(string chart,
        ChordCompletionResearchResult result) => result.ChordGroups.SelectMany(group => group.Members.Select(target =>
    {
        var visible = group.Members.Where(x => x.ObservationId != target.ObservationId).ToArray();
        var query = HeadSignature(result.KeyCount,
            visible.Where(x => x.HeadType == OriginalHeadMemberType.TapHead).Select(x => x.Lane),
            visible.Where(x => x.HeadType == OriginalHeadMemberType.LongNoteHead).Select(x => x.Lane));
        var spatial = $"L:{target.Lane}";
        var temporal = $"HT:{target.HeadType}";
        return new Lane0Occurrence(Lane0Family.RiceHeadCompletion, chart, result.KeyCount,
            $"{group.SourceGroupId}/{target.ObservationId}", group.SourceGroupId, group.SourceGroupId, null,
            query, $"{spatial}|{temporal}", temporal, spatial,
            group.HeadState.MemberObservationIds.Select(x => x.Value).ToImmutableArray(), [], []);
    })).OrderBy(x => x.OccurrenceId, StringComparer.Ordinal).ToImmutableArray();

    private static Dictionary<string, Lane0Occurrence> IndexUnique(ImmutableArray<Lane0Occurrence> values,
        string branch, ImmutableArray<string>.Builder failures)
    {
        var result = new Dictionary<string, Lane0Occurrence>(StringComparer.Ordinal);
        foreach (var value in values)
            if (!result.TryAdd(value.OccurrenceId, value)) failures.Add($"Duplicate {branch} occurrence: {value.OccurrenceId}.");
        return result;
    }

    private static bool StructurallyEquivalent(Lane0Occurrence x, Lane0Occurrence y) =>
        x.Family == y.Family && x.ChartId == y.ChartId && x.KeyCount == y.KeyCount
        && x.OccurrenceId == y.OccurrenceId && x.GroupId == y.GroupId && x.EventId == y.EventId
        && x.ParentId == y.ParentId && x.QuerySignature == y.QuerySignature
        && x.JointResultSignature == y.JointResultSignature
        && x.TemporalResultSignature == y.TemporalResultSignature
        && x.SpatialResultSignature == y.SpatialResultSignature
        && x.ObservationIds.SequenceEqual(y.ObservationIds)
        && x.ReleaseEndpointObservationIds.SequenceEqual(y.ReleaseEndpointObservationIds)
        && x.IsSynthetic == y.IsSynthetic && x.CompleteIdentity == y.CompleteIdentity
        && SequenceEqual(x.ComponentOccurrenceIds, y.ComponentOccurrenceIds);

    private static bool SequenceEqual<T>(ImmutableArray<T> x, ImmutableArray<T> y) =>
        x.IsDefault == y.IsDefault && (x.IsDefault || x.SequenceEqual(y));

    private static Lane0PopulationSummary Summarize(ImmutableArray<Lane0Occurrence> occurrences,
        ImmutableArray<Lane0Holdout> trials, IReadOnlySet<string> operational) => new(
        occurrences.Length, trials.Length, operational.Count,
        trials.Count(x => x.ComparableDonors > 0), trials.Count(x => x.IndependentSupported),
        trials.Count(x => x.State == Lane0HoldoutState.JointUnique),
        trials.Count(x => x.State == Lane0HoldoutState.JointAmongAlternatives),
        trials.Count(x => x.State == Lane0HoldoutState.Contradiction),
        trials.Count(x => x.State == Lane0HoldoutState.NoContext),
        trials.Count(x => x.TemporalMarginalSupported && x.SpatialMarginalSupported && !x.JointSupported),
        trials.Count(x => x.State == Lane0HoldoutState.AmbiguousIntegrity),
        trials.Count(x => x.IndependentSupported && operational.Contains(x.OccurrenceId)),
        trials.Sum(x => x.Leakage.IntegrityFailures)
            + trials.Count(x => x.State == Lane0HoldoutState.AmbiguousIntegrity));

    private static ImmutableArray<Lane0FamilyCriterion> BuildCriteria(
        ImmutableArray<Lane0CorrectiveChartResult> rows) =>
        new[] { "RICE_HEAD_COMPLETION", "G1_INTERIOR_SPATIAL" }.Select(family =>
        {
            var supported = rows.Where(x => x.Family == family && x.Corrected.JointSupport > 0).ToArray();
            var chartCount = supported.Select(x => x.ChartId).Distinct(StringComparer.Ordinal).Count();
            var keymodes = supported.Select(x => x.KeyCount).Distinct().Count();
            var operational = supported.Sum(x => x.Corrected.OperationalSupported);
            return new Lane0FamilyCriterion(family, chartCount, keymodes, operational,
                chartCount >= 2 && keymodes >= 2 && operational > 0
                    && rows.Where(x => x.Family == family).Sum(x => x.Corrected.IntegrityFailures) == 0);
        }).ToImmutableArray();

    private static ImmutableArray<string> ValidateHistoricalSnapshot(
        ImmutableArray<Lane0CorrectiveChartResult> rows,
        Lane0FrozenSnapshot expected)
    {
        var failures = ImmutableArray.CreateBuilder<string>();
        Validate("RICE_HEAD_COMPLETION", expected.Rice);
        Validate("G1_INTERIOR_SPATIAL", expected.G1);
        return failures.ToImmutable();

        void Validate(string family, Lane0FrozenFamilySnapshot frozen)
        {
            var selected = rows.Where(x => x.Family == family).ToArray();
            var actual = Sum(selected.Select(x => x.Historical));
            var historicallySupported = selected.Where(x => x.Historical.JointSupport > 0).ToArray();
            var supportedCharts = historicallySupported.Select(x => x.ChartId)
                .Distinct(StringComparer.Ordinal).Count();
            var supportedKeymodes = historicallySupported.Select(x => x.KeyCount).Distinct().Count();
            if (actual.Structural != frozen.Structural || actual.ComparableContext != frozen.ComparableContext
                || actual.JointSupport != frozen.JointSupport || actual.JointUnique != frozen.JointUnique
                || actual.JointAmongAlternatives != frozen.JointAmongAlternatives
                || actual.Contradiction != frozen.Contradiction || actual.NoContext != frozen.NoContext
                || actual.Operational != frozen.Operational
                || actual.OperationalSupported != frozen.OperationalSupported
                || supportedCharts != frozen.SupportedCharts
                || supportedKeymodes != frozen.SupportedKeymodes)
                failures.Add($"{family}: historical snapshot mismatch.");
        }
    }

    private static bool RiceSentinelMatches(ImmutableArray<Lane0CorrectiveChartResult> rows) =>
        rows.Where(x => x.Family == "RICE_HEAD_COMPLETION")
            .All(x => ValidateRiceSentinel(x.Historical, x.Corrected));

    private static Lane0PopulationSummary Sum(IEnumerable<Lane0PopulationSummary> values) => values.Aggregate(
        new Lane0PopulationSummary(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
        (a, x) => new(a.Structural + x.Structural, a.Holdouts + x.Holdouts,
            a.Operational + x.Operational, a.ComparableContext + x.ComparableContext,
            a.JointSupport + x.JointSupport, a.JointUnique + x.JointUnique,
            a.JointAmongAlternatives + x.JointAmongAlternatives, a.Contradiction + x.Contradiction,
            a.NoContext + x.NoContext, a.MarginalOnly + x.MarginalOnly,
            a.AmbiguousIntegrity + x.AmbiguousIntegrity,
            a.OperationalSupported + x.OperationalSupported,
            a.IntegrityFailures + x.IntegrityFailures));

    private static string HeadSignature(int keys, IEnumerable<int> taps, IEnumerable<int> lns) =>
        $"K:{keys}|T:{string.Join(',', taps.Order())}|L:{string.Join(',', lns.Order())}";

    private static byte[] JsonBytes(object value) => new UTF8Encoding(false).GetBytes(
        JsonSerializer.Serialize(value, Json).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n");

    private static byte[] CsvBytes(string header, IEnumerable<string> rows) => new UTF8Encoding(false)
        .GetBytes(string.Join('\n', new[] { header }.Concat(rows)) + "\n");
}
