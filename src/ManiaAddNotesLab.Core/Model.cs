namespace ManiaAddNotesLab.Core;

public enum ManiaObjectType { Tap, LongNote }
public enum AddedObjectOrigin { None, HeadOpportunity, LnInteriorOpportunity, ArticulationReplacement }
public enum OpportunityKind { BaseHead, LnInterior }
public enum VerticalDensityMode { OccupiedColumnsLegacy, SimultaneousHeads }
public enum VerticalDensityScaleMode { AbsoluteLegacy, KeymodeRelative }
public enum ContextualDensityScaleMode { AbsoluteHeadCountLegacy, KeymodeRelative }
public enum InteriorEligibilityMode { Legacy, AnchorSupported }
public enum InteriorContextMode { LegacyVirtualSource, OriginalOnly }

public sealed record ManiaObject(
    int Lane,
    int StartTime,
    ManiaObjectType Type,
    int? EndTime,
    bool IsSynthetic = false,
    string? RawLine = null,
    int Sequence = 0,
    AddedObjectOrigin Origin = AddedObjectOrigin.None)
{
    public static ManiaObject Tap(int lane, int time, bool synthetic = false, int sequence = 0) =>
        new(lane, time, ManiaObjectType.Tap, null, synthetic, null, sequence);

    public static ManiaObject Ln(int lane, int start, int end, bool synthetic = false, int sequence = 0,
        AddedObjectOrigin origin = AddedObjectOrigin.None) =>
        new(lane, start, ManiaObjectType.LongNote, end, synthetic, null, sequence, origin);
}

public sealed record TimingPoint(decimal Time, decimal BeatLength);
public sealed record TimedManiaObject(ManiaObject Object, decimal StartBeat, decimal EndBeat)
{
    public decimal DurationBeats => EndBeat - StartBeat;
}

public sealed class ManiaChart
{
    public required int KeyCount { get; init; }
    public required IReadOnlyList<string> Lines { get; init; }
    public required IReadOnlyList<ManiaObject> OriginalObjects { get; init; }
    public required IReadOnlyList<TimingPoint> TimingPoints { get; init; }
    public IReadOnlyList<ManiaObject> AddedObjects { get; init; } = [];
    public IReadOnlyList<ArticulationReplacement> ArticulationReplacements { get; init; } = [];

    public IReadOnlyList<ManiaObject> AllObjects =>
    [
        .. OriginalObjects.Where(original => !ArticulationReplacements.Any(r => SameOriginal(r.ParentOriginalLn, original))),
        .. ArticulationReplacements.SelectMany(r => new[] { r.LeftSegment, r.RightSegment }),
        .. AddedObjects
    ];

    private static bool SameOriginal(ManiaObject left, ManiaObject right) => left.Sequence == right.Sequence
        && left.Lane == right.Lane && left.StartTime == right.StartTime && left.EndTime == right.EndTime;
}

public sealed record ArticulationReplacement(ManiaObject ParentOriginalLn, ManiaObject LeftSegment,
    ManiaObject RightSegment, int ReleaseTime, int RepressTime);

public sealed record AddNotesOptions
{
    public double Chance { get; init; } = 0.30;
    public int? StartMs { get; init; }
    public int? EndMs { get; init; }
    public double LnWindowBeats { get; init; } = 4.0;
    public double SourceAffinityMultiplier { get; init; } = 1.25;
    public double DistanceDecayPerBeat { get; init; } = 0.20;
    public double MinimumDistanceWeight { get; init; } = 0.20;
    public int DensityGraceColumns { get; init; } = 2;
    public double DensityDecayPerColumn { get; init; } = 0.65;
    public VerticalDensityMode VerticalDensityMode { get; init; } = VerticalDensityMode.SimultaneousHeads;
    public VerticalDensityScaleMode VerticalDensityScaleMode { get; init; } = VerticalDensityScaleMode.KeymodeRelative;
    public bool ContextualDensityNormalizationEnabled { get; init; } = true;
    public ContextualDensityScaleMode ContextualDensityScaleMode { get; init; } = ContextualDensityScaleMode.KeymodeRelative;
    public double MicroDensityWindowBeats { get; init; } = 1.0;
    public double ContextDensityWindowBeats { get; init; } = 4.0;
    public double BurstDensityRatioThreshold { get; init; } = 1.5;
    public double BurstSpanOneBeatFactor { get; init; } = 0.45;
    public double BurstSpanTwoBeatFactor { get; init; } = 0.60;
    public double BurstSpanThreeBeatFactor { get; init; } = 0.80;
    public bool UseLocalLaneGap { get; init; } = true;
    public double LaneGapWindowBeats { get; init; } = 4.0;
    public int LaneGapMinimumSupport { get; init; } = 2;
    public double FallbackMinimumLaneGapBeats { get; init; } = 0.125;
    public bool MapRelativeSnapEnabled { get; init; } = true;
    public bool InteriorLnOpportunitiesEnabled { get; init; }
    public int MaxInteriorOpportunitiesPerSource { get; init; } = 2;
    public double InteriorMinimumSourceBeats { get; init; } = 3.0;
    public double InteriorLengthRatio { get; init; } = 1.5;
    public double InteriorAbsoluteLongBeats { get; init; } = 8.0;
    public int InteriorMinimumContextLnCount { get; init; } = 3;
    public int InteriorMinimumSupportedAnchors { get; init; } = 2;
    public InteriorEligibilityMode InteriorEligibilityMode { get; init; } = InteriorEligibilityMode.AnchorSupported;
    public InteriorContextMode InteriorContextMode { get; init; } = InteriorContextMode.OriginalOnly;
    public bool ArticulationEnabled { get; init; }
    public int ArticulationMaxNonHeldColumns { get; init; } = 1;
    public int MaxArticulationsPerOriginalLn { get; init; } = 1;
    public double RetriggerGapWindowBeats { get; init; } = 4.0;
    public int RetriggerGapMinimumSupport { get; init; } = 2;
    public bool Trace { get; init; }
    public bool DiagnosticsEnabled { get; init; }
}

