using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ManiaAddNotesLab.Core;

public enum AdaptiveFeatureProvenance { ObservedFileFact, ExactFileDerived, ResearchDerived }
public enum AdaptiveMethodDisposition { Implement, RejectBeforeExperiment, Defer }
public enum AdaptiveContextMethodKind { GlobalChart, LegacyFixedWindowReference, ExactNeighborRecurrence }
public enum AdaptiveReconstructionState { Reconstructed, Ambiguous, Mismatch, NoContext }
public enum AdaptiveSegmentationState { ProposedRegions, GlobalOnly, InsufficientStructure, NoStableBoundary }
public enum AdaptiveParameterKind
{
    FormatInvariant, ImplementationOnly, ResearchHypothesis, ExternalConvention, MapperDerivedCandidate
}

public sealed record AdaptiveMethodIdentity(
    string MethodId,
    string MethodVersion,
    string RepresentationId,
    string ParameterSetId);

public sealed record AdaptiveParameterDeclaration(
    string Name,
    string ExactValue,
    AdaptiveParameterKind Kind,
    string Rationale);

public sealed record AdaptiveMethodDeclaration(
    AdaptiveMethodIdentity Identity,
    AdaptiveMethodDisposition Disposition,
    string Question,
    string Assumptions,
    string Complexity,
    string FailureModes,
    ImmutableArray<AdaptiveParameterDeclaration> Parameters);

public sealed record StructuralEventToken(
    string Identity,
    ImmutableArray<int> TapHeadLanes,
    ImmutableArray<int> LongNoteHeadLanes,
    ImmutableArray<int> LongNoteReleaseLanes,
    ImmutableArray<int> HeldBeforeLanes,
    int HeadCount,
    int ReleaseCount,
    bool MixedHeadTypes);

public sealed record OriginalTemporalEvent(
    string EventId,
    string ChartFingerprint,
    int SerializedTime,
    decimal FileDerivedBeat,
    int? PreviousSpacingMilliseconds,
    decimal? PreviousSpacingBeats,
    ImmutableArray<OriginalObservationId> HeadObservationIds,
    ImmutableArray<OriginalObservationId> ReleaseObservationIds,
    StructuralEventToken Token)
{
    public ImmutableArray<OriginalObservationId> AllObservationIds => HeadObservationIds
        .AddRange(ReleaseObservationIds).Distinct().OrderBy(x => x.Value).ToImmutableArray();
}

public sealed record OriginalTemporalEventSeries(
    string ResearchSchemaVersion,
    string ChartFingerprint,
    int KeyCount,
    int OriginalObjectCount,
    ImmutableArray<OriginalTemporalEvent> Events,
    int SyntheticTeachingCount,
    int CrossChartEvidenceCount);

public sealed record ExactRecurrenceOccurrence(
    string EventId,
    int StartIndex,
    int SerializedTime,
    decimal FileDerivedBeat,
    ImmutableArray<OriginalObservationId> ObservationIds);

public sealed record RecurrenceRelation(
    string ChartFingerprint,
    AdaptiveMethodIdentity Method,
    string ExactBlockIdentity,
    int BlockLength,
    ExactRecurrenceOccurrence Left,
    ExactRecurrenceOccurrence Right);

public sealed record BoundaryCandidate(
    string ChartFingerprint,
    AdaptiveMethodIdentity Method,
    string BoundaryEventId,
    int SerializedTime,
    decimal FileDerivedBeat,
    string LeftTokenIdentity,
    string RightTokenIdentity,
    int LeftRunLength,
    int RightRunLength,
    ImmutableArray<OriginalObservationId> ProvenanceObservationIds);

public sealed record ContextRegionCandidate(
    string ChartFingerprint,
    AdaptiveMethodIdentity Method,
    string StartEventId,
    string EndEventId,
    ImmutableArray<string> EventIds,
    ImmutableArray<OriginalObservationId> ProvenanceObservationIds);

