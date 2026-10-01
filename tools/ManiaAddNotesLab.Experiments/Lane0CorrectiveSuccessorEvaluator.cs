using System.Collections.Immutable;
using ManiaAddNotesLab.Core;

/// <summary>
/// Successor science. It intentionally does not call the consumed historical evaluator entrypoint:
/// the only scientific remediation is the preregistered B-to-A historical-case reference change.
/// Pure branch-comparison helpers remain shared to avoid changing the frozen algorithm.
/// </summary>
internal sealed class Lane0CorrectiveSuccessorEvaluationRunner : ILane0SuccessorScientificEvaluator
{
    internal const string SchemaVersion = "lane-0-corrective-successor-evaluation.1";
    internal static readonly Lane0FrozenSnapshot FrozenHistoricalSnapshot = new(
        new(40360, 39596, 37080, 693, 36387, 2516, 764, 40360, 37080, 11, 3),
        new(16881, 9540, 3373, 192, 3181, 6167, 7341, 288, 11, 5, 2));
    internal static readonly Lane0HistoricalCaseRule CorrectedHistoricalCases = new(11,
        ImmutableSortedDictionary<string, int>.Empty.WithComparers(StringComparer.Ordinal)
            .Add(Lane0CorrectiveSuccessorReferenceValidator.SecondHistoricalIdentity, 9)
            .Add(Lane0CorrectiveSuccessorReferenceValidator.CorrectedSpringIdentity, 2), 7);

    public Lane0SuccessorEvaluationPass Evaluate(
        ImmutableArray<Lane0CorrectiveChartInput> inputs,
        Lane0SuccessorEvaluationContext context)
    {
        var result = EvaluateScience(inputs);
        return new(result.Outcome, Lane0CorrectiveSuccessorPackage.Build(result, context),
            result.IntegrityFailures, result.BlockedReasons, result.ResearchRngCalls,
            result.InputsUnchanged, result.BehaviorChanged, false);
    }

    internal static Lane0CorrectiveEvaluationResult EvaluateScience(
        IEnumerable<Lane0CorrectiveChartInput> inputs)
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
            var rice = BuildFrozenRiceOccurrences(input.ChartId,
                ChordCompletionResearch.Evaluate(input.Chart));
            var riceTrials = Lane0SpatialResearch.EvaluateAll(rice);
            var riceSummary = Summarize(rice, riceTrials,
                rice.Select(x => x.OccurrenceId).ToHashSet(StringComparer.Ordinal));
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

            var comparison = Lane0CorrectiveEvaluationRunner.CompareG1Branches(historical, corrected,
                historicalOperational, correctedOperational);
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