public sealed class AddNotesStatistics
{
    public int InputObjects { get; internal set; }
    public int OutputObjects { get; internal set; }
    public int OriginalTaps { get; internal set; }
    public int OriginalLongNotes { get; internal set; }
    public int AddedTaps { get; internal set; }
    public int AddedLongNotes { get; internal set; }
    public int TotalOpportunities { get; internal set; }
    public int SuccessfulProbabilityRolls { get; internal set; }
    public int PlacedObjects => AddedTaps + AddedLongNotes;
    public int FailedPlacements { get; internal set; }
    public int LnCandidateRetries { get; internal set; }
    public int LnSkipsDueToGeometry { get; internal set; }
    public int DensityAdjustedOpportunities { get; internal set; }
    public int ContextualDensityAdjustedOpportunities { get; internal set; }
    public double MeanEffectiveChance { get; internal set; }
    public int BaseHeadOpportunities { get; internal set; }
    public int InteriorLnOpportunities { get; internal set; }
    public int SuccessfulBaseRolls { get; internal set; }
    public int SuccessfulInteriorRolls { get; internal set; }
    public int AddedLongNotesFromHeadOpportunities { get; internal set; }
    public int AddedLongNotesFromInteriorOpportunities { get; internal set; }
    public int BaseLnOpportunityCount { get; internal set; }
    public int BaseLnSuccessfulRolls { get; internal set; }
    public int BaseLnPlaced { get; internal set; }
    public int InteriorRejectedTooShort { get; internal set; }
    public int InteriorRejectedInsufficientLnContext { get; internal set; }
    public int InteriorRejectedNoSupportedAnchors { get; internal set; }
    public int InteriorRejectedRelativeLengthLegacy { get; internal set; }
    public int InteriorCandidatesShort { get; internal set; }
    public int InteriorCandidatesMedium { get; internal set; }
    public int InteriorCandidatesLong { get; internal set; }
    public int InteriorCandidatesImpossible { get; internal set; }
    public int LnShapeRejectedGap { get; internal set; }
    public int LnShapeRejectedOverlap { get; internal set; }
    public int TapOpportunities { get; internal set; }
    public int TapSuccessfulRolls { get; internal set; }
    public int TapPlaced { get; internal set; }
    public int TapPlacedBurst { get; internal set; }
    public int TapPlacedSustained { get; internal set; }
    public int TapPlacedNormal { get; internal set; }
    internal double TapEffectiveChanceSum { get; set; }
    internal double TapChordFactorSum { get; set; }
    internal double TapContextualFactorSum { get; set; }
    public Dictionary<int, int> TapPlacedBySimultaneousHeadCount { get; } = [];
    public Dictionary<int, int> TapPlacedByHeadRatioPercent { get; } = [];
    public Dictionary<int, int> TapPlacedByHeldLnColumns { get; } = [];
    public Dictionary<int, int> TapPlacedByHeldLnRatioPercent { get; } = [];
    public int InteriorFreeLanePlaced => AddedLongNotesFromInteriorOpportunities;
    public int InteriorNoFreeLane { get; internal set; }
    public int InteriorBlockedByLnSaturation { get; internal set; }
    public int ArticulationEligible { get; internal set; }
    public int ArticulationPlaced { get; internal set; }
    public int RejectedAnchorNotHead { get; internal set; }
    public int RejectedNotSaturated { get; internal set; }
    public int RejectedNoRetriggerGap { get; internal set; }
    public int RejectedLeftTooShort { get; internal set; }
    public int RejectedRightTooShort { get; internal set; }
    public int RejectedParentCap { get; internal set; }
    public int RejectedGeometry { get; internal set; }
    public int RejectedAlreadyModified { get; internal set; }
    public int ArticulatedParents => ArticulationPlaced;
    internal double InteriorEffectiveChanceSum { get; set; }
    internal long SimultaneousHeadColumnsTotal { get; set; }
    internal long HeldLnColumnsTotal { get; set; }
    internal double SimultaneousHeadRatioSum { get; set; }
    internal double HeldLnRatioSum { get; set; }
    internal double NonHeldColumnsSum { get; set; }
    internal double NonHeldRatioSum { get; set; }
    internal int VerticalDensityObservationCount { get; set; }
    internal double LegalLaneRatioSum { get; set; }
    internal int LegalLaneRatioObservationCount { get; set; }
    public long GeometryChecks { get; internal set; }
    public long GeometryObjectsExamined { get; internal set; }
    public long LnGeometryChecks { get; internal set; }
    public long BeatConversions { get; internal set; }
    public int LnCandidatesBuilt { get; internal set; }
    public int LnCandidatesImpossible { get; internal set; }
    public double ContextBuildMs { get; internal set; }
    public double CandidateBuildMs { get; internal set; }
    public double GeometryMs { get; internal set; }
    public double WriterMs { get; set; }
    public double Pass1Ms { get; internal set; }
    public double ArticulationPassMs { get; internal set; }
    public double ProfileBuildMs { get; internal set; }
    public long ProfileEstimatedSizeBytes { get; internal set; }
    public int ProfileObservationCount { get; internal set; }
    public int ProfileRelationCount { get; internal set; }
    public double CertificateBuildMs { get; internal set; }
    public double FailureDiagnosticMs { get; internal set; }
    public int DiagnosticCandidateCount { get; internal set; }
    public int CandidateLaneDiagnosticCount { get; internal set; }
    public int WitnessReferenceCount { get; internal set; }
    public long DetailedExportBytes { get; set; }
    public double OriginalLnRatio => Ratio(OriginalLongNotes, InputObjects);
    public double AddedLnRatio => Ratio(AddedLongNotes + ArticulationPlaced, PlacedObjects + ArticulationPlaced);
    public double ResultLnRatio => Ratio(OriginalLongNotes + AddedLongNotes + ArticulationPlaced, OutputObjects);
    public double InteriorMeanEffectiveChance => Ratio(InteriorEffectiveChanceSum, InteriorLnOpportunities);
    public double SimultaneousHeadColumnsMean => Ratio(SimultaneousHeadColumnsTotal, VerticalDensityObservationCount);
    public double HeldLnColumnsMean => Ratio(HeldLnColumnsTotal, VerticalDensityObservationCount);
    public double SimultaneousHeadRatioMean => Ratio(SimultaneousHeadRatioSum, VerticalDensityObservationCount);
    public double HeldLnRatioMean => Ratio(HeldLnRatioSum, VerticalDensityObservationCount);
    public double NonHeldColumnsMean => Ratio(NonHeldColumnsSum, VerticalDensityObservationCount);
    public double NonHeldRatioMean => Ratio(NonHeldRatioSum, VerticalDensityObservationCount);
    public double LegalLaneRatioMean => Ratio(LegalLaneRatioSum, LegalLaneRatioObservationCount);
    public double TapEffectiveChanceMean => Ratio(TapEffectiveChanceSum, TapOpportunities);
    public double TapPlacedPer100OriginalTaps => Ratio(TapPlaced * 100.0, OriginalTaps);
    public double MeanChordFactorForTap => Ratio(TapChordFactorSum, TapOpportunities);
    public double MeanContextualFactorForTap => Ratio(TapContextualFactorSum, TapOpportunities);
    public int AddedHeads => AddedTaps + AddedLongNotes + ArticulationPlaced;
    public int AddedReleases => AddedLongNotes + ArticulationPlaced;
    public int AddedInteractions => AddedHeads + AddedReleases;
    private static double Ratio(int part, int total) => total == 0 ? 0 : (double)part / total;
    private static double Ratio(double part, int total) => total == 0 ? 0 : part / total;
}

public sealed record DensitySnapshot(double MicroHeadDensity, double ContextHeadDensity, double DensityRatio,
    double ElevatedSpanBeats, double ContextualFactor);

public sealed record VerticalDensitySnapshot(int Columns, double Ratio, double EquivalentPenaltySteps,
    double ChordFactor);

public sealed record LaneGapDecision(double GapBeats, bool IsLocal, int EvidenceCount);

public sealed record AddNotesResult(ManiaChart ModifiedChart, AddNotesStatistics Statistics, string? Trace,
    MapperEvidenceProfile EvidenceProfile, DecisionDiagnostics? DecisionDiagnostics);

public interface IRandomSource
{
    double NextDouble();
    int Next(int maxExclusive);
}

public interface IDerivableRandomSource : IRandomSource
{
    IRandomSource Derive(int salt);
}

public sealed class SeededRandom(int seed) : IDerivableRandomSource
{
    private readonly Random _random = new(seed);
    public double NextDouble() => _random.NextDouble();
    public int Next(int maxExclusive) => _random.Next(maxExclusive);
    public IRandomSource Derive(int salt) => new SeededRandom(unchecked((seed * 16777619) ^ salt ^ 0x41A7));
}