public sealed record SegmentationProposal(
    AdaptiveSegmentationState State,
    ImmutableArray<BoundaryCandidate> Boundaries,
    ImmutableArray<ContextRegionCandidate> Regions);

public sealed record ValidationBlock(
    string ChartFingerprint,
    AdaptiveMethodIdentity Method,
    string TargetEventId,
    string TargetTokenIdentity,
    ImmutableArray<OriginalObservationId> ExcludedObservationIds,
    ImmutableArray<string> DonorEventIds,
    ImmutableArray<OriginalObservationId> DonorObservationIds,
    ImmutableArray<string> CandidateTokenIdentities,
    AdaptiveReconstructionState State,
    int LeakageCount);

public sealed record AdaptiveHeldOutSummary(
    AdaptiveMethodIdentity Method,
    int Eligible,
    int Comparable,
    int Reconstructed,
    int Ambiguous,
    int Mismatch,
    int NoContext,
    int LeakageCount,
    ImmutableArray<ValidationBlock> Blocks);

public sealed record BoundaryStabilityResult(
    AdaptiveMethodIdentity Method,
    ImmutableArray<OriginalObservationId> ExcludedObservationIds,
    int BaselineBoundaryCount,
    int PerturbedBoundaryCount,
    int SharedExactBoundaryCount,
    int BoundaryChanges,
    int LeakageCount);

public sealed record SyntheticBoundaryEvaluation(
    int TruthEligible,
    int Recovered,
    int FalsePositive,
    int FalseNegative);

public sealed record SyntheticRecurrenceEvaluation(
    int TruthEligible,
    int Recovered,
    int FalseMerge,
    int FalseSplit);

public sealed record AdaptiveContextResearchResult(
    string ResearchSchemaVersion,
    string ChartFingerprint,
    int KeyCount,
    OriginalTemporalEventSeries EventSeries,
    ImmutableArray<AdaptiveMethodDeclaration> MethodCatalog,
    ImmutableArray<RecurrenceRelation> Recurrences,
    ImmutableArray<SegmentationProposal> Segmentations,
    ImmutableArray<AdaptiveHeldOutSummary> HeldOut,
    ImmutableArray<BoundaryStabilityResult> Stability,
    int SyntheticTeachingCount,
    int CrossChartEvidenceCount);

/// <summary>
/// Phase E research-only projection. It reads immutable original evidence and cannot select candidates,
/// lanes, chords, LNs, articulation, generation scope, or consume RNG.
/// </summary>
public static class AdaptiveContextResearch
{
    public const string ResearchSchemaVersion = "phase-e-adaptive-context-shadow.1";
    public const string RepresentationId = "original-temporal-event-series.1";

