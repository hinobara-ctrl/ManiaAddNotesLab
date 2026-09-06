using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace ManiaAddNotesLab.Core;

public sealed class AddNotesEngine
{
    public const double SourceAffinityMultiplier = 1.25;
    private const decimal Epsilon = 0.000001m;

    public AddNotesResult Apply(ManiaChart chart, AddNotesOptions options, IRandomSource rng) =>
        Apply(chart, options, rng, null);

    public AddNotesResult Apply(ManiaChart chart, AddNotesOptions options, IRandomSource rng,
        MapperEvidenceProfile? preparedEvidenceProfile)
    {
        Validate(chart, options, rng);
        MapperEvidenceProfile evidenceProfile;
        double profileBuildMs;
        if (preparedEvidenceProfile is null)
        {
            var profileStarted = Stopwatch.GetTimestamp();
            evidenceProfile = MapperEvidenceProfileBuilder.Build(chart);
            profileBuildMs = ElapsedMs(profileStarted);
        }
        else
        {
            ValidateEvidenceProfile(chart, preparedEvidenceProfile);
            evidenceProfile = preparedEvidenceProfile;
            profileBuildMs = 0;
        }
        var timeline = new BeatTimeline(chart.TimingPoints);
        var analysis = new OriginalChartAnalysis(chart, timeline);
        var geometry = new LaneGeometryIndex(chart.KeyCount, analysis.Objects);
        var densityGeometry = new LaneGeometryIndex(chart.KeyCount, analysis.Objects);
        var densityAnalyzer = new HeadDensityAnalyzer(analysis.Heads, chart.KeyCount);
        var gapAnalyzer = new LocalLaneGapAnalyzer(analysis);
        var retriggerAnalyzer = new LocalRetriggerGapAnalyzer(analysis);
        var trace = options.Trace ? new StringBuilder() : null;
        var diagnostics = options.DiagnosticsEnabled ? new DecisionDiagnosticsBuilder(evidenceProfile) : null;
        var diagnosticTimeline = diagnostics is null ? null : new BeatTimeline(chart.TimingPoints);
        if (trace is not null && diagnostics is not null) trace.AppendLine("[LEGACY DECISION]");
        var stats = NewStatistics(chart.OriginalObjects);
        stats.ProfileBuildMs = profileBuildMs;
        stats.ProfileEstimatedSizeBytes = evidenceProfile.EstimatedSizeBytes;
        stats.ProfileObservationCount = evidenceProfile.ObservationCount;
        stats.ProfileRelationCount = evidenceProfile.RelationCount;
        var added = new List<ManiaObject>();
        var opportunities = BuildOpportunities(analysis, options, stats, trace);
        var articulationIntents = new List<ArticulationIntent>();
        var effectiveChanceSum = 0d;
        var pass1Started = Stopwatch.GetTimestamp();

        foreach (var opportunity in opportunities)
        {
            var opportunityKey = diagnostics?.OpportunityKey(opportunity.Kind, opportunity.Order,
                opportunity.Source.Object, opportunity.ParentOriginalLn?.Object, opportunity.InteriorAnchor?.Time);
            var density = densityAnalyzer.Analyze(opportunity.Source.StartBeat, options);
            var contextualFactor = options.ContextualDensityNormalizationEnabled ? density.ContextualFactor : 1.0;
            var geometryStart = Stopwatch.GetTimestamp();
            // Only originals teach density. The mutable geometry still owns collision checks,
            // but synthetic placements from earlier opportunities cannot feed probability factors.
            var occupiedColumns = densityGeometry.CountOccupiedColumns(opportunity.Source.StartBeat, stats);
            var simultaneousHeadColumns = densityGeometry.CountSimultaneousHeadColumns(opportunity.Source.StartBeat);
            var heldLnColumns = densityGeometry.CountHeldLnColumns(opportunity.Source.StartBeat);
            stats.GeometryMs += ElapsedMs(geometryStart);
            stats.SimultaneousHeadColumnsTotal += simultaneousHeadColumns;
            stats.HeldLnColumnsTotal += heldLnColumns;
            stats.SimultaneousHeadRatioSum += (double)simultaneousHeadColumns / chart.KeyCount;
            stats.HeldLnRatioSum += (double)heldLnColumns / chart.KeyCount;
            stats.NonHeldColumnsSum += chart.KeyCount - heldLnColumns;
            stats.NonHeldRatioSum += (double)(chart.KeyCount - heldLnColumns) / chart.KeyCount;
            stats.VerticalDensityObservationCount++;
            var densityColumns = options.VerticalDensityMode == VerticalDensityMode.SimultaneousHeads
                ? simultaneousHeadColumns : occupiedColumns;
            var vertical = CalculateVerticalDensity(chart.KeyCount, densityColumns, options);
            var chordFactor = vertical.ChordFactor;
            var effectiveChance = Math.Min(options.Chance, options.Chance * chordFactor * contextualFactor);
            effectiveChanceSum += effectiveChance;
            if (opportunity.Kind == OpportunityKind.LnInterior)
                stats.InteriorEffectiveChanceSum += effectiveChance;
            var isTapOpportunity = opportunity.Kind == OpportunityKind.BaseHead
                && opportunity.Source.Object.Type == ManiaObjectType.Tap;
            if (isTapOpportunity)
            {
                stats.TapOpportunities++;
                stats.TapEffectiveChanceSum += effectiveChance;
                stats.TapChordFactorSum += chordFactor;
                stats.TapContextualFactorSum += contextualFactor;
            }
            if (chordFactor < 1 - 1e-9) stats.DensityAdjustedOpportunities++;
            if (contextualFactor < 1 - 1e-9) stats.ContextualDensityAdjustedOpportunities++;
            AppendDensityTrace(trace, opportunity, options, density, occupiedColumns, simultaneousHeadColumns,
                heldLnColumns, chordFactor, effectiveChance);

            if (effectiveChance <= 0 || (effectiveChance < 1 && rng.NextDouble() >= effectiveChance)) continue;
            stats.SuccessfulProbabilityRolls++;
            if (opportunity.Kind == OpportunityKind.BaseHead)
            {
                stats.SuccessfulBaseRolls++;
                if (opportunity.Source.Object.Type == ManiaObjectType.LongNote) stats.BaseLnSuccessfulRolls++;
                else stats.TapSuccessfulRolls++;
            }
            else stats.SuccessfulInteriorRolls++;

            TimedManiaObject? placed = opportunity.Source.Object.Type == ManiaObjectType.Tap
                ? PlaceTap(opportunity, geometry, rng, stats, trace, diagnostics, opportunityKey,
                    evidenceProfile, simultaneousHeadColumns, heldLnColumns)
                : PlaceLongNote(opportunity, analysis, geometry, gapAnalyzer, timeline, options, rng, stats, trace,
                    diagnostics, opportunityKey, evidenceProfile, diagnosticTimeline);

            if (placed is null)
            {
                stats.FailedPlacements++;
                if (opportunity.Kind == OpportunityKind.LnInterior)
                {
                    stats.InteriorNoFreeLane++;
                    var nonHeldColumns = chart.KeyCount - heldLnColumns;
                    if (nonHeldColumns <= options.ArticulationMaxNonHeldColumns)
                    {
                        stats.InteriorBlockedByLnSaturation++;
                        articulationIntents.Add(new ArticulationIntent(opportunity, heldLnColumns,
                            nonHeldColumns, (double)heldLnColumns / chart.KeyCount, opportunityKey));
                    }
                    else stats.RejectedNotSaturated++;
                }
                continue;
            }
            added.Add(placed.Object);
            geometry.Insert(placed);
            if (placed.Object.Type == ManiaObjectType.Tap)
            {
                stats.AddedTaps++;
                stats.TapPlaced++;
                Increment(stats.TapPlacedBySimultaneousHeadCount, simultaneousHeadColumns);
                Increment(stats.TapPlacedByHeadRatioPercent, RatioPercent(simultaneousHeadColumns, chart.KeyCount));
                Increment(stats.TapPlacedByHeldLnColumns, heldLnColumns);
                Increment(stats.TapPlacedByHeldLnRatioPercent, RatioPercent(heldLnColumns, chart.KeyCount));
                if (density.ContextualFactor < 1 - 1e-9) stats.TapPlacedBurst++;
                else if (density.ElevatedSpanBeats >= 4) stats.TapPlacedSustained++;
                else stats.TapPlacedNormal++;
            }
            else
            {
                stats.AddedLongNotes++;
                if (opportunity.Kind == OpportunityKind.BaseHead)
                {
                    stats.AddedLongNotesFromHeadOpportunities++;
                    stats.BaseLnPlaced++;
                }
                else stats.AddedLongNotesFromInteriorOpportunities++;
            }
        }

        stats.Pass1Ms = ElapsedMs(pass1Started);
        var articulationStarted = Stopwatch.GetTimestamp();
        var replacements = options.ArticulationEnabled
            ? ResolveArticulations(articulationIntents, analysis, geometry, retriggerAnalyzer, timeline,
                options, DeriveArticulationRandom(rng, chart), stats, trace, diagnostics, evidenceProfile)
            : [];
        stats.ArticulationPassMs = options.ArticulationEnabled ? ElapsedMs(articulationStarted) : 0;

        stats.TotalOpportunities = opportunities.Count;
        stats.OutputObjects = stats.InputObjects + stats.PlacedObjects + replacements.Count;
        stats.MeanEffectiveChance = opportunities.Count == 0 ? 0 : effectiveChanceSum / opportunities.Count;
        stats.BeatConversions = timeline.ConversionCount;
        var decisionDiagnostics = diagnostics?.Build();
        if (decisionDiagnostics is not null)
        {
            stats.CertificateBuildMs = decisionDiagnostics.CertificateBuildMs;
            stats.FailureDiagnosticMs = decisionDiagnostics.FailureDiagnosticMs;
            stats.DiagnosticCandidateCount = decisionDiagnostics.Summary.DiagnosticCandidateCount;
            stats.CandidateLaneDiagnosticCount = decisionDiagnostics.Summary.CandidateLaneDiagnosticCount;
            stats.WitnessReferenceCount = decisionDiagnostics.Summary.WitnessReferenceCount;
            AppendShadowTrace(trace, decisionDiagnostics);
        }
        AppendPerformanceTrace(trace, stats);
        return new AddNotesResult(new ManiaChart
        {
            KeyCount = chart.KeyCount,
            Lines = chart.Lines,
            OriginalObjects = chart.OriginalObjects,
            TimingPoints = chart.TimingPoints,
            AddedObjects = added,
            ArticulationReplacements = replacements
        }, stats, trace?.ToString(), evidenceProfile, decisionDiagnostics);
    }

