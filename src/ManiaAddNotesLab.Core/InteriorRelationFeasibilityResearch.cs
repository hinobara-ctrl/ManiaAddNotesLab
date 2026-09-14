using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ManiaAddNotesLab.Core;

public enum InteriorRelationClass
{
    Contained,
    EqualEnd,
    Crossing,
    Invalid,
    Degenerate,
    SerializationCollision,
    Other
}

public enum InteriorAlternativeState
{
    NoObservedRelation,
    ObservedUnique,
    ObservedAmongAlternatives,
    ConflictingForSpecificClaim
}

public enum InteriorHoldoutKind { TargetObservation, ParentOccurrence }

public enum InteriorLegacyGate
{
    Structural,
    SourceLength,
    SourceContext,
    RelativeOrAbsoluteLengthLegacy,
    AnchorSupport,
    AnchorContext,
    Ranking,
    Cap,
    CurrentOpportunity
}

public sealed record InteriorRelationOccurrence(
    string OccurrenceId,
    string ChartFingerprint,
    OriginalObservationId ParentLongNoteId,
    OriginalObservationId WitnessLongNoteId,
    int ParentLane,
    int WitnessLane,
    int ParentStartTime,
    int ParentEndTime,
    int AnchorTime,
    int WitnessEndTime,
    decimal ParentStartBeat,
    decimal ParentEndBeat,
    decimal AnchorBeat,
    decimal WitnessEndBeat,
    decimal DurationFromAnchorBeats,
    decimal OffsetFromParentEndBeats,
    InteriorAnchorKind AnchorKind,
    InteriorRelationClass RelationClass,
    bool SameLane,
    bool GeometryValidOnOriginalLaneAfterWitnessHoldout,
    bool SerializationValid,
    string QuerySignature,
    string RelationSignature);

public sealed record InteriorStructuralAnchor(
    string AnchorId,
    OriginalObservationId ParentLongNoteId,
    int AnchorTime,
    decimal AnchorBeat,
    InteriorAnchorKind AnchorKind,
    ImmutableArray<OriginalObservationId> HeadWitnessIds,
    ImmutableArray<OriginalObservationId> ReleaseWitnessIds,
    ImmutableArray<string> CompleteRelationOccurrenceIds);

public sealed record InteriorCurrentGateAudit(
    string AnchorId,
    OriginalObservationId ParentLongNoteId,
    int AnchorTime,
    int StructuralAnchorCountForParent,
    int SourceContextCount,
    int AnchorContextCount,
    bool SourceLengthPassed,
    bool SourceContextPassed,
    bool RelativeOrAbsoluteLengthPassed,
    bool AnchorSupportPassed,
    bool AnchorContextPassed,
    int? RankPosition,
    bool SelectedBeforeCap,
    bool CurrentOpportunity,
    InteriorLegacyGate FirstExclusionGate,
    int CompleteRelationCount,
    ImmutableArray<InteriorRelationClass> RelationClasses);

public sealed record InteriorRelationHoldout(
    string OccurrenceId,
    InteriorHoldoutKind HoldoutKind,
    string QuerySignature,
    string TargetRelationSignature,
    int ComparableDonorCount,
    int DistinctRelationCount,
    bool ExactJointSupported,
    bool DurationMarginalSupported,
    bool EndpointMarginalSupported,
    bool MarginalOnly,
    InteriorAlternativeState AlternativeState,
    ImmutableArray<string> DonorOccurrenceIds,
    int TargetLeakageCount,
    int ParentLeakageCount,
    int ReleaseLeakageCount,
    int FutureLeakageCount,
    int SameEventLeakageCount,
    int SyntheticLeakageCount,
    int CrossChartLeakageCount);