    public static ImmutableArray<AdaptiveMethodDeclaration> PreHumanMethodCatalog =>
    [
        Method("global-chart.1", "none", AdaptiveMethodDisposition.Implement,
            "Reference reconstruction from every non-excluded event in the chart.",
            "No segmentation and no claim of structural truth.", "O(N)",
            "Usually ambiguous; chart-wide frequency is not authority."),
        Method("legacy-fixed-window.1", "beats-4", AdaptiveMethodDisposition.Implement,
            "Historical ±4 beat contextual reference.",
            "Four beats is an external legacy convention, never truth.", "O(N log N + D)",
            "Window edges are arbitrary and can mix or split structure.",
            new AdaptiveParameterDeclaration("windowBeats", "4", AdaptiveParameterKind.ExternalConvention,
                "Frozen historical reference; not tuned on Phase E results.")),
        Method("exact-neighbor-recurrence.1", "none", AdaptiveMethodDisposition.Implement,
            "Reconstruct an exact categorical event token from exact previous/next token context.",
            "Exact equality is intentionally narrow; target and its group are excluded from donors.",
            "O(N + D)", "May abstain frequently and cannot recover fuzzy motif identity."),
        Method("exact-block-recurrence.1", "block-2", AdaptiveMethodDisposition.Implement,
            "Represent non-contiguous recurrence of exact event-token sequences.",
            "Block length is an explicit research hypothesis; recurrence is not segmentation.",
            "O(N + R)", "Exact identity may have low human coverage.",
            new AdaptiveParameterDeclaration("blockLength", "2", AdaptiveParameterKind.ResearchHypothesis,
                "Smallest sequence that distinguishes an event from a transition-like block.")),
        Method("stable-run-boundary.1", "min-run-2", AdaptiveMethodDisposition.Implement,
            "Propose an exact event-anchored boundary between two stable token runs.",
            "A boundary is inferred, not mapper-authored section truth.", "O(N)",
            "Sensitive to minimum run length and blind to gradual change.",
            new AdaptiveParameterDeclaration("minimumStableRunEvents", "2", AdaptiveParameterKind.ResearchHypothesis,
                "Reject isolated one-event outliers as regimes.")),
        Method("stable-run-boundary.1", "min-run-3", AdaptiveMethodDisposition.Implement,
            "Sensitivity configuration for stable token runs.",
            "No configuration is selected as a winner.", "O(N)",
            "Higher abstention and fewer proposed boundaries.",
            new AdaptiveParameterDeclaration("minimumStableRunEvents", "3", AdaptiveParameterKind.ResearchHypothesis,
                "Predeclared sensitivity perturbation of min-run-2.")),
        Method("fuzzy-self-similarity", "not-applicable", AdaptiveMethodDisposition.RejectBeforeExperiment,
            "Approximate recurrence.", "Would require an unjustified similarity metric and threshold.",
            "Potentially O(N²)", "Can manufacture recurrence and hide exact false merges."),
        Method("pelt-crops-change-point", "not-applicable", AdaptiveMethodDisposition.Defer,
            "General change-point optimization.",
            "Penalty/objective preferences are not justified by mapper evidence in this phase.",
            "Method-dependent", "A popular algorithm could become a magic segmenter."),
        Method("hybrid-recurrence-boundaries", "not-applicable", AdaptiveMethodDisposition.Defer,
            "Combine recurrence and segmentation.",
            "The two component questions must first be measured independently.",
            "Method-dependent", "Combination could conceal a failure in either component.")
    ];

    public static AdaptiveContextResearchResult Evaluate(ManiaChart chart,
        bool includeBaselineValidationBlocks = true)
    {
        var series = BuildEventSeries(chart);
        var recurrenceMethod = Identity("exact-block-recurrence.1", "block-2");
        var recurrences = FindExactRecurrences(series, 2, recurrenceMethod);
        var segmentationMethods = new[]
        {
            (Minimum: 2, Identity: Identity("stable-run-boundary.1", "min-run-2")),
            (Minimum: 3, Identity: Identity("stable-run-boundary.1", "min-run-3"))
        };
        var segmentations = segmentationMethods.Select(x => InferStableRunBoundaries(series, x.Minimum, x.Identity))
            .ToImmutableArray();
        var heldOut = new[]
        {
            EvaluateHeldOut(series, AdaptiveContextMethodKind.GlobalChart,
                includeDetailedBlocks: includeBaselineValidationBlocks),
            EvaluateHeldOut(series, AdaptiveContextMethodKind.LegacyFixedWindowReference, 4m,
                includeBaselineValidationBlocks),
            EvaluateHeldOut(series, AdaptiveContextMethodKind.ExactNeighborRecurrence)
        }.ToImmutableArray();
        var stability = segmentationMethods.SelectMany(method => StabilitySamples(series, method.Minimum,
                method.Identity, 16))
            .ToImmutableArray();
        return new AdaptiveContextResearchResult(ResearchSchemaVersion, series.ChartFingerprint, series.KeyCount,
            series, PreHumanMethodCatalog, recurrences, segmentations, heldOut, stability,
            series.SyntheticTeachingCount, series.CrossChartEvidenceCount);
    }