    public DensitySnapshot AnalyzeDensity(ManiaChart chart, AddNotesOptions options, int time)
    {
        var timeline = new BeatTimeline(chart.TimingPoints);
        var analysis = new OriginalChartAnalysis(chart, timeline);
        return new HeadDensityAnalyzer(analysis.Heads, chart.KeyCount).Analyze(timeline.ToBeatDecimal(time), options);
    }

    public VerticalDensitySnapshot AnalyzeVerticalDensity(int keyCount, int columns, AddNotesOptions options) =>
        CalculateVerticalDensity(keyCount, columns, options);

    public LaneGapDecision ResolveLocalLaneGap(ManiaChart chart, AddNotesOptions options, int time)
    {
        var timeline = new BeatTimeline(chart.TimingPoints);
        var analysis = new OriginalChartAnalysis(chart, timeline);
        return new LocalLaneGapAnalyzer(analysis).Resolve(timeline.ToBeatDecimal(time), options);
    }

    public LocalLnContext BuildLocalLnContext(ManiaObject source, IReadOnlyList<ManiaObject> originals,
        BeatTimeline timeline, double windowBeats)
    {
        var chart = new ManiaChart { KeyCount = Math.Max(1, originals.Max(o => o.Lane) + 1), Lines = [],
            OriginalObjects = originals, TimingPoints = [] };
        var analysis = new OriginalChartAnalysis(chart, timeline);
        var timedSource = analysis.Objects.FirstOrDefault(o => ReferenceEquals(o.Object, source) || o.Object == source)
            ?? new TimedManiaObject(source, timeline.ToBeatDecimal(source.StartTime), timeline.ToBeatDecimal(source.EndTime!.Value));
        return BuildOriginalLnContext(timedSource, analysis, windowBeats);
    }

    public IReadOnlyList<ReleaseCandidate> BuildReleaseCandidates(ManiaObject source, LocalLnContext context,
        BeatTimeline timeline, double windowBeats, double sourceAffinityMultiplier = SourceAffinityMultiplier,
        double distanceDecayPerBeat = 0.20, double minimumDistanceWeight = 0.20,
        bool applySourceAffinity = true)
    {
        var timed = new TimedManiaObject(source, context.SourceHeadBeat,
            source.EndTime is null ? context.SourceHeadBeat : timeline.ToBeatDecimal(source.EndTime.Value));
        return BuildReleaseCandidates(timed, context, timeline, new AddNotesOptions
        {
            LnWindowBeats = windowBeats,
            SourceAffinityMultiplier = sourceAffinityMultiplier,
            DistanceDecayPerBeat = distanceDecayPerBeat,
            MinimumDistanceWeight = minimumDistanceWeight,
            MapRelativeSnapEnabled = true
        }, applySourceAffinity, computeC1Weights: true);
    }

    public LocalLnContext BuildOriginalInteriorLnContext(ManiaObject parent, int anchorTime,
        IReadOnlyList<ManiaObject> originals, BeatTimeline timeline, double windowBeats, int minimumContext = 3)
    {
        var chart = new ManiaChart { KeyCount = Math.Max(1, originals.Max(o => o.Lane) + 1), Lines = [],
            OriginalObjects = originals, TimingPoints = [] };
        var analysis = new OriginalChartAnalysis(chart, timeline);
        var timedParent = analysis.Objects.Single(o => o.Object == parent);
        var anchor = new AnchorEvidence(anchorTime, timeline.ToBeatDecimal(anchorTime), 1, "diagnostic");
        var options = new AddNotesOptions { LnWindowBeats = windowBeats,
            InteriorMinimumContextLnCount = minimumContext };
        var context = ResolveOriginalInteriorContext(timedParent, anchor, analysis, options);
        if (context.Count < minimumContext)
            throw new InvalidOperationException("Insufficient original LN context for this interior anchor.");
        var observations = context.Select(o => new LnObservation(o.Object, o.StartBeat, o.EndBeat)).ToArray();
        return new LocalLnContext(anchor.Beat, observations,
            observations.Min(o => o.ReleaseBeat - o.HeadBeat));
    }

    // Compatibility helper for focused geometry tests; the engine hot path uses LaneGeometryIndex.
    public static IReadOnlyList<int> FindLegalLanes(int keys, int start, int? end, IReadOnlyList<ManiaObject> occupied)
    {
        var result = new List<int>();
        for (var lane = 0; lane < keys; lane++)
        {
            var legal = true;
            foreach (var obj in occupied)
            {
                if (obj.Lane != lane) continue;
                legal = end is null
                    ? obj.Type == ManiaObjectType.Tap ? obj.StartTime != start : obj.StartTime > start || obj.EndTime!.Value < start
                    : obj.Type == ManiaObjectType.Tap ? obj.StartTime < start || obj.StartTime > end : obj.StartTime > end || obj.EndTime!.Value < start;
                if (!legal) break;
            }
            if (legal) result.Add(lane);
        }
        return result;
    }

    private static List<AddNoteOpportunity> BuildOpportunities(OriginalChartAnalysis analysis, AddNotesOptions options,
        AddNotesStatistics stats, StringBuilder? trace)
    {
        var result = new List<AddNoteOpportunity>();
        var order = 0;
        foreach (var source in analysis.Heads)
        {
            if (!InRange(source.Object.StartTime, options)) continue;
            result.Add(new AddNoteOpportunity(source, OpportunityKind.BaseHead, order++));
            stats.BaseHeadOpportunities++;
            if (source.Object.Type == ManiaObjectType.LongNote) stats.BaseLnOpportunityCount++;
        }
        if (options.InteriorLnOpportunitiesEnabled && options.MaxInteriorOpportunitiesPerSource > 0)
        {
            foreach (var source in analysis.LongNotes)
            {
                foreach (var interior in BuildInteriorOpportunities(source, analysis, options, stats, order, trace))
                {
                    order++;
                    if (!InRange(interior.Source.Object.StartTime, options)) continue;
                    result.Add(interior);
                    stats.InteriorLnOpportunities++;
                }
            }
        }
        result.Sort((a, b) =>
        {
            var time = a.Source.Object.StartTime.CompareTo(b.Source.Object.StartTime);
            if (time != 0) return time;
            var kind = a.Kind.CompareTo(b.Kind);
            return kind != 0 ? kind : a.Order.CompareTo(b.Order);
        });
        return result;
    }