public sealed record InteriorRelationFeasibilityResult(
    string SchemaVersion,
    string ChartFingerprint,
    int KeyCount,
    int OriginalObjectCount,
    int OriginalLongNoteCount,
    ImmutableArray<InteriorStructuralAnchor> StructuralAnchors,
    ImmutableArray<InteriorRelationOccurrence> CompleteRelations,
    ImmutableArray<InteriorCurrentGateAudit> CurrentGateAudit,
    ImmutableArray<InteriorRelationHoldout> Holdouts,
    int CurrentInteriorOpportunityCount,
    int CurrentInteriorOpportunityMirrorMismatchCount,
    int MarginalOnlyAnchorCount,
    int IgnoredSyntheticObjectCount,
    int ResearchRngCalls);

/// <summary>
/// G1.0 research-only census. It reads a chart, creates no objects, consumes no RNG and has no normal
/// CLI/Web call site. Exact same-occurrence witnesses are kept separate from marginal values.
/// </summary>
public static class InteriorRelationFeasibilityResearch
{
    public const string SchemaVersion = "g1.0-interior-relation-feasibility-shadow.1";

    public static InteriorRelationFeasibilityResult Evaluate(ManiaChart source,
        AddNotesOptions? currentOptions = null, bool verifyCurrentOpportunityCount = true)
    {
        ArgumentNullException.ThrowIfNull(source);
        currentOptions ??= new AddNotesOptions
        {
            Chance = 0,
            InteriorLnOpportunitiesEnabled = true,
            MaxInteriorOpportunitiesPerSource = 2,
            InteriorMinimumSourceBeats = 3,
            InteriorLengthRatio = 1.5,
            InteriorAbsoluteLongBeats = 8,
            InteriorMinimumContextLnCount = 3,
            InteriorMinimumSupportedAnchors = 2,
            LnWindowBeats = 4,
            InteriorEligibilityMode = InteriorEligibilityMode.AnchorSupported,
            InteriorContextMode = InteriorContextMode.OriginalOnly
        };

        var originals = source.OriginalObjects.Where(x => !x.IsSynthetic && x.Origin == AddedObjectOrigin.None)
            .ToArray();
        var chart = new ManiaChart
        {
            KeyCount = source.KeyCount,
            Lines = source.Lines,
            OriginalObjects = originals,
            TimingPoints = source.TimingPoints
        };
        var ignoredSynthetic = source.OriginalObjects.Count - originals.Length;
        var profile = MapperEvidenceProfileBuilder.Build(chart);
        var timeline = new BeatTimeline(chart.TimingPoints);
        var analysis = new OriginalChartAnalysis(chart, timeline);
        var observations = profile.Observations.ToDictionary(x => x.Id);
        var objects = profile.Observations.ToDictionary(x => x.Id,
            x => chart.OriginalObjects[x.Id.Value]);
        var anchors = profile.InteriorAnchorObservations
            .OrderBy(x => x.ParentLongNoteId.Value).ThenBy(x => x.AnchorTime).ThenBy(x => x.AnchorBeat)
            .ToArray();
        var occurrences = new List<InteriorRelationOccurrence>();

        foreach (var anchor in anchors)
        {
            var parent = observations[anchor.ParentLongNoteId];
            foreach (var witnessId in anchor.HeadWitnessIds.OrderBy(x => x.Value))
            {
                var witness = observations[witnessId];
                if (witness.Type != ManiaObjectType.LongNote) continue;
                var relation = Classify(parent, witness);
                var geometry = OriginalLaneGeometryValid(observations.Values, witness);
                var serialization = witness.EndTime is not null && witness.EndTime.Value > witness.StartTime;
                var query = QuerySignature(chart.KeyCount, parent, anchor);
                var relationSignature = RelationSignature(parent, witness, relation);
                var id = Id(profile.ChartFingerprint, parent.Id, anchor.AnchorTime, anchor.AnchorBeat,
                    witness.Id, relationSignature);
                occurrences.Add(new InteriorRelationOccurrence(id, profile.ChartFingerprint, parent.Id,
                    witness.Id, parent.Lane, witness.Lane, parent.StartTime, parent.EndTime!.Value,
                    anchor.AnchorTime, witness.EndTime!.Value, parent.StartBeat, parent.EndBeat,
                    anchor.AnchorBeat, witness.EndBeat, witness.EndBeat - anchor.AnchorBeat,
                    witness.EndBeat - parent.EndBeat, anchor.Kind, relation, parent.Lane == witness.Lane,
                    geometry, serialization, query, relationSignature));
            }
        }
        occurrences.Sort((a, b) => StringComparer.Ordinal.Compare(a.OccurrenceId, b.OccurrenceId));
        var byAnchor = occurrences.GroupBy(x => AnchorId(x.ParentLongNoteId, x.AnchorTime, x.AnchorBeat))
            .ToDictionary(x => x.Key, x => x.Select(y => y.OccurrenceId).Order(StringComparer.Ordinal)
                .ToImmutableArray(), StringComparer.Ordinal);
        var structural = anchors.Select(x => new InteriorStructuralAnchor(
            AnchorId(x.ParentLongNoteId, x.AnchorTime, x.AnchorBeat), x.ParentLongNoteId, x.AnchorTime,
            x.AnchorBeat, x.Kind, x.HeadWitnessIds, x.ReleaseWitnessIds,
            byAnchor.GetValueOrDefault(AnchorId(x.ParentLongNoteId, x.AnchorTime, x.AnchorBeat), [])))
            .ToImmutableArray();

        var gateAudit = BuildGateAudit(analysis, profile, structural, occurrences, currentOptions);
        var selected = gateAudit.Count(x => x.CurrentOpportunity);
        var mirrorMismatch = 0;
        if (verifyCurrentOpportunityCount)
        {
            var result = new AddNotesEngine().Apply(chart, currentOptions with { Chance = 0 },
                new ZeroCallRandom(), profile);
            mirrorMismatch = Math.Abs(result.Statistics.InteriorLnOpportunities - selected);
            if (result.Statistics.SuccessfulProbabilityRolls != 0)
                throw new InvalidOperationException("G1.0 zero-chance mirror unexpectedly passed probability.");
        }
        var holdouts = BuildHoldouts(occurrences);
        var relationAnchorIds = occurrences.Select(x => AnchorId(x.ParentLongNoteId, x.AnchorTime, x.AnchorBeat))
            .ToHashSet(StringComparer.Ordinal);
        var marginalOnlyAnchors = structural.Count(x => !relationAnchorIds.Contains(x.AnchorId)
            && (x.HeadWitnessIds.Length > 0 || x.ReleaseWitnessIds.Length > 0));

        return new InteriorRelationFeasibilityResult(SchemaVersion, profile.ChartFingerprint, chart.KeyCount,
            chart.OriginalObjects.Count, profile.Observations.Count(x => x.Type == ManiaObjectType.LongNote),
            structural, occurrences.ToImmutableArray(), gateAudit, holdouts, selected, mirrorMismatch,
            marginalOnlyAnchors, ignoredSynthetic, 0);
    }