        var unchanged = fingerprints.All(x =>
            x.Before == MapperEvidenceProfileBuilder.ComputeFingerprint(x.Chart));
        if (!unchanged) failures.Add("Input chart fingerprint changed.");
        if (rng != 0) failures.Add($"Research RNG calls must be zero, actual={rng}.");
        var orderedRows = rows.OrderBy(x => x.ChartId, StringComparer.Ordinal)
            .ThenBy(x => x.Family, StringComparer.Ordinal).ToImmutableArray();
        var orderedTransitions = transitions.OrderBy(x => x.ChartId, StringComparer.Ordinal)
            .ThenBy(x => x.OccurrenceId, StringComparer.Ordinal).ToImmutableArray();
        var criteria = BuildCriteria(orderedRows);
        failures.AddRange(ValidateHistoricalSnapshot(orderedRows, FrozenHistoricalSnapshot));
        var cases = Lane0CorrectiveEvaluationRunner.ResolveHistoricalCases(orderedTransitions,
            CorrectedHistoricalCases, failures);
        if (orderedRows.Where(x => x.Family == "RICE_HEAD_COMPLETION")
            .Any(x => !Lane0CorrectiveEvaluationRunner.ValidateRiceSentinel(
                x.Historical, x.Corrected)))
            failures.Add("RICE_SENTINEL_NON_INTERFERENCE_FAILURE");
        var blocked = ImmutableArray<string>.Empty;
        var outcome = Lane0CorrectiveEvaluationRunner.Classify(failures.Count == 0, false,
            criteria.Single(x => x.Family == "RICE_HEAD_COMPLETION").Passed,
            criteria.Single(x => x.Family == "G1_INTERIOR_SPATIAL").Passed);
        var matrix = orderedTransitions.GroupBy(x => (x.HistoricalState, x.CorrectedState))
            .OrderBy(x => x.Key.HistoricalState, StringComparer.Ordinal)
            .ThenBy(x => x.Key.CorrectedState, StringComparer.Ordinal)
            .Select(x => new Lane0TransitionCount(x.Key.HistoricalState,
                x.Key.CorrectedState, x.Count())).ToImmutableArray();
        return new(SchemaVersion, outcome, orderedRows, criteria, orderedTransitions, matrix,
            cases, failures.Order(StringComparer.Ordinal).ToImmutableArray(), blocked, rng, false, unchanged);
    }

    private static ImmutableArray<Lane0Occurrence> BuildFrozenRiceOccurrences(string chart,
        ChordCompletionResearchResult result) => result.ChordGroups.SelectMany(group =>
        group.Members.Select(target =>
        {
            var visible = group.Members.Where(x => x.ObservationId != target.ObservationId).ToArray();
            var query = HeadSignature(result.KeyCount,
                visible.Where(x => x.HeadType == OriginalHeadMemberType.TapHead).Select(x => x.Lane),
                visible.Where(x => x.HeadType == OriginalHeadMemberType.LongNoteHead).Select(x => x.Lane));
            var spatial = $"L:{target.Lane}";
            var temporal = $"HT:{target.HeadType}";
            return new Lane0Occurrence(Lane0Family.RiceHeadCompletion, chart, result.KeyCount,
                $"{group.SourceGroupId}/{target.ObservationId}", group.SourceGroupId,
                group.SourceGroupId, null, query, $"{spatial}|{temporal}", temporal, spatial,
                group.HeadState.MemberObservationIds.Select(x => x.Value).ToImmutableArray(), [], []);
        })).OrderBy(x => x.OccurrenceId, StringComparer.Ordinal).ToImmutableArray();

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
                    && rows.Where(x => x.Family == family)
                        .Sum(x => x.Corrected.IntegrityFailures) == 0);
        }).ToImmutableArray();

    private static ImmutableArray<string> ValidateHistoricalSnapshot(
        ImmutableArray<Lane0CorrectiveChartResult> rows, Lane0FrozenSnapshot expected)
    {
        var failures = ImmutableArray.CreateBuilder<string>();
        Validate("RICE_HEAD_COMPLETION", expected.Rice);
        Validate("G1_INTERIOR_SPATIAL", expected.G1);
        return failures.ToImmutable();
        void Validate(string family, Lane0FrozenFamilySnapshot frozen)
        {
            var selected = rows.Where(x => x.Family == family).ToArray();
            var actual = Sum(selected.Select(x => x.Historical));
            var supported = selected.Where(x => x.Historical.JointSupport > 0).ToArray();
            if (actual.Structural != frozen.Structural
                || actual.ComparableContext != frozen.ComparableContext
                || actual.JointSupport != frozen.JointSupport
                || actual.JointUnique != frozen.JointUnique
                || actual.JointAmongAlternatives != frozen.JointAmongAlternatives
                || actual.Contradiction != frozen.Contradiction || actual.NoContext != frozen.NoContext
                || actual.Operational != frozen.Operational
                || actual.OperationalSupported != frozen.OperationalSupported
                || supported.Select(x => x.ChartId).Distinct(StringComparer.Ordinal).Count()
                    != frozen.SupportedCharts
                || supported.Select(x => x.KeyCount).Distinct().Count() != frozen.SupportedKeymodes)
                failures.Add($"{family}: historical snapshot mismatch.");
        }
    }

    private static Lane0PopulationSummary Sum(IEnumerable<Lane0PopulationSummary> values) =>
        values.Aggregate(new Lane0PopulationSummary(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            (a, x) => new(a.Structural + x.Structural, a.Holdouts + x.Holdouts,
                a.Operational + x.Operational, a.ComparableContext + x.ComparableContext,
                a.JointSupport + x.JointSupport, a.JointUnique + x.JointUnique,
                a.JointAmongAlternatives + x.JointAmongAlternatives,
                a.Contradiction + x.Contradiction, a.NoContext + x.NoContext,
                a.MarginalOnly + x.MarginalOnly, a.AmbiguousIntegrity + x.AmbiguousIntegrity,
                a.OperationalSupported + x.OperationalSupported,
                a.IntegrityFailures + x.IntegrityFailures));

    private static string HeadSignature(int keys, IEnumerable<int> taps, IEnumerable<int> lns) =>
        $"K:{keys}|T:{string.Join(',', taps.Order())}|L:{string.Join(',', lns.Order())}";
}