    private static IReadOnlyList<AddNoteOpportunity> BuildInteriorOpportunities(TimedManiaObject source,
        OriginalChartAnalysis analysis, AddNotesOptions options, AddNotesStatistics stats, int firstOrder,
        StringBuilder? trace)
    {
        if (source.DurationBeats < (decimal)options.InteriorMinimumSourceBeats)
        {
            stats.InteriorRejectedTooShort++;
            return [];
        }
        var local = options.InteriorEligibilityMode == InteriorEligibilityMode.Legacy
            ? analysis.LongNotesNear(source.StartBeat, (decimal)options.LnWindowBeats)
            : OriginalContextNear(analysis, source.StartBeat, source, (decimal)options.LnWindowBeats);
        if (local.Count < options.InteriorMinimumContextLnCount)
        {
            stats.InteriorRejectedInsufficientLnContext++;
            return [];
        }
        var durations = local.Select(o => o.DurationBeats).Order().ToArray();
        var median = durations[(durations.Length - 1) / 2];
        var relativePriority = source.DurationBeats >= median * (decimal)options.InteriorLengthRatio;
        var absolutePriority = source.DurationBeats >= (decimal)options.InteriorAbsoluteLongBeats;
        if (options.InteriorEligibilityMode == InteriorEligibilityMode.Legacy
            && !relativePriority && !absolutePriority)
        {
            stats.InteriorRejectedRelativeLengthLegacy++;
            return [];
        }

        var anchors = new Dictionary<int, AnchorEvidence>();
        foreach (var item in analysis.ObjectsBetween(source.StartBeat, source.EndBeat))
            AddAnchor(item.Object.StartTime, item.StartBeat, "head");
        foreach (var item in analysis.ReleasesBetween(source.StartBeat, source.EndBeat))
            AddAnchor(item.Time, item.Beat, "release");
        if (options.InteriorEligibilityMode == InteriorEligibilityMode.AnchorSupported
            && anchors.Count < options.InteriorMinimumSupportedAnchors)
        {
            stats.InteriorRejectedNoSupportedAnchors++;
            return [];
        }

        var center = (source.StartBeat + source.EndBeat) / 2;
        var ranked = anchors.Values.Select(anchor =>
        {
            var anchorContext = ResolveOriginalInteriorContext(source, anchor, analysis, options);
            return new RankedAnchor(anchor, anchorContext, anchor.Count + anchorContext.Count);
        }).Where(x => options.InteriorContextMode == InteriorContextMode.LegacyVirtualSource
            || x.Context.Count >= options.InteriorMinimumContextLnCount).ToList();
        if (ranked.Count == 0)
        {
            stats.InteriorRejectedInsufficientLnContext++;
            return [];
        }

        var selected = new List<RankedAnchor>();
        if (options.InteriorEligibilityMode == InteriorEligibilityMode.Legacy
            && options.InteriorContextMode == InteriorContextMode.LegacyVirtualSource)
        {
            selected.AddRange(ranked.OrderByDescending(x => x.Anchor.Count)
                .ThenBy(x => decimal.Abs(x.Anchor.Beat - center)).ThenBy(x => x.Anchor.Time)
                .Take(options.MaxInteriorOpportunitiesPerSource));
        }
        while (selected.Count < options.MaxInteriorOpportunitiesPerSource && selected.Count < ranked.Count)
        {
            var next = ranked.Where(x => !selected.Contains(x))
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => selected.Count == 0 ? 0 : selected.Min(s => decimal.Abs(s.Anchor.Beat - x.Anchor.Beat)))
                .ThenBy(x => decimal.Abs(x.Anchor.Beat - center))
                .ThenBy(x => x.Anchor.Time).First();
            selected.Add(next);
        }
        var result = new List<AddNoteOpportunity>();
        foreach (var rankedAnchor in selected)
        {
            var anchor = rankedAnchor.Anchor;
            var virtualObject = ManiaObject.Ln(source.Object.Lane, anchor.Time, source.Object.EndTime!.Value,
                sequence: source.Object.Sequence, origin: AddedObjectOrigin.LnInteriorOpportunity);
            var virtualSource = new TimedManiaObject(virtualObject, anchor.Beat, source.EndBeat);
            trace?.AppendLine($"INTERIOR parentOriginalLn={source.Object.StartTime}-{source.Object.EndTime}@lane{source.Object.Lane + 1} interiorAnchor={anchor.Time} opportunityStartBeat={F((double)anchor.Beat)} supportedAnchors={anchors.Count} evidence={anchor.Count} ({anchor.Reasons}) localOriginalLnCount={rankedAnchor.Context.Count} priority={(absolutePriority ? "absolute" : relativePriority ? "relative" : "normal")} accepted");
            result.Add(new AddNoteOpportunity(virtualSource, OpportunityKind.LnInterior, firstOrder + result.Count,
                source, anchor));
        }
        return result;