    private static ImmutableArray<InteriorCurrentGateAudit> BuildGateAudit(OriginalChartAnalysis analysis,
        MapperEvidenceProfile profile, IReadOnlyList<InteriorStructuralAnchor> anchors,
        IReadOnlyList<InteriorRelationOccurrence> occurrences, AddNotesOptions options)
    {
        var result = new List<InteriorCurrentGateAudit>();
        var relationLookup = occurrences.GroupBy(x => AnchorId(x.ParentLongNoteId, x.AnchorTime, x.AnchorBeat))
            .ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.Ordinal);
        var timedBySequence = analysis.Objects.ToDictionary(x => x.Object.Sequence);

        foreach (var parentGroup in anchors.GroupBy(x => x.ParentLongNoteId).OrderBy(x => x.Key.Value))
        {
            var parentObservation = profile.Observations[parentGroup.Key.Value];
            var parent = timedBySequence[parentObservation.SourceSequence];
            var parentAnchors = parentGroup.OrderBy(x => x.AnchorTime).ThenBy(x => x.AnchorBeat).ToArray();
            var sourceLengthPassed = parent.DurationBeats >= (decimal)options.InteriorMinimumSourceBeats;
            var sourceContext = options.InteriorEligibilityMode == InteriorEligibilityMode.Legacy
                ? analysis.LongNotesNear(parent.StartBeat, (decimal)options.LnWindowBeats)
                : ContextNear(analysis, parent.StartBeat, parent, (decimal)options.LnWindowBeats);
            var sourceContextPassed = sourceContext.Count >= options.InteriorMinimumContextLnCount;
            var relativeOrAbsolutePassed = true;
            if (sourceContext.Count > 0)
            {
                var durations = sourceContext.Select(x => x.DurationBeats).Order().ToArray();
                var median = durations[(durations.Length - 1) / 2];
                relativeOrAbsolutePassed = parent.DurationBeats >= median * (decimal)options.InteriorLengthRatio
                    || parent.DurationBeats >= (decimal)options.InteriorAbsoluteLongBeats;
            }
            if (options.InteriorEligibilityMode != InteriorEligibilityMode.Legacy)
                relativeOrAbsolutePassed = true;
            var supportPassed = options.InteriorEligibilityMode != InteriorEligibilityMode.AnchorSupported
                || parentAnchors.Length >= options.InteriorMinimumSupportedAnchors;
            var center = (parent.StartBeat + parent.EndBeat) / 2;
            var ranked = parentAnchors.Select(anchor =>
            {
                var context = ResolveContext(analysis, parent, anchor.AnchorBeat, options);
                var score = anchor.HeadWitnessIds.Length + anchor.ReleaseWitnessIds.Length + context.Count;
                return new Ranked(anchor, context.Count, score);
            }).Where(x => options.InteriorContextMode == InteriorContextMode.LegacyVirtualSource
                || x.ContextCount >= options.InteriorMinimumContextLnCount).ToList();
            var ordered = Rank(ranked, center, options).ToArray();
            var ranks = ordered.Select((x, i) => (x.Anchor.AnchorId, Rank: i + 1))
                .ToDictionary(x => x.AnchorId, x => x.Rank, StringComparer.Ordinal);

            foreach (var anchor in parentAnchors)
            {
                var anchorContext = ResolveContext(analysis, parent, anchor.AnchorBeat, options).Count;
                var anchorContextPassed = options.InteriorContextMode == InteriorContextMode.LegacyVirtualSource
                    || anchorContext >= options.InteriorMinimumContextLnCount;
                var eligible = sourceLengthPassed && sourceContextPassed && relativeOrAbsolutePassed
                    && supportPassed && anchorContextPassed;
                var rank = ranks.GetValueOrDefault(anchor.AnchorId);
                var current = eligible && rank > 0 && rank <= options.MaxInteriorOpportunitiesPerSource;
                var first = !sourceLengthPassed ? InteriorLegacyGate.SourceLength
                    : !sourceContextPassed ? InteriorLegacyGate.SourceContext
                    : !relativeOrAbsolutePassed ? InteriorLegacyGate.RelativeOrAbsoluteLengthLegacy
                    : !supportPassed ? InteriorLegacyGate.AnchorSupport
                    : !anchorContextPassed ? InteriorLegacyGate.AnchorContext
                    : rank <= 0 ? InteriorLegacyGate.Ranking
                    : rank > options.MaxInteriorOpportunitiesPerSource ? InteriorLegacyGate.Cap
                    : InteriorLegacyGate.CurrentOpportunity;
                var relations = relationLookup.GetValueOrDefault(anchor.AnchorId, []);
                result.Add(new InteriorCurrentGateAudit(anchor.AnchorId, anchor.ParentLongNoteId,
                    anchor.AnchorTime, parentAnchors.Length, sourceContext.Count, anchorContext,
                    sourceLengthPassed, sourceContextPassed, relativeOrAbsolutePassed, supportPassed,
                    anchorContextPassed, rank <= 0 ? null : rank, eligible, current, first,
                    relations.Length, relations.Select(x => x.RelationClass).Distinct().Order().ToImmutableArray()));
            }
        }
        return result.OrderBy(x => x.ParentLongNoteId.Value).ThenBy(x => x.AnchorTime).ToImmutableArray();
    }

    private static IEnumerable<Ranked> Rank(IReadOnlyList<Ranked> ranked, decimal center,
        AddNotesOptions options)
    {
        var selected = new List<Ranked>();
        if (options.InteriorEligibilityMode == InteriorEligibilityMode.Legacy
            && options.InteriorContextMode == InteriorContextMode.LegacyVirtualSource)
            selected.AddRange(ranked.OrderByDescending(x => x.Anchor.HeadWitnessIds.Length
                    + x.Anchor.ReleaseWitnessIds.Length)
                .ThenBy(x => decimal.Abs(x.Anchor.AnchorBeat - center)).ThenBy(x => x.Anchor.AnchorTime));
        else
        {
            while (selected.Count < ranked.Count)
            {
                var next = ranked.Where(x => !selected.Contains(x)).OrderByDescending(x => x.Score)
                    .ThenByDescending(x => selected.Count == 0 ? 0
                        : selected.Min(s => decimal.Abs(s.Anchor.AnchorBeat - x.Anchor.AnchorBeat)))
                    .ThenBy(x => decimal.Abs(x.Anchor.AnchorBeat - center)).ThenBy(x => x.Anchor.AnchorTime).First();
                selected.Add(next);
            }
        }
        return selected;
    }

    private static ImmutableArray<InteriorRelationHoldout> BuildHoldouts(
        IReadOnlyList<InteriorRelationOccurrence> occurrences)
    {
        var groups = occurrences.GroupBy(x => x.QuerySignature, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.OrderBy(y => y.AnchorTime).ThenBy(y => y.OccurrenceId,
                StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
        var result = new List<InteriorRelationHoldout>(occurrences.Count * 2);
        foreach (var target in occurrences.OrderBy(x => x.OccurrenceId, StringComparer.Ordinal))
        {
            foreach (var kind in Enum.GetValues<InteriorHoldoutKind>())
            {
                // Construction remains the frozen G1.0 query. Verification below is intentionally a
                // separate pass over the accepted set rather than a reused construction predicate.
                var donors = groups[target.QuerySignature].Where(x => x.ChartFingerprint == target.ChartFingerprint
                    && x.AnchorTime < target.AnchorTime
                    && x.WitnessLongNoteId != target.WitnessLongNoteId
                    && x.ParentLongNoteId != target.WitnessLongNoteId
                    && x.WitnessLongNoteId != target.ParentLongNoteId
                    && (kind != InteriorHoldoutKind.ParentOccurrence
                        || x.ParentLongNoteId != target.ParentLongNoteId)).ToArray();
                var accepted = donors.Select(x => new InteriorRelationHoldoutDonor(x)).ToArray();
                var assessment = InteriorRelationHoldoutAuditor.Assess(target, accepted);
                var audit = InteriorRelationHoldoutAuditor.Audit(target, kind, accepted);
                result.Add(new InteriorRelationHoldout(target.OccurrenceId, kind, target.QuerySignature,
                    target.RelationSignature, assessment.ComparableDonorCount,
                    assessment.DistinctRelationCount, assessment.ExactJointSupported,
                    assessment.DurationMarginalSupported, assessment.EndpointMarginalSupported,
                    assessment.MarginalOnly, assessment.AlternativeState, assessment.DonorOccurrenceIds,
                    audit.TargetLeakageCount, audit.ParentLeakageCount, audit.ReleaseLeakageCount,
                    audit.FutureLeakageCount, audit.SameEventLeakageCount, audit.SyntheticLeakageCount,
                    audit.CrossChartLeakageCount));
            }
        }
        return result.ToImmutableArray();
    }

    private static IReadOnlyList<TimedManiaObject> ResolveContext(OriginalChartAnalysis analysis,
        TimedManiaObject parent, decimal anchorBeat, AddNotesOptions options)
    {
        var near = ContextNear(analysis, anchorBeat, parent, (decimal)options.LnWindowBeats);
        if (near.Count >= options.InteriorMinimumContextLnCount) return near;
        return ContextNear(analysis, parent.StartBeat, parent, (decimal)options.LnWindowBeats);
    }

    private static IReadOnlyList<TimedManiaObject> ContextNear(OriginalChartAnalysis analysis, decimal beat,
        TimedManiaObject parent, decimal window) => analysis.LongNotesNear(beat, window)
        .Where(x => x.Object.Sequence != parent.Object.Sequence || x.Object.StartTime != parent.Object.StartTime
            || x.Object.EndTime != parent.Object.EndTime).ToArray();

    private static InteriorRelationClass Classify(OriginalObservation parent, OriginalObservation witness)
    {
        if (witness.EndTime is null) return InteriorRelationClass.Invalid;
        if (witness.EndTime.Value <= witness.StartTime) return witness.EndTime.Value == witness.StartTime
            ? InteriorRelationClass.SerializationCollision : InteriorRelationClass.Degenerate;
        if (witness.EndBeat <= witness.StartBeat) return InteriorRelationClass.Degenerate;
        if (witness.EndBeat < parent.EndBeat) return InteriorRelationClass.Contained;
        if (witness.EndBeat == parent.EndBeat) return InteriorRelationClass.EqualEnd;
        if (witness.EndBeat > parent.EndBeat) return InteriorRelationClass.Crossing;
        return InteriorRelationClass.Other;
    }

    private static bool OriginalLaneGeometryValid(IEnumerable<OriginalObservation> observations,
        OriginalObservation witness) => observations.Where(x => x.Id != witness.Id && x.Lane == witness.Lane)
        .All(x => x.Type == ManiaObjectType.Tap
            ? x.StartBeat < witness.StartBeat || x.StartBeat >= witness.EndBeat
            : x.StartBeat >= witness.EndBeat || x.EndBeat <= witness.StartBeat);

    public static string CanonicalQuerySignature(int keys, decimal parentDurationBeats,
        decimal anchorOffsetFromParentStartBeats, InteriorAnchorKind anchorKind) =>
        $"K:{keys}|PD:{D(parentDurationBeats)}|AO:{D(anchorOffsetFromParentStartBeats)}|AK:{(int)anchorKind}";

    public static string CanonicalRelationSignature(InteriorRelationClass relation,
        decimal durationFromAnchorBeats, decimal offsetFromParentEndBeats) =>
        $"R:{relation}|D:{D(durationFromAnchorBeats)}|O:{D(offsetFromParentEndBeats)}";

    private static string QuerySignature(int keys, OriginalObservation parent, InteriorAnchorObservation anchor) =>
        CanonicalQuerySignature(keys, parent.DurationBeats, anchor.AnchorBeat - parent.StartBeat, anchor.Kind);

    private static string RelationSignature(OriginalObservation parent, OriginalObservation witness,
        InteriorRelationClass relation) => CanonicalRelationSignature(relation,
            witness.EndBeat - witness.StartBeat, witness.EndBeat - parent.EndBeat);

    private static string AnchorId(OriginalObservationId parent, int time, decimal beat) =>
        $"{parent}-A{time}-B{D(beat)}";

    private static string Id(string chart, OriginalObservationId parent, int time, decimal beat,
        OriginalObservationId witness, string relation)
    {
        var bytes = Encoding.UTF8.GetBytes($"{SchemaVersion}|{chart}|{parent}|{time}|{D(beat)}|{witness}|{relation}");
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static string D(decimal value) => value.ToString(CultureInfo.InvariantCulture);
    private sealed record Ranked(InteriorStructuralAnchor Anchor, int ContextCount, int Score);

    private sealed class ZeroCallRandom : IRandomSource
    {
        public double NextDouble() => throw new InvalidOperationException("G1.0 must consume zero RNG.");
        public int Next(int maxExclusive) => throw new InvalidOperationException("G1.0 must consume zero RNG.");
    }
}