    public static OriginalTemporalEventSeries BuildEventSeries(ManiaChart chart)
    {
        ArgumentNullException.ThrowIfNull(chart);
        var canonicalOriginals = chart.OriginalObjects
            .OrderBy(x => x.StartTime).ThenBy(x => x.EndTime ?? x.StartTime).ThenBy(x => x.Lane)
            .ThenBy(x => x.Type).ThenBy(x => x.Sequence).ToArray();
        var canonical = new ManiaChart
        {
            KeyCount = chart.KeyCount,
            Lines = chart.Lines,
            OriginalObjects = canonicalOriginals,
            TimingPoints = chart.TimingPoints.OrderBy(x => x.Time).ThenBy(x => x.BeatLength).ToArray()
        };
        var profile = MapperEvidenceProfileBuilder.Build(canonical);
        var moments = profile.Observations.Select(x => (x.StartTime, x.StartBeat))
            .Concat(profile.Observations.Where(x => x.Type == ManiaObjectType.LongNote)
                .Select(x => (x.EndTime!.Value, x.EndBeat)))
            .Distinct().OrderBy(x => x.Item1).ThenBy(x => x.Item2).ToArray();
        var events = ImmutableArray.CreateBuilder<OriginalTemporalEvent>();
        for (var index = 0; index < moments.Length; index++)
        {
            var (time, beat) = moments[index];
            var heads = profile.Observations.Where(x => x.StartTime == time && x.StartBeat == beat)
                .OrderBy(x => x.Id.Value).ToArray();
            var releases = profile.Observations.Where(x => x.Type == ManiaObjectType.LongNote
                    && x.EndTime == time && x.EndBeat == beat)
                .OrderBy(x => x.Id.Value).ToArray();
            var held = profile.Observations.Where(x => x.Type == ManiaObjectType.LongNote
                    && x.StartBeat < beat && x.EndBeat > beat)
                .Select(x => x.Lane).Distinct().Order().ToImmutableArray();
            var taps = heads.Where(x => x.Type == ManiaObjectType.Tap).Select(x => x.Lane).Distinct().Order()
                .ToImmutableArray();
            var lnHeads = heads.Where(x => x.Type == ManiaObjectType.LongNote).Select(x => x.Lane).Distinct().Order()
                .ToImmutableArray();
            var releaseLanes = releases.Select(x => x.Lane).Distinct().Order().ToImmutableArray();
            var tokenIdentity = $"T[{Lanes(taps)}]|L[{Lanes(lnHeads)}]|R[{Lanes(releaseLanes)}]|H[{Lanes(held)}]";
            var token = new StructuralEventToken(tokenIdentity, taps, lnHeads, releaseLanes, held,
                taps.Length + lnHeads.Length, releaseLanes.Length, taps.Length > 0 && lnHeads.Length > 0);
            var previous = index == 0 ? ((int Time, decimal Beat)?)null : moments[index - 1];
            events.Add(new OriginalTemporalEvent($"E{index:D8}", profile.ChartFingerprint, time, beat,
                previous is null ? null : time - previous.Value.Time,
                previous is null ? null : beat - previous.Value.Beat,
                heads.Select(x => x.Id).ToImmutableArray(), releases.Select(x => x.Id).ToImmutableArray(), token));
        }
        return new OriginalTemporalEventSeries(ResearchSchemaVersion, profile.ChartFingerprint, chart.KeyCount,
            profile.OriginalObjectCount, events.ToImmutable(), 0, 0);
    }