        void AddAnchor(int time, decimal beat, string reason)
        {
            if (anchors.TryGetValue(time, out var existing))
                anchors[time] = existing with { Count = existing.Count + 1, Reasons = existing.Reasons + "+" + reason };
            else anchors[time] = new AnchorEvidence(time, beat, 1, reason);
        }
    }

    private static IReadOnlyList<TimedManiaObject> OriginalContextNear(OriginalChartAnalysis analysis, decimal beat,
        TimedManiaObject parent, decimal window) => analysis.LongNotesNear(beat, window)
        .Where(o => o.Object.Sequence != parent.Object.Sequence || o.Object.StartTime != parent.Object.StartTime
            || o.Object.EndTime != parent.Object.EndTime).ToArray();

    private static IReadOnlyList<TimedManiaObject> ResolveOriginalInteriorContext(TimedManiaObject parent,
        AnchorEvidence anchor, OriginalChartAnalysis analysis, AddNotesOptions options)
    {
        var nearAnchor = OriginalContextNear(analysis, anchor.Beat, parent, (decimal)options.LnWindowBeats);
        if (nearAnchor.Count >= options.InteriorMinimumContextLnCount) return nearAnchor;
        return OriginalContextNear(analysis, parent.StartBeat, parent, (decimal)options.LnWindowBeats);
    }

    private static TimedManiaObject? PlaceTap(AddNoteOpportunity opportunity, LaneGeometryIndex geometry,
        IRandomSource rng, AddNotesStatistics stats, StringBuilder? trace, DecisionDiagnosticsBuilder? diagnostics,
        OpportunityDiagnosticKey? opportunityKey, MapperEvidenceProfile profile, int originalHeads, int originalHeld)
    {
        var started = Stopwatch.GetTimestamp();
        var lanes = geometry.FindLegalTapLanes(opportunity.Source.StartBeat, stats);
        ObserveLegalLaneRatio(stats, lanes.Count, geometry.KeyCount);
        stats.GeometryMs += ElapsedMs(started);
        HardValidityResult? validity = null;
        if (diagnostics is not null)
        {
            started = Stopwatch.GetTimestamp();
            validity = geometry.ExplainTapPlacement(opportunity.Source.StartBeat, diagnostics.Provenance);
            diagnostics.AddFailureTicks(Stopwatch.GetTimestamp() - started);
            var explained = validity.LaneEvaluations.Where(x => x.IsValid).Select(x => x.Lane).ToArray();
            if (!lanes.SequenceEqual(explained)) throw new InvalidOperationException("Legacy and diagnostic tap legality diverged.");
        }
        if (lanes.Count == 0)
        {
            if (diagnostics is not null)
            {
                var key = diagnostics.CandidateKey(opportunityKey!.Value, DiagnosticCandidateKind.Tap, 0,
                    opportunity.Source.Object.StartTime, null);
                var certificate = diagnostics.Certificate(key, [], validity!, false);
                diagnostics.Record(new DecisionDiagnostic(opportunityKey.Value, key, DiagnosticCandidateKind.Tap,
                    opportunity.Kind, opportunity.Order, 0, opportunity.Source.Object.StartTime, null,
                    opportunity.Source.StartBeat, null, null, LegacyDecisionOutcome.NoLegalLane, null, [], 0, 0,
                    certificate, validity!, null, InteriorAnchorKind.None, null, InteriorEndRelation.NotApplicable,
                    new RiceStateDiagnostic(geometry.KeyCount, originalHeads, originalHeld,
                        geometry.CountSimultaneousHeadColumns(opportunity.Source.StartBeat), geometry.KeyCount,
                        0, null, geometry.CountSimultaneousHeadColumns(opportunity.Source.StartBeat))));
            }
            return null;
        }
        var lane = lanes[rng.Next(lanes.Count)];
        var obj = ManiaObject.Tap(lane, opportunity.Source.Object.StartTime, true, opportunity.Source.Object.Sequence)
            with { Origin = AddedObjectOrigin.HeadOpportunity };
        if (diagnostics is not null)
        {
            var key = diagnostics.CandidateKey(opportunityKey!.Value, DiagnosticCandidateKind.Tap, 0,
                opportunity.Source.Object.StartTime, null);
            var certificate = diagnostics.Certificate(key, [], validity!, false);
            var currentHeads = geometry.CountSimultaneousHeadColumns(opportunity.Source.StartBeat);
            diagnostics.Record(new DecisionDiagnostic(opportunityKey.Value, key, DiagnosticCandidateKind.Tap,
                opportunity.Kind, opportunity.Order, 0, opportunity.Source.Object.StartTime, null,
                opportunity.Source.StartBeat, null, null, LegacyDecisionOutcome.Placed, lane, [], 0, 0,
                certificate, validity!, null, InteriorAnchorKind.None, null, InteriorEndRelation.NotApplicable,
                new RiceStateDiagnostic(geometry.KeyCount, originalHeads, originalHeld, currentHeads,
                    geometry.KeyCount - lanes.Count, lanes.Count, lane, currentHeads + 1)));
        }
        return new TimedManiaObject(obj, opportunity.Source.StartBeat, opportunity.Source.StartBeat);
    }

    private TimedManiaObject? PlaceLongNote(AddNoteOpportunity opportunity, OriginalChartAnalysis analysis,
        LaneGeometryIndex geometry, LocalLaneGapAnalyzer gapAnalyzer, BeatTimeline timeline, AddNotesOptions options,
        IRandomSource rng, AddNotesStatistics stats, StringBuilder? trace, DecisionDiagnosticsBuilder? diagnostics,
        OpportunityDiagnosticKey? opportunityKey, MapperEvidenceProfile profile, BeatTimeline? diagnosticTimeline)
    {
        var detailTrace = opportunity.Kind == OpportunityKind.LnInterior ? trace : null;
        var started = Stopwatch.GetTimestamp();
        var context = opportunity.Kind == OpportunityKind.LnInterior
            ? options.InteriorContextMode == InteriorContextMode.OriginalOnly
                ? BuildOriginalInteriorContext(opportunity, analysis, options)
                : BuildLegacyVirtualLnContext(opportunity.Source, analysis, options.LnWindowBeats)
            : BuildOriginalLnContext(opportunity.Source, analysis, options.LnWindowBeats);
        var gap = gapAnalyzer.Resolve(opportunity.Source.StartBeat, options);
        stats.ContextBuildMs += ElapsedMs(started);

        started = Stopwatch.GetTimestamp();
        var candidates = BuildReleaseCandidates(opportunity.Source, context, timeline, options,
            applySourceAffinity: opportunity.Kind != OpportunityKind.LnInterior,
            computeC1Weights: diagnostics is not null);
        stats.CandidateBuildMs += ElapsedMs(started);
        stats.LnCandidatesBuilt += candidates.Count;
        detailTrace?.AppendLine($"GAP source={(gap.IsLocal ? "local" : "fallback")} value={F(gap.GapBeats)} evidence={gap.EvidenceCount}");
        detailTrace?.AppendLine($"LN source: time={opportunity.Source.Object.StartTime}, lane={opportunity.Source.Object.Lane + 1}, duration={F((double)opportunity.Source.DurationBeats)} beats");
        detailTrace?.AppendLine($"LN context: mode={options.InteriorContextMode} count={context.Observations.Count}, minimum={F((double)context.MinimumDurationBeats)} beats");

        var resolved = new List<ResolvedReleaseCandidate>(candidates.Count);
        if (candidates.Count == 0 && diagnostics is not null)
        {
            var validity = new HardValidityResult(false, [HardValidityFailureCause.NoShapeEvidence], []);
            var key = diagnostics.CandidateKey(opportunityKey!.Value, DiagnosticCandidateKind.LongNote, 0,
                opportunity.Source.Object.StartTime, null);
            diagnostics.Record(new DecisionDiagnostic(opportunityKey.Value, key, DiagnosticCandidateKind.LongNote,
                opportunity.Kind, opportunity.Order, 0, opportunity.Source.Object.StartTime, null,
                opportunity.Source.StartBeat, null, null, LegacyDecisionOutcome.NoShapeEvidence, null, [], 0, 0,
                diagnostics.Certificate(key, [], validity, true), validity,
                ParentId(opportunity, diagnostics), AnchorKind(opportunity, profile, diagnostics),
                StartInsideParent(opportunity), InteriorEndRelation.Invalid, null));
        }
        for (var candidateOrder = 0; candidateOrder < candidates.Count; candidateOrder++)
        {
            var candidate = candidates[candidateOrder];
            if (opportunity.Kind == OpportunityKind.LnInterior)
            {
                var duration = candidate.EndBeat - opportunity.Source.StartBeat;
                var contextDurations = context.Observations.Select(o => o.ReleaseBeat - o.HeadBeat).Order().ToArray();
                var median = contextDurations[(contextDurations.Length - 1) / 2];
                var upper = contextDurations[(int)Math.Floor((contextDurations.Length - 1) * .75)];
                if (duration <= median) stats.InteriorCandidatesShort++;
                else if (duration <= upper) stats.InteriorCandidatesMedium++;
                else stats.InteriorCandidatesLong++;
            }
            started = Stopwatch.GetTimestamp();
            var lanes = geometry.FindLegalLnLanes(opportunity.Source.StartBeat, candidate.EndBeat,
                (decimal)gap.GapBeats, stats, out var rejectedByOverlap, out var rejectedByGap);
            ObserveLegalLaneRatio(stats, lanes.Count, geometry.KeyCount);
            stats.GeometryMs += ElapsedMs(started);
            var diagnosticIndex = -1;
            if (diagnostics is not null)
            {
                started = Stopwatch.GetTimestamp();
                var validity = geometry.ExplainLnPlacement(opportunity.Source.StartBeat, candidate.EndBeat,
                    (decimal)gap.GapBeats, opportunity.Source.Object.StartTime, candidate.EndTime,
                    diagnostics.Provenance);
                diagnostics.AddFailureTicks(Stopwatch.GetTimestamp() - started);
                var explained = validity.LaneEvaluations.Where(x => x.IsValid).Select(x => x.Lane).ToArray();
                if (!lanes.SequenceEqual(explained)) throw new InvalidOperationException("Legacy and diagnostic LN legality diverged.");
                var paths = BuildCandidateEvidencePaths(context, candidate, diagnosticTimeline!, options, diagnostics);
                var contributions = BuildObservationContributions(paths, profile, context.SourceHeadBeat, options);
                var unique = paths.Select(x => x.ObservationId).Distinct().Count();
                var key = diagnostics.CandidateKey(opportunityKey!.Value, DiagnosticCandidateKind.LongNote,
                    candidateOrder, opportunity.Source.Object.StartTime, candidate.EndTime);
                var relation = ClassifyInteriorEnd(opportunity, candidate);
                diagnosticIndex = diagnostics.Record(new DecisionDiagnostic(opportunityKey.Value, key,
                    DiagnosticCandidateKind.LongNote, opportunity.Kind, opportunity.Order, candidateOrder,
                    opportunity.Source.Object.StartTime, candidate.EndTime, opportunity.Source.StartBeat,
                    candidate.EndBeat, candidate.Weight, lanes.Count == 0 ? LegacyDecisionOutcome.NoLegalLane
                        : LegacyDecisionOutcome.CandidateNotSelected, null, paths, unique,
                    Math.Max(0, paths.Length - unique), diagnostics.Certificate(key, paths, validity, true), validity,
                    ParentId(opportunity, diagnostics), AnchorKind(opportunity, profile, diagnostics),
                    StartInsideParent(opportunity), relation, null,
                    candidate.Evidence.DeduplicatedObservationWeight, contributions, "LegacyPathSum",
                    "DistinctObservationDistanceSumThenCandidateAffinity"));
            }
            var rejection = lanes.Count > 0 ? "none" : rejectedByOverlap && rejectedByGap ? "overlap+gap"
                : rejectedByOverlap ? "overlap" : rejectedByGap ? "gap" : "no-legal-lane";
            detailTrace?.AppendLine($"SHAPE endBeat={F((double)candidate.EndBeat)} endTime={candidate.EndTime} durationBeats={F((double)(candidate.EndBeat - opportunity.Source.StartBeat))} weight={F(candidate.Weight)} durationVotes={candidate.Evidence.DurationVotes} releaseVotes={candidate.Evidence.ReleaseVotes} exactAnchors={candidate.Evidence.ExactReleaseVotes} sourceAffinity={F(candidate.Evidence.SourceAffinity)} adjustmentMs={candidate.Evidence.SnapAdjustmentMilliseconds} legalLanes={lanes.Count} rejection={rejection}");
            if (lanes.Count == 0)
            {
                stats.LnCandidatesImpossible++;
                stats.LnCandidateRetries++;
                if (rejectedByOverlap) stats.LnShapeRejectedOverlap++;
                if (rejectedByGap) stats.LnShapeRejectedGap++;
                if (opportunity.Kind == OpportunityKind.LnInterior) stats.InteriorCandidatesImpossible++;
                continue;
            }
            resolved.Add(new ResolvedReleaseCandidate(candidate, lanes, diagnosticIndex));
        }
        if (resolved.Count == 0)
        {
            stats.LnSkipsDueToGeometry++;
            detailTrace?.AppendLine("LN skip: no candidate fits.\n");
            return null;
        }

        var selected = TakeWeighted(resolved, rng);
        var lane = selected.LegalLanes[rng.Next(selected.LegalLanes.Count)];
        if (diagnostics is not null && selected.DiagnosticIndex >= 0)
            diagnostics.MarkSelected(selected.DiagnosticIndex, lane, LegacyDecisionOutcome.Placed);
        var origin = opportunity.Kind == OpportunityKind.LnInterior
            ? AddedObjectOrigin.LnInteriorOpportunity : AddedObjectOrigin.HeadOpportunity;
        var obj = ManiaObject.Ln(lane, opportunity.Source.Object.StartTime, selected.Candidate.EndTime, true,
            opportunity.Source.Object.Sequence, origin);
        detailTrace?.AppendLine($"LN selected={selected.Candidate.EndTime}, placed lane={lane + 1}\n");
        return new TimedManiaObject(obj, opportunity.Source.StartBeat, selected.Candidate.EndBeat);
    }

    private static LocalLnContext BuildOriginalInteriorContext(AddNoteOpportunity opportunity,
        OriginalChartAnalysis analysis, AddNotesOptions options)
    {
        var parent = opportunity.ParentOriginalLn
            ?? throw new InvalidOperationException("An interior opportunity requires its original parent LN.");
        var anchor = opportunity.InteriorAnchor
            ?? throw new InvalidOperationException("An interior opportunity requires an original anchor.");
        var originals = ResolveOriginalInteriorContext(parent, anchor, analysis, options);
        if (originals.Count < options.InteriorMinimumContextLnCount)
            throw new InvalidOperationException("Interior original-only context was not validated during eligibility.");
        var observations = originals.Select(o => new LnObservation(o.Object, o.StartBeat, o.EndBeat)).ToArray();
        return new LocalLnContext(opportunity.Source.StartBeat, observations,
            observations.Min(o => o.ReleaseBeat - o.HeadBeat));
    }

    private static LocalLnContext BuildOriginalLnContext(TimedManiaObject source, OriginalChartAnalysis analysis,
        double windowBeats)
    {
        var observations = analysis.LongNotesNear(source.StartBeat, (decimal)windowBeats)
            .Select(o => new LnObservation(o.Object, o.StartBeat, o.EndBeat)).ToList();
        if (observations.Count == 0)
            throw new InvalidOperationException("No original LN context exists for this source.");
        var minimum = observations.Min(o => o.ReleaseBeat - o.HeadBeat);
        return new LocalLnContext(source.StartBeat, observations, minimum);
    }

    private static LocalLnContext BuildLegacyVirtualLnContext(TimedManiaObject source,
        OriginalChartAnalysis analysis, double windowBeats)
    {
        var observations = analysis.LongNotesNear(source.StartBeat, (decimal)windowBeats)
            .Select(o => new LnObservation(o.Object, o.StartBeat, o.EndBeat)).ToList();
        if (!observations.Any(o => o.Object.StartTime == source.Object.StartTime && o.Object.EndTime == source.Object.EndTime))
            observations.Add(new LnObservation(source.Object, source.StartBeat, source.EndBeat));
        var minimum = observations.Min(o => o.ReleaseBeat - o.HeadBeat);
        return new LocalLnContext(source.StartBeat, observations, minimum);
    }

    private static IReadOnlyList<ReleaseCandidate> BuildReleaseCandidates(TimedManiaObject source,
        LocalLnContext context, BeatTimeline timeline, AddNotesOptions options, bool applySourceAffinity = true,
        bool computeC1Weights = false)
    {
        var byEnd = new Dictionary<int, MutableCandidate>();
        foreach (var observation in context.Observations)
        {
            var distance = decimal.Abs(observation.HeadBeat - context.SourceHeadBeat);
            var distanceWeight = DistanceWeight((double)distance, options.DistanceDecayPerBeat, options.MinimumDistanceWeight);
            AddDuration(observation, context.SourceHeadBeat + observation.ReleaseBeat - observation.HeadBeat,
                distanceWeight);
            AddRelease(observation, distanceWeight);
        }

        foreach (var candidate in byEnd.Values)
        {
            if (applySourceAffinity && (Nearly(candidate.EndBeat - context.SourceHeadBeat, source.DurationBeats)
                || candidate.EndTime == source.Object.EndTime)
            ) {
                candidate.Weight *= options.SourceAffinityMultiplier;
                candidate.DeduplicatedWeight *= options.SourceAffinityMultiplier;
                candidate.SourceAffinity = options.SourceAffinityMultiplier;
            }
        }
        return byEnd.Values.Where(c => c.EndTime > source.Object.StartTime).OrderBy(c => c.EndTime)
            .Select(c => new ReleaseCandidate(c.EndBeat, c.EndTime, c.Weight,
                new CandidateEvidence(c.DurationVotes, c.ReleaseVotes, c.ExactReleaseVotes,
                    c.SourceAffinity, c.DistanceContribution, c.SnapAdjustmentMilliseconds,
                    c.Weight, computeC1Weights ? c.DeduplicatedWeight : c.Weight,
                    c.EvidencePathCount, computeC1Weights ? c.IndependentWitnessCount : c.EvidencePathCount,
                    computeC1Weights ? c.EvidencePathCount - c.IndependentWitnessCount : 0))).ToArray();

        void AddDuration(LnObservation observation, decimal intendedEndBeat, double weight)
        {
            var rawTime = timeline.ToTimeMilliseconds(intendedEndBeat);
            Add(observation, rawTime, weight, 1, 0, 0, intendedEndBeat);
        }

        void AddRelease(LnObservation observation, double weight) =>
            Add(observation, observation.Object.EndTime!.Value, weight, 0, 1, 1, observation.ReleaseBeat);

        void Add(LnObservation observation, int rawTime, double weight, int durationVotes, int releaseVotes,
            int exactReleaseVotes, decimal intendedEndBeat)
        {
            var endTime = options.MapRelativeSnapEnabled ? rawTime : timeline.SnapToSupportedDivision(rawTime);
            var endBeat = endTime == rawTime ? intendedEndBeat : timeline.ToBeatDecimal(endTime);
            if (endBeat - context.SourceHeadBeat + Epsilon < context.MinimumDurationBeats) return;
            if (!byEnd.TryGetValue(endTime, out var candidate))
                byEnd[endTime] = candidate = new MutableCandidate(endTime, endBeat);
            candidate.Weight += weight;
            candidate.DurationVotes += durationVotes;
            candidate.ReleaseVotes += releaseVotes;
            candidate.ExactReleaseVotes += exactReleaseVotes;
            candidate.DistanceContribution += weight;
            candidate.EvidencePathCount++;
            if (computeC1Weights
                && (candidate.Witnesses ??= []).Add(ObservationKey.From(observation.Object)))
            {
                candidate.DeduplicatedWeight += weight;
                candidate.IndependentWitnessCount++;
            }
            candidate.SnapAdjustmentMilliseconds += Math.Abs(endTime - rawTime);
        }
    }

    private static VerticalDensitySnapshot CalculateVerticalDensity(int keyCount, int columns,
        AddNotesOptions options)
    {
        if (keyCount is < 1 or > 18) throw new ArgumentOutOfRangeException(nameof(keyCount));
        if (columns is < 0 || columns > keyCount) throw new ArgumentOutOfRangeException(nameof(columns));
        var ratio = (double)columns / keyCount;
        var steps = options.VerticalDensityScaleMode == VerticalDensityScaleMode.AbsoluteLegacy
            ? Math.Max(0, columns - options.DensityGraceColumns)
            : Math.Max(0, (ratio - options.DensityGraceColumns / 7.0) / (1.0 / 7.0));
        return new VerticalDensitySnapshot(columns, ratio, steps,
            Math.Pow(options.DensityDecayPerColumn, steps));
    }

    private static IReadOnlyList<ArticulationReplacement> ResolveArticulations(
        IReadOnlyList<ArticulationIntent> intents, OriginalChartAnalysis analysis, LaneGeometryIndex geometry,
        LocalRetriggerGapAnalyzer retriggerAnalyzer, BeatTimeline timeline, AddNotesOptions options,
        IRandomSource rng, AddNotesStatistics stats, StringBuilder? trace, DecisionDiagnosticsBuilder? diagnostics,
        MapperEvidenceProfile profile)
    {
        var result = new List<ArticulationReplacement>();
        foreach (var group in intents.GroupBy(x => x.Opportunity.ParentOriginalLn!.Object.Sequence)
                     .OrderBy(x => x.Key))
        {
            var candidates = new List<ArticulationCandidate>();
            foreach (var intent in group.OrderBy(x => x.Opportunity.InteriorAnchor!.Time))
            {
                var opportunity = intent.Opportunity;
                var parent = opportunity.ParentOriginalLn!;
                var anchor = opportunity.InteriorAnchor!;
                if (!analysis.HeadTimes.Contains(anchor.Time))
                {
                    stats.RejectedAnchorNotHead++;
                    continue;
                }

                stats.ArticulationEligible++;
                var retrigger = retriggerAnalyzer.Resolve(parent.Object.Lane, anchor.Beat, options);
                if (retrigger is null)
                {
                    stats.RejectedNoRetriggerGap++;
                    continue;
                }

                var localContext = ResolveOriginalInteriorContext(parent, anchor, analysis, options);
                if (localContext.Count == 0)
                {
                    stats.RejectedNoRetriggerGap++;
                    continue;
                }
                var minimum = localContext.Min(x => x.DurationBeats);
                foreach (var retriggerGap in retrigger.SupportedGaps)
                {
                    var releaseBeat = anchor.Beat - retriggerGap;
                    if (releaseBeat - parent.StartBeat + Epsilon < minimum)
                    {
                        stats.RejectedLeftTooShort++;
                        continue;
                    }
                    if (parent.EndBeat - anchor.Beat + Epsilon < minimum)
                    {
                        stats.RejectedRightTooShort++;
                        continue;
                    }
                    if (!(parent.StartBeat < releaseBeat && releaseBeat < anchor.Beat
                        && anchor.Beat < parent.EndBeat)) continue;

                    var releaseTime = timeline.ToTimeMilliseconds(releaseBeat);
                    var exactReleaseVotes = analysis.ReleaseAnchors.Count(x => x.Time == releaseTime);
                    var geometryStart = Stopwatch.GetTimestamp();
                    var legal = geometry.CanReplaceWithArticulation(parent, releaseBeat, anchor.Beat, stats);
                    stats.GeometryMs += ElapsedMs(geometryStart);
                    if (!legal)
                    {
                        stats.RejectedGeometry++;
                        continue;
                    }
                    var weight = retrigger.EvidenceCount + exactReleaseVotes + (retrigger.UsedSameLane ? 1 : 0);
                    candidates.Add(new ArticulationCandidate(intent, releaseBeat, releaseTime, anchor.Beat,
                        anchor.Time, weight));
                    trace?.AppendLine($"ARTICULATION_CANDIDATE parent={parent.Object.StartTime}-{parent.Object.EndTime}@lane{parent.Object.Lane + 1} R={releaseTime} H={anchor.Time} gap={F((double)retriggerGap)} held={intent.HeldLnColumns}/{geometry.KeyCount} nonHeld={intent.NonHeldColumns} heldRatio={F(intent.HeldRatio)} sameLaneGap={retrigger.UsedSameLane} evidence={retrigger.EvidenceCount} weight={F(weight)}");
                }
            }

            ArticulationCandidate? selected = null;
            if (candidates.Count > 0)
            {
                selected = TakeWeightedArticulation(candidates, rng);
                var parentObject = selected.Intent.Opportunity.ParentOriginalLn!.Object;
                var left = ManiaObject.Ln(parentObject.Lane, parentObject.StartTime, selected.ReleaseTime, true,
                    parentObject.Sequence, AddedObjectOrigin.ArticulationReplacement);
                var right = ManiaObject.Ln(parentObject.Lane, selected.RepressTime, parentObject.EndTime!.Value, true,
                    parentObject.Sequence, AddedObjectOrigin.ArticulationReplacement);
                result.Add(new ArticulationReplacement(parentObject, left, right, selected.ReleaseTime,
                    selected.RepressTime));
                stats.ArticulationPlaced++;
                var competingIntents = candidates.Select(x => x.Intent.Opportunity.InteriorAnchor!.Time).Distinct().Count() - 1;
                if (competingIntents > 0) stats.RejectedParentCap += competingIntents;
                trace?.AppendLine($"ARTICULATION_PLACED parent={parentObject.StartTime}-{parentObject.EndTime}@lane{parentObject.Lane + 1} R={selected.ReleaseTime} H={selected.RepressTime}");
                if (options.MaxArticulationsPerOriginalLn <= 0) throw new InvalidOperationException();
            }
            if (diagnostics is not null)
            {
                foreach (var intent in group.OrderBy(x => x.Opportunity.InteriorAnchor!.Time))
                {
                    var parent = intent.Opportunity.ParentOriginalLn!;
                    var anchor = intent.Opportunity.InteriorAnchor!;
                    var failure = diagnostics.ClassifyNormalFailures(intent.DiagnosticKey!.Value);
                    var retriggerDetails = DecisionDiagnosticQueries.BuildRetriggerEvidence(profile,
                        parent.Object.Lane, anchor.Beat, options);
                    var legacyRetrigger = retriggerAnalyzer.Resolve(parent.Object.Lane, anchor.Beat, options);
                    diagnostics.AddArticulation(new ArticulationIntentDiagnostic(intent.DiagnosticKey.Value,
                        diagnostics.FindObservation(parent.Object)!.Id, anchor.Time, true, intent.NonHeldColumns,
                        intent.HeldLnColumns, intent.HeldRatio, failure.CandidateCount, failure.Classification,
                        failure.BlockedByOriginal, failure.Mixed,
                        selected?.Intent == intent ? "placed" : "skipped",
                        legacyRetrigger?.EvidenceCount ?? 0, retriggerDetails));
                }
            }
        }
        return result;
    }

    private static ArticulationCandidate TakeWeightedArticulation(IReadOnlyList<ArticulationCandidate> candidates,
        IRandomSource rng)
    {
        var total = candidates.Sum(x => x.Weight);
        var roll = rng.NextDouble() * total;
        var running = 0d;
        for (var i = 0; i < candidates.Count; i++)
        {
            running += candidates[i].Weight;
            if (roll < running || i == candidates.Count - 1) return candidates[i];
        }
        throw new InvalidOperationException();
    }

    private static IRandomSource DeriveArticulationRandom(IRandomSource rng, ManiaChart chart)
    {
        if (rng is IDerivableRandomSource derivable) return derivable.Derive(0x415254);
        var hash = 17;
        unchecked
        {
            hash = hash * 31 + chart.KeyCount;
            foreach (var item in chart.OriginalObjects)
            {
                hash = hash * 31 + item.StartTime;
                hash = hash * 31 + (item.EndTime ?? item.StartTime);
                hash = hash * 31 + item.Lane;
            }
        }
        return new SeededRandom(hash ^ 0x415254);
    }

    private static void ObserveLegalLaneRatio(AddNotesStatistics stats, int legalLanes, int keyCount)
    {
        stats.LegalLaneRatioSum += (double)legalLanes / keyCount;
        stats.LegalLaneRatioObservationCount++;
    }

    private static int RatioPercent(int part, int total) => (int)Math.Round(part * 100.0 / total,
        MidpointRounding.AwayFromZero);

    private static void Increment(Dictionary<int, int> values, int key) =>
        values[key] = values.GetValueOrDefault(key) + 1;

    private static ResolvedReleaseCandidate TakeWeighted(IReadOnlyList<ResolvedReleaseCandidate> candidates, IRandomSource rng)
    {
        var total = candidates.Sum(c => c.Candidate.Weight);
        var roll = rng.NextDouble() * total;
        var running = 0d;
        for (var i = 0; i < candidates.Count; i++)
        {
            running += candidates[i].Candidate.Weight;
            if (roll < running || i == candidates.Count - 1) return candidates[i];
        }
        throw new InvalidOperationException();
    }

    private static void AppendDensityTrace(StringBuilder? trace, AddNoteOpportunity opportunity, AddNotesOptions options,
        DensitySnapshot density, int occupiedColumns, int simultaneousHeadColumns, int heldLnColumns,
        double chordFactor, double effectiveChance)
    {
        if (trace is null || opportunity.Kind != OpportunityKind.LnInterior) return;
        trace.AppendLine($"OPPORTUNITY kind={opportunity.Kind} time={opportunity.Source.Object.StartTime} baseChance={F(options.Chance)}");
        trace.AppendLine($"DENSITY micro={F(density.MicroHeadDensity)} context={F(density.ContextHeadDensity)} ratio={F(density.DensityRatio)} span={F(density.ElevatedSpanBeats)} contextualFactor={F(density.ContextualFactor)}");
        trace.AppendLine($"CHORD mode={options.VerticalDensityMode} occupied={occupiedColumns} simultaneousHeads={simultaneousHeadColumns} heldLn={heldLnColumns} factor={F(chordFactor)} effectiveChance={F(effectiveChance)}");
    }

    private static void AppendPerformanceTrace(StringBuilder? trace, AddNotesStatistics stats)
    {
        if (trace is null) return;
        trace.AppendLine("PERFORMANCE");
        trace.AppendLine($"contextBuildMs={F(stats.ContextBuildMs)} candidateBuildMs={F(stats.CandidateBuildMs)} geometryMs={F(stats.GeometryMs)}");
        trace.AppendLine($"geometryChecks={stats.GeometryChecks} objectsExamined={stats.GeometryObjectsExamined} beatConversions={stats.BeatConversions}");
        trace.AppendLine($"lnCandidatesBuilt={stats.LnCandidatesBuilt} impossible={stats.LnCandidatesImpossible} lnGeometryChecks={stats.LnGeometryChecks}");
        trace.AppendLine($"interiorRejected short={stats.InteriorRejectedTooShort} context={stats.InteriorRejectedInsufficientLnContext} anchors={stats.InteriorRejectedNoSupportedAnchors} legacyLength={stats.InteriorRejectedRelativeLengthLegacy}");
        trace.AppendLine($"interiorCandidates short={stats.InteriorCandidatesShort} medium={stats.InteriorCandidatesMedium} long={stats.InteriorCandidatesLong} impossible={stats.InteriorCandidatesImpossible}");
        trace.AppendLine($"verticalMeans simultaneousHeads={F(stats.SimultaneousHeadColumnsMean)} heldLn={F(stats.HeldLnColumnsMean)}");
        trace.AppendLine($"verticalRatios head={F(stats.SimultaneousHeadRatioMean)} held={F(stats.HeldLnRatioMean)} nonHeldColumns={F(stats.NonHeldColumnsMean)} nonHeldRatio={F(stats.NonHeldRatioMean)} legalLaneRatio={F(stats.LegalLaneRatioMean)}");
        trace.AppendLine($"rice opportunities={stats.TapOpportunities} rolls={stats.TapSuccessfulRolls} placed={stats.TapPlaced} effective={F(stats.TapEffectiveChanceMean)} chord={F(stats.MeanChordFactorForTap)} contextual={F(stats.MeanContextualFactorForTap)} normal={stats.TapPlacedNormal} burst={stats.TapPlacedBurst} sustained={stats.TapPlacedSustained}");
        trace.AppendLine($"articulation intents={stats.InteriorBlockedByLnSaturation} eligible={stats.ArticulationEligible} placed={stats.ArticulationPlaced} addedInteractions={stats.AddedInteractions} pass1Ms={F(stats.Pass1Ms)} articulationMs={F(stats.ArticulationPassMs)}");
    }

    private static void AppendShadowTrace(StringBuilder? trace, DecisionDiagnostics diagnostics)
    {
        if (trace is null) return;
        trace.AppendLine("[SHADOW EVIDENCE]");
        trace.AppendLine($"diagnosticVersion={diagnostics.DecisionDiagnosticVersion} candidates={diagnostics.Summary.DiagnosticCandidateCount} witnessReferences={diagnostics.Summary.WitnessReferenceCount}");
        foreach (var decision in diagnostics.Decisions)
        {
            trace.AppendLine($"DiagnosticCandidateKey: {decision.CandidateKey}");
            trace.AppendLine($"legacyOutcome={decision.LegacyOutcome} legacyWeight={(decision.LegacyWeight is null ? "N/A" : F(decision.LegacyWeight.Value))} c1ShadowWeight={(decision.C1ShadowWeight is null ? "N/A" : F(decision.C1ShadowWeight.Value))} independentWitnesses={decision.IndependentWitnessCount} duplicatePaths={decision.DuplicateEvidencePaths} activeAggregation={decision.ActiveAggregationRule} c1ShadowAggregation={decision.C1ShadowAggregationRule}");
            trace.AppendLine($"[SHADOW HARD VALIDITY] valid={decision.HardValidity.IsValid} failures={string.Join('+', decision.HardValidity.FailureReasons)}");
        }
        trace.AppendLine("[SHADOW RESULT]");
        trace.AppendLine("Legacy decision unchanged: YES");
    }

    private static ImmutableArray<CandidateEvidencePath> BuildCandidateEvidencePaths(LocalLnContext context,
        ReleaseCandidate candidate, BeatTimeline timeline, AddNotesOptions options,
        DecisionDiagnosticsBuilder diagnostics)
    {
        var paths = new List<CandidateEvidencePath>();
        foreach (var observation in context.Observations)
        {
            var original = diagnostics.FindObservation(observation.Object);
            if (original is null) continue;
            var durationEndBeat = context.SourceHeadBeat + observation.ReleaseBeat - observation.HeadBeat;
            var durationRawTime = timeline.ToTimeMilliseconds(durationEndBeat);
            var durationEndTime = options.MapRelativeSnapEnabled
                ? durationRawTime : timeline.SnapToSupportedDivision(durationRawTime);
            if (durationEndTime == candidate.EndTime)
                paths.Add(new CandidateEvidencePath(original.Id, "Duration", EvidenceScope.Local));
            var releaseRawTime = observation.Object.EndTime!.Value;
            var releaseEndTime = options.MapRelativeSnapEnabled
                ? releaseRawTime : timeline.SnapToSupportedDivision(releaseRawTime);
            if (releaseEndTime == candidate.EndTime)
                paths.Add(new CandidateEvidencePath(original.Id, "ExactRelease", EvidenceScope.Local));
        }
        return paths.OrderBy(x => x.ObservationId.Value).ThenBy(x => x.EvidenceTag).ToImmutableArray();
    }

    private static ImmutableArray<ObservationAuthorityContribution> BuildObservationContributions(
        ImmutableArray<CandidateEvidencePath> paths, MapperEvidenceProfile profile, decimal sourceHeadBeat,
        AddNotesOptions options) => paths.GroupBy(x => x.ObservationId).OrderBy(x => x.Key.Value)
        .Select(group =>
        {
            var observation = profile.Observations[group.Key.Value];
            var distance = decimal.Abs(observation.StartBeat - sourceHeadBeat);
            var distanceWeight = DistanceWeight((double)distance, options.DistanceDecayPerBeat,
                options.MinimumDistanceWeight);
            var labels = group.Select(x => x.EvidenceTag).Distinct().Order().ToImmutableArray();
            return new ObservationAuthorityContribution(group.Key, distanceWeight,
                distanceWeight * group.Count(), distanceWeight, labels);
        }).ToImmutableArray();

    private static OriginalObservationId? ParentId(AddNoteOpportunity opportunity,
        DecisionDiagnosticsBuilder diagnostics) => opportunity.ParentOriginalLn is null
        ? null : diagnostics.FindObservation(opportunity.ParentOriginalLn.Object)?.Id;

    private static InteriorAnchorKind AnchorKind(AddNoteOpportunity opportunity, MapperEvidenceProfile profile,
        DecisionDiagnosticsBuilder diagnostics)
    {
        if (opportunity.ParentOriginalLn is null || opportunity.InteriorAnchor is null) return InteriorAnchorKind.None;
        var parentId = diagnostics.FindObservation(opportunity.ParentOriginalLn.Object)?.Id;
        return parentId is null ? InteriorAnchorKind.None : profile.InteriorAnchorObservations
            .FirstOrDefault(x => x.ParentLongNoteId == parentId && x.AnchorTime == opportunity.InteriorAnchor.Time)?.Kind
            ?? InteriorAnchorKind.None;
    }

    private static bool? StartInsideParent(AddNoteOpportunity opportunity) => opportunity.ParentOriginalLn is null
        ? null : opportunity.ParentOriginalLn.StartBeat < opportunity.Source.StartBeat
            && opportunity.Source.StartBeat < opportunity.ParentOriginalLn.EndBeat;

    private static InteriorEndRelation ClassifyInteriorEnd(AddNoteOpportunity opportunity,
        ReleaseCandidate candidate)
    {
        if (opportunity.ParentOriginalLn is null) return InteriorEndRelation.NotApplicable;
        if (candidate.EndBeat <= opportunity.Source.StartBeat) return InteriorEndRelation.Invalid;
        if (candidate.EndBeat < opportunity.ParentOriginalLn.EndBeat) return InteriorEndRelation.Contained;
        if (candidate.EndBeat == opportunity.ParentOriginalLn.EndBeat) return InteriorEndRelation.EqualEnd;
        return InteriorEndRelation.Crossing;
    }

    private static double DistanceWeight(double distance, double decayPerBeat, double minimumWeight)
    {
        if (distance <= 0.000001) return 1.0;
        var band = Math.Ceiling(distance - 0.000001);
        return Math.Max(minimumWeight, 1.0 - decayPerBeat * band);
    }

    private static bool InRange(int time, AddNotesOptions options) =>
        (options.StartMs is null || time >= options.StartMs) && (options.EndMs is null || time <= options.EndMs);
    private static bool Nearly(decimal a, decimal b) => decimal.Abs(a - b) <= 0.001m;
    private static double ElapsedMs(long start) => Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    private static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static AddNotesStatistics NewStatistics(IReadOnlyList<ManiaObject> originals) => new()
    {
        InputObjects = originals.Count,
        OriginalTaps = originals.Count(o => o.Type == ManiaObjectType.Tap),
        OriginalLongNotes = originals.Count(o => o.Type == ManiaObjectType.LongNote)
    };

    private static void Validate(ManiaChart chart, AddNotesOptions options, IRandomSource rng)
    {
        ArgumentNullException.ThrowIfNull(chart);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(rng);
        if (options.Chance is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(options.Chance));
        if (options.LnWindowBeats <= 0 || options.MicroDensityWindowBeats <= 0 || options.ContextDensityWindowBeats <= 0)
            throw new ArgumentOutOfRangeException(nameof(options.LnWindowBeats));
        if (options.SourceAffinityMultiplier <= 0 || options.MinimumDistanceWeight is <= 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(options.SourceAffinityMultiplier));
        if (options.DistanceDecayPerBeat is < 0 or > 1 || options.DensityDecayPerColumn is <= 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(options.DensityDecayPerColumn));
        if (options.DensityGraceColumns < 0 || options.BurstDensityRatioThreshold <= 1)
            throw new ArgumentOutOfRangeException(nameof(options.DensityGraceColumns));
        if (options.BurstSpanOneBeatFactor is <= 0 or > 1 || options.BurstSpanTwoBeatFactor is <= 0 or > 1
            || options.BurstSpanThreeBeatFactor is <= 0 or > 1) throw new ArgumentOutOfRangeException(nameof(options.BurstSpanOneBeatFactor));
        if (options.LaneGapWindowBeats <= 0 || options.LaneGapMinimumSupport < 1 || options.FallbackMinimumLaneGapBeats < 0)
            throw new ArgumentOutOfRangeException(nameof(options.LaneGapWindowBeats));
        if (options.MaxInteriorOpportunitiesPerSource < 0 || options.InteriorMinimumSourceBeats <= 0
            || options.InteriorLengthRatio <= 0 || options.InteriorAbsoluteLongBeats <= 0
            || options.InteriorMinimumContextLnCount < 1 || options.InteriorMinimumSupportedAnchors < 1
            || options.MaxInteriorOpportunitiesPerSource > 2) throw new ArgumentOutOfRangeException(nameof(options.MaxInteriorOpportunitiesPerSource));
        if (options.ArticulationMaxNonHeldColumns < 0 || options.MaxArticulationsPerOriginalLn != 1
            || options.RetriggerGapWindowBeats <= 0 || options.RetriggerGapMinimumSupport < 1)
            throw new ArgumentOutOfRangeException(nameof(options.MaxArticulationsPerOriginalLn));
        if (options.StartMs is not null && options.EndMs is not null && options.StartMs > options.EndMs)
            throw new ArgumentException("start-ms must be less than or equal to end-ms.");
    }

    private static void ValidateEvidenceProfile(ManiaChart chart, MapperEvidenceProfile profile)
    {
        if (profile.EvidenceProfileVersion != MapperEvidenceProfileBuilder.EvidenceProfileVersion
            || profile.KeyCount != chart.KeyCount
            || profile.OriginalObjectCount != chart.OriginalObjects.Count
            || profile.ChartFingerprint != MapperEvidenceProfileBuilder.ComputeFingerprint(chart))
            throw new ArgumentException("The prepared evidence profile does not match this chart or profile version.",
                nameof(profile));
    }

    private sealed record AddNoteOpportunity(TimedManiaObject Source, OpportunityKind Kind, int Order,
        TimedManiaObject? ParentOriginalLn = null, AnchorEvidence? InteriorAnchor = null);
    private sealed record AnchorEvidence(int Time, decimal Beat, int Count, string Reasons);
    private sealed record RankedAnchor(AnchorEvidence Anchor, IReadOnlyList<TimedManiaObject> Context, int Score);
    private sealed record ArticulationIntent(AddNoteOpportunity Opportunity, int HeldLnColumns,
        int NonHeldColumns, double HeldRatio, OpportunityDiagnosticKey? DiagnosticKey);
    private sealed record ArticulationCandidate(ArticulationIntent Intent, decimal ReleaseBeat, int ReleaseTime,
        decimal RepressBeat, int RepressTime, double Weight);
    private sealed class MutableCandidate(int endTime, decimal endBeat)
    {
        public int EndTime { get; } = endTime;
        public decimal EndBeat { get; } = endBeat;
        public double Weight { get; set; }
        public double DeduplicatedWeight { get; set; }
        public int DurationVotes { get; set; }
        public int ReleaseVotes { get; set; }
        public int ExactReleaseVotes { get; set; }
        public double SourceAffinity { get; set; } = 1.0;
        public double DistanceContribution { get; set; }
        public int SnapAdjustmentMilliseconds { get; set; }
        public int EvidencePathCount { get; set; }
        public int IndependentWitnessCount { get; set; }
        public HashSet<ObservationKey>? Witnesses { get; set; }
    }

    private readonly record struct ObservationKey(int Sequence, int Lane, ManiaObjectType Type, int StartTime,
        int? EndTime)
    {
        public static ObservationKey From(ManiaObject value) =>
            new(value.Sequence, value.Lane, value.Type, value.StartTime, value.EndTime);
    }
}

public sealed record LnObservation(ManiaObject Object, decimal HeadBeat, decimal ReleaseBeat);
public sealed record LocalLnContext(decimal SourceHeadBeat, IReadOnlyList<LnObservation> Observations, decimal MinimumDurationBeats);
public sealed record CandidateEvidence(int DurationVotes, int ReleaseVotes, int ExactReleaseVotes,
    double SourceAffinity, double DistanceContribution, int SnapAdjustmentMilliseconds,
    double LegacyWeight, double DeduplicatedObservationWeight, int EvidencePathCount,
    int IndependentWitnessCount, int DuplicatePathCount);
public sealed record ReleaseCandidate(decimal EndBeat, int EndTime, double Weight, CandidateEvidence Evidence);
public sealed record ResolvedReleaseCandidate(ReleaseCandidate Candidate, IReadOnlyList<int> LegalLanes,
    int DiagnosticIndex = -1);