    public static ImmutableArray<RecurrenceRelation> FindExactRecurrences(OriginalTemporalEventSeries series,
        int blockLength, AdaptiveMethodIdentity? method = null)
    {
        ArgumentNullException.ThrowIfNull(series);
        if (blockLength < 1) throw new ArgumentOutOfRangeException(nameof(blockLength));
        method ??= Identity("exact-block-recurrence.1", $"block-{blockLength}");
        if (series.Events.Length < blockLength) return [];
        var occurrences = Enumerable.Range(0, series.Events.Length - blockLength + 1)
            .Select(start => new
            {
                Identity = string.Join("=>", series.Events.Skip(start).Take(blockLength).Select(x => x.Token.Identity)),
                Occurrence = Occurrence(series.Events, start, blockLength)
            });
        return occurrences.GroupBy(x => x.Identity, StringComparer.Ordinal).Where(x => x.Count() > 1)
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .SelectMany(group => group.OrderBy(x => x.Occurrence.StartIndex).Zip(
                group.OrderBy(x => x.Occurrence.StartIndex).Skip(1),
                (left, right) => new RecurrenceRelation(series.ChartFingerprint, method, group.Key, blockLength,
                    left.Occurrence, right.Occurrence)))
            .ToImmutableArray();
    }

    public static SegmentationProposal InferStableRunBoundaries(OriginalTemporalEventSeries series,
        int minimumStableRunEvents, AdaptiveMethodIdentity? method = null)
    {
        ArgumentNullException.ThrowIfNull(series);
        if (minimumStableRunEvents < 1) throw new ArgumentOutOfRangeException(nameof(minimumStableRunEvents));
        method ??= Identity("stable-run-boundary.1", $"min-run-{minimumStableRunEvents}");
        if (series.Events.Length < minimumStableRunEvents * 2)
            return new(AdaptiveSegmentationState.InsufficientStructure, [], []);
        var runs = Runs(series.Events);
        var boundaries = ImmutableArray.CreateBuilder<BoundaryCandidate>();
        for (var i = 1; i < runs.Length; i++)
        {
            var left = runs[i - 1];
            var right = runs[i];
            if (left.Length < minimumStableRunEvents || right.Length < minimumStableRunEvents) continue;
            var anchor = series.Events[right.Start];
            var provenance = series.Events.Skip(left.Start).Take(left.Length + right.Length)
                .SelectMany(x => x.AllObservationIds).Distinct().OrderBy(x => x.Value).ToImmutableArray();
            boundaries.Add(new BoundaryCandidate(series.ChartFingerprint, method, anchor.EventId,
                anchor.SerializedTime, anchor.FileDerivedBeat, left.Identity, right.Identity,
                left.Length, right.Length, provenance));
        }
        if (boundaries.Count == 0)
            return new(runs.Length == 1 ? AdaptiveSegmentationState.GlobalOnly
                : AdaptiveSegmentationState.NoStableBoundary, [], []);
        var boundaryIndexes = boundaries.Select(x => Array.FindIndex(series.Events.ToArray(),
                e => e.EventId == x.BoundaryEventId))
            .ToArray();
        var starts = new[] { 0 }.Concat(boundaryIndexes).ToArray();
        var ends = boundaryIndexes.Select(x => x - 1).Append(series.Events.Length - 1).ToArray();
        var regions = starts.Zip(ends, (start, end) =>
        {
            var selected = series.Events.Skip(start).Take(end - start + 1).ToArray();
            return new ContextRegionCandidate(series.ChartFingerprint, method, selected[0].EventId,
                selected[^1].EventId, selected.Select(x => x.EventId).ToImmutableArray(),
                selected.SelectMany(x => x.AllObservationIds).Distinct().OrderBy(x => x.Value).ToImmutableArray());
        }).ToImmutableArray();
        return new(AdaptiveSegmentationState.ProposedRegions, boundaries.ToImmutable(), regions);
    }

    public static AdaptiveHeldOutSummary EvaluateHeldOut(OriginalTemporalEventSeries series,
        AdaptiveContextMethodKind kind, decimal fixedWindowBeats = 4m,
        bool includeDetailedBlocks = true)
    {
        ArgumentNullException.ThrowIfNull(series);
        var method = kind switch
        {
            AdaptiveContextMethodKind.GlobalChart => Identity("global-chart.1", "none"),
            AdaptiveContextMethodKind.LegacyFixedWindowReference => Identity("legacy-fixed-window.1", "beats-4"),
            _ => Identity("exact-neighbor-recurrence.1", "none")
        };
        var globalGroups = series.Events.GroupBy(x => x.Token.Identity, StringComparer.Ordinal)
            .OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => x.ToArray()).ToArray();
        var neighborIndex = Enumerable.Range(1, Math.Max(0, series.Events.Length - 2))
            .GroupBy(i => (Previous: series.Events[i - 1].Token.Identity,
                Next: series.Events[i + 1].Token.Identity))
            .ToDictionary(x => x.Key, x => x.ToArray());
        var blocks = ImmutableArray.CreateBuilder<ValidationBlock>();
        var eligible = 0;
        var comparable = 0;
        var reconstructed = 0;
        var ambiguous = 0;
        var mismatch = 0;
        var noContext = 0;
        var totalLeakage = 0;
        for (var targetIndex = 0; targetIndex < series.Events.Length; targetIndex++)
        {
            if (kind == AdaptiveContextMethodKind.ExactNeighborRecurrence
                && (targetIndex == 0 || targetIndex == series.Events.Length - 1)) continue;
            eligible++;
            var target = series.Events[targetIndex];
            var excluded = target.AllObservationIds;
            IEnumerable<Donor> rawDonors = kind switch
            {
                AdaptiveContextMethodKind.GlobalChart => globalGroups.Select(group => group
                    .Select(x => new Donor(x, x.AllObservationIds))
                    .FirstOrDefault(x => !x.Provenance.Any(excluded.Contains))).Where(x => x is not null)
                    .Select(x => x!),
                AdaptiveContextMethodKind.LegacyFixedWindowReference => EventsInBeatWindow(series.Events,
                        target.FileDerivedBeat - fixedWindowBeats, target.FileDerivedBeat + fixedWindowBeats)
                    .Select(x => new Donor(x, x.AllObservationIds)),
                _ => neighborIndex.GetValueOrDefault((series.Events[targetIndex - 1].Token.Identity,
                        series.Events[targetIndex + 1].Token.Identity), [])
                    .Select(i => new Donor(series.Events[i], series.Events.Skip(i - 1).Take(3)
                        .SelectMany(x => x.AllObservationIds).Distinct().OrderBy(x => x.Value).ToImmutableArray()))
            };
            var donors = rawDonors.Where(x => !x.Provenance.Any(excluded.Contains))
                .GroupBy(x => x.Center.Token.Identity, StringComparer.Ordinal)
                .Select(x => x.OrderBy(d => d.Center.EventId, StringComparer.Ordinal).First()).ToArray();
            var candidateTokens = donors.Select(x => x.Center.Token.Identity).Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal).ToImmutableArray();
            var state = Classify(target.Token.Identity, candidateTokens);
            var donorObservations = donors.SelectMany(x => x.Provenance).Distinct().OrderBy(x => x.Value)
                .ToImmutableArray();
            var leakage = donorObservations.Count(excluded.Contains);
            if (state != AdaptiveReconstructionState.NoContext) comparable++;
            if (state == AdaptiveReconstructionState.Reconstructed) reconstructed++;
            if (state == AdaptiveReconstructionState.Ambiguous) ambiguous++;
            if (state == AdaptiveReconstructionState.Mismatch) mismatch++;
            if (state == AdaptiveReconstructionState.NoContext) noContext++;
            totalLeakage += leakage;
            if (includeDetailedBlocks)
                blocks.Add(new ValidationBlock(series.ChartFingerprint, method, target.EventId, target.Token.Identity,
                    excluded, donors.Select(x => x.Center.EventId).Distinct().Order(StringComparer.Ordinal)
                        .ToImmutableArray(), donorObservations, candidateTokens, state, leakage));
        }
        return new AdaptiveHeldOutSummary(method, eligible, comparable, reconstructed, ambiguous, mismatch,
            noContext, totalLeakage, blocks.ToImmutable());
    }

    public static int AuditLeakage(IEnumerable<OriginalObservationId> excluded,
        IEnumerable<OriginalObservationId> donorProvenance)
    {
        var exclusion = excluded.ToImmutableHashSet();
        return donorProvenance.Distinct().Count(exclusion.Contains);
    }

    public static SyntheticBoundaryEvaluation EvaluateSyntheticBoundaries(
        IEnumerable<string> expectedBoundaryEventIds, IEnumerable<string> proposedBoundaryEventIds)
    {
        var expected = expectedBoundaryEventIds.ToImmutableHashSet(StringComparer.Ordinal);
        var proposed = proposedBoundaryEventIds.ToImmutableHashSet(StringComparer.Ordinal);
        var recovered = expected.Intersect(proposed).Count();
        return new(expected.Count, recovered, proposed.Except(expected).Count(), expected.Count - recovered);
    }

    public static SyntheticRecurrenceEvaluation EvaluateSyntheticRecurrences(
        IEnumerable<string> expectedExactBlockIdentities, IEnumerable<string> proposedExactBlockIdentities)
    {
        var expected = expectedExactBlockIdentities.ToImmutableHashSet(StringComparer.Ordinal);
        var proposed = proposedExactBlockIdentities.ToImmutableHashSet(StringComparer.Ordinal);
        var recovered = expected.Intersect(proposed).Count();
        return new(expected.Count, recovered, proposed.Except(expected).Count(), expected.Count - recovered);
    }

    public static BoundaryStabilityResult EvaluateBoundaryStability(OriginalTemporalEventSeries series,
        IEnumerable<string> excludedEventIds, int minimumStableRunEvents,
        AdaptiveMethodIdentity? method = null)
    {
        method ??= Identity("stable-run-boundary.1", $"min-run-{minimumStableRunEvents}");
        var excludedIds = excludedEventIds.ToImmutableHashSet(StringComparer.Ordinal);
        var excludedObservations = series.Events.Where(x => excludedIds.Contains(x.EventId))
            .SelectMany(x => x.AllObservationIds).Distinct().OrderBy(x => x.Value).ToImmutableArray();
        var removedEventIds = series.Events.Where(x => x.AllObservationIds.Any(excludedObservations.Contains))
            .Select(x => x.EventId).ToImmutableHashSet(StringComparer.Ordinal);
        var filtered = series with
        {
            Events = series.Events.Where(x => !removedEventIds.Contains(x.EventId)).ToImmutableArray()
        };
        var baseline = InferStableRunBoundaries(series, minimumStableRunEvents, method).Boundaries;
        var perturbed = InferStableRunBoundaries(filtered, minimumStableRunEvents, method).Boundaries;
        var baselineIds = baseline.Select(x => x.BoundaryEventId).Where(x => !removedEventIds.Contains(x))
            .ToImmutableHashSet(StringComparer.Ordinal);
        var perturbedIds = perturbed.Select(x => x.BoundaryEventId).ToImmutableHashSet(StringComparer.Ordinal);
        var shared = baselineIds.Intersect(perturbedIds).Count();
        return new BoundaryStabilityResult(method, excludedObservations, baselineIds.Count, perturbedIds.Count,
            shared, baselineIds.Count + perturbedIds.Count - 2 * shared,
            perturbed.SelectMany(x => x.ProvenanceObservationIds).Distinct().Count(excludedObservations.Contains));
    }

    private static IEnumerable<BoundaryStabilityResult> StabilitySamples(OriginalTemporalEventSeries series,
        int minimum, AdaptiveMethodIdentity method, int maximumSamples)
    {
        if (series.Events.IsEmpty) yield break;
        var indexes = Enumerable.Range(0, Math.Min(maximumSamples, series.Events.Length))
            .Select(i => i * series.Events.Length / Math.Min(maximumSamples, series.Events.Length)).Distinct();
        foreach (var index in indexes)
            yield return EvaluateBoundaryStability(series, [series.Events[index].EventId], minimum, method);
        if (series.Events.Length >= 3)
        {
            var middle = series.Events.Length / 2;
            yield return EvaluateBoundaryStability(series,
                series.Events.Skip(Math.Max(0, middle - 1)).Take(3).Select(x => x.EventId), minimum, method);
        }
    }

    private static AdaptiveReconstructionState Classify(string target, ImmutableArray<string> candidates) =>
        candidates.Length == 0 ? AdaptiveReconstructionState.NoContext
        : candidates.Length > 1 ? AdaptiveReconstructionState.Ambiguous
        : candidates[0] == target ? AdaptiveReconstructionState.Reconstructed
        : AdaptiveReconstructionState.Mismatch;

    private static IEnumerable<OriginalTemporalEvent> EventsInBeatWindow(
        ImmutableArray<OriginalTemporalEvent> events, decimal minimum, decimal maximum)
    {
        var low = 0;
        var high = events.Length;
        while (low < high)
        {
            var middle = low + ((high - low) >> 1);
            if (events[middle].FileDerivedBeat < minimum) low = middle + 1; else high = middle;
        }
        for (var index = low; index < events.Length && events[index].FileDerivedBeat <= maximum; index++)
            yield return events[index];
    }

    private static ExactRecurrenceOccurrence Occurrence(ImmutableArray<OriginalTemporalEvent> events,
        int start, int length)
    {
        var first = events[start];
        return new(first.EventId, start, first.SerializedTime, first.FileDerivedBeat,
            events.Skip(start).Take(length).SelectMany(x => x.AllObservationIds).Distinct()
                .OrderBy(x => x.Value).ToImmutableArray());
    }

    private static ImmutableArray<Run> Runs(ImmutableArray<OriginalTemporalEvent> events)
    {
        if (events.IsEmpty) return [];
        var result = ImmutableArray.CreateBuilder<Run>();
        var start = 0;
        for (var i = 1; i <= events.Length; i++)
        {
            if (i < events.Length && RegimeIdentity(events[i].Token) == RegimeIdentity(events[start].Token)) continue;
            result.Add(new Run(start, i - start, RegimeIdentity(events[start].Token)));
            start = i;
        }
        return result.ToImmutable();
    }

    private static AdaptiveMethodDeclaration Method(string id, string parameterSet,
        AdaptiveMethodDisposition disposition, string question, string assumptions, string complexity,
        string failureModes, params AdaptiveParameterDeclaration[] parameters) =>
        new(Identity(id, parameterSet), disposition, question, assumptions, complexity, failureModes,
            parameters.ToImmutableArray());

    private static AdaptiveMethodIdentity Identity(string method, string parameterSet) =>
        new(method, "1", RepresentationId, parameterSet);
    private static string RegimeIdentity(StructuralEventToken token) =>
        $"heads={token.HeadCount}|releases={token.ReleaseCount}|held={token.HeldBeforeLanes.Length}|mixed={token.MixedHeadTypes}";
    private static string Lanes(IEnumerable<int> lanes) => string.Join(',', lanes.Select(x =>
        x.ToString(CultureInfo.InvariantCulture)));
    private sealed record Run(int Start, int Length, string Identity);
    private sealed record Donor(OriginalTemporalEvent Center, ImmutableArray<OriginalObservationId> Provenance);
}

public static class AdaptiveContextResearchJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string Serialize(AdaptiveContextResearchResult result) => JsonSerializer.Serialize(result, Options);
}
