using System.Collections.Immutable;

namespace ManiaAddNotesLab.Core;

public enum QuantizationDomainSourceKind
{
    ExternalConvention,
    SyntheticGroundTruth,
    EditorMetadata,
    MapperAnnotated,
    FileDerived,
    ChartDerived,
    ManualResearchDomain,
    Unknown
}

public enum DomainCircularityStatus { NonCircular, Circular, Unknown }
public enum DomainGroundTruthRelationship
{
    None,
    SameSourceAsDomain,
    IndependentValidationPlanned,
    IndependentValidationAvailable,
    Unknown
}
public enum QuantizationDomainIntendedUse { SyntheticControl, MethodDemonstration, ValidationCandidate, ObservationOnly }

public sealed record QuantizationHypothesisDomainDescriptor(
    string Id,
    string ContentHash,
    string CanonicalizationVersion,
    QuantizationDomainSourceKind SourceKind,
    string SourceDescription,
    string Justification,
    int SemanticCandidateCount,
    DomainCircularityStatus Circularity,
    string CircularityReason,
    DomainGroundTruthRelationship GroundTruthRelationship,
    QuantizationDomainIntendedUse IntendedUse,
    bool MapperDerivedJustified)
{
    public static QuantizationHypothesisDomainDescriptor Create(QuantizationHypothesisDomain domain,
        QuantizationDomainSourceKind sourceKind, string sourceDescription,
        DomainCircularityStatus circularity, string circularityReason,
        DomainGroundTruthRelationship groundTruthRelationship,
        QuantizationDomainIntendedUse intendedUse, bool mapperDerivedJustified = false)
    {
        ArgumentNullException.ThrowIfNull(domain);
        if (string.IsNullOrWhiteSpace(sourceDescription))
            throw new ArgumentException("A source description is required.", nameof(sourceDescription));
        if (string.IsNullOrWhiteSpace(circularityReason))
            throw new ArgumentException("A circularity reason is required.", nameof(circularityReason));
        if (circularity == DomainCircularityStatus.Circular && mapperDerivedJustified)
            throw new ArgumentException("A circular domain cannot be declared mapper-derived.",
                nameof(mapperDerivedJustified));
        return new(domain.Id, domain.ContentHash, QuantizationHypothesisDomain.CanonicalizationVersion,
            sourceKind, sourceDescription, domain.Justification, domain.Candidates.Length, circularity,
            circularityReason, groundTruthRelationship, intendedUse, mapperDerivedJustified);
    }
}

public enum QuantizationGroundTruthLevel
{
    NoLabel,
    RetrospectiveAnnotation,
    AuthorConfirmed,
    PreSerializationLatent,
    ControlledGeneration
}

public enum QuantizationTruthSourceKind
{
    UnlabeledHumanOsu,
    RetrospectiveHumanAnnotation,
    MapperAuthorAnnotation,
    EditorSourceExportPair,
    ProgrammaticSynthetic,
    Unknown
}

public enum DomainTruthIndependenceStatus
{
    Independent,
    DomainConstructedFromTruth,
    SameUnlabeledFile,
    Unknown
}

public sealed record LabeledTransitionTruth(
    string TruthId,
    int KeyCount,
    TypedGapEndpointType SourceEndpointType,
    TypedGapEndpointType DestinationEndpointType,
    decimal SourceLatentBeat,
    decimal DestinationLatentBeat,
    decimal LatentGapBeats,
    int SourceSerializedTimestamp,
    int DestinationSerializedTimestamp,
    TypedGapTransitionKind TransitionKind,
    ImmutableArray<TimingPoint> TimingMap,
    string TimingMapVersion,
    string SerializationModelVersion,
    decimal? SourceLongNoteHeadLatentBeat,
    int? SourceLongNoteHeadSerializedTimestamp,
    QuantizationTruthSourceKind TruthSourceKind,
    QuantizationGroundTruthLevel TruthLevel,
    string Provenance);

public enum TransitionDomainValidationState
{
    UniqueCorrect,
    UniqueIncorrect,
    AmbiguousContainsTruth,
    AmbiguousExcludesTruth,
    UnsupportedUnderModel,
    NotApplicable
}

public sealed record TransitionDomainValidationResult(
    string TruthId,
    string DomainId,
    string DomainContentHash,
    QuantizationTruthSourceKind TruthSourceKind,
    DomainTruthIndependenceStatus Independence,
    TransitionDomainValidationState State,
    QuantizationCompatibilitySet SourceCompatibility,
    QuantizationCompatibilitySet DestinationCompatibility);

/// <summary>
/// F2.3 research-only contracts for complete transition truth and domain provenance. No method
/// learns a domain, ranks one, or participates in generation.
/// </summary>
public static class QuantizationDomainGroundTruthResearch
{
    public const string ResearchSchemaVersion = "phase-f2-3-quantization-domain-ground-truth.1";

    public static LabeledTransitionTruth CreateControlledTruth(string truthId, int keyCount,
        TypedGapTransitionKind transitionKind, decimal sourceLatentBeat, decimal destinationLatentBeat,
        IEnumerable<TimingPoint> timingPoints, QuantizationTruthSourceKind truthSourceKind,
        QuantizationGroundTruthLevel truthLevel, string provenance,
        decimal? sourceLongNoteHeadLatentBeat = null)
    {
        if (string.IsNullOrWhiteSpace(truthId)) throw new ArgumentException("A truth id is required.", nameof(truthId));
        if (keyCount is < 1 or > 18) throw new ArgumentOutOfRangeException(nameof(keyCount));
        if (destinationLatentBeat < sourceLatentBeat)
            throw new ArgumentException("Destination latent beat cannot precede source latent beat.");
        if (string.IsNullOrWhiteSpace(provenance))
            throw new ArgumentException("Truth provenance is required.", nameof(provenance));
        var map = timingPoints?.ToImmutableArray() ?? throw new ArgumentNullException(nameof(timingPoints));
        var serializer = new QuantizationSerializationForwardModel(map);
        var (sourceType, destinationType) = Endpoints(transitionKind);
        if (sourceType == TypedGapEndpointType.LongNoteRelease && sourceLongNoteHeadLatentBeat is null)
            throw new ArgumentException("Release-origin truth requires the LN head coordinate.",
                nameof(sourceLongNoteHeadLatentBeat));
        if (sourceType != TypedGapEndpointType.LongNoteRelease && sourceLongNoteHeadLatentBeat is not null)
            throw new ArgumentException("Tap-origin truth cannot carry a source LN head coordinate.",
                nameof(sourceLongNoteHeadLatentBeat));
        if (sourceLongNoteHeadLatentBeat > sourceLatentBeat)
            throw new ArgumentException("An LN head cannot follow its release.", nameof(sourceLongNoteHeadLatentBeat));
        return new LabeledTransitionTruth(truthId, keyCount, sourceType, destinationType,
            sourceLatentBeat, destinationLatentBeat, destinationLatentBeat - sourceLatentBeat,
            serializer.Serialize(sourceLatentBeat), serializer.Serialize(destinationLatentBeat),
            transitionKind, map, serializer.TimingMapVersion,
            QuantizationSerializationForwardModel.ModelVersion, sourceLongNoteHeadLatentBeat,
            sourceLongNoteHeadLatentBeat is null ? null : serializer.Serialize(sourceLongNoteHeadLatentBeat.Value),
            truthSourceKind, truthLevel, provenance);
    }

    public static TransitionDomainValidationResult Evaluate(QuantizationHypothesisDomain domain,
        LabeledTransitionTruth truth, DomainTruthIndependenceStatus independence)
    {
        ArgumentNullException.ThrowIfNull(domain);
        ArgumentNullException.ThrowIfNull(truth);
        var serializer = new QuantizationSerializationForwardModel(truth.TimingMap);
        if (serializer.TimingMapVersion != truth.TimingMapVersion
            || QuantizationSerializationForwardModel.ModelVersion != truth.SerializationModelVersion)
            throw new InvalidDataException("Truth timing/serialization provenance does not match its timing map.");
        var expectedEndpoints = Endpoints(truth.TransitionKind);
        if (truth.SourceEndpointType != expectedEndpoints.Source
            || truth.DestinationEndpointType != expectedEndpoints.Destination)
            throw new InvalidDataException("Truth endpoint types do not match its transition kind.");
        if (truth.LatentGapBeats != truth.DestinationLatentBeat - truth.SourceLatentBeat)
            throw new InvalidDataException("Truth latent gap does not match its endpoint coordinates.");
        if (serializer.Serialize(truth.SourceLatentBeat) != truth.SourceSerializedTimestamp
            || serializer.Serialize(truth.DestinationLatentBeat) != truth.DestinationSerializedTimestamp)
            throw new InvalidDataException("Truth serialized endpoints do not match forward serialization.");
        if (truth.SourceEndpointType == TypedGapEndpointType.LongNoteRelease
            && (truth.SourceLongNoteHeadLatentBeat is null
                || truth.SourceLongNoteHeadSerializedTimestamp !=
                serializer.Serialize(truth.SourceLongNoteHeadLatentBeat.Value)))
            throw new InvalidDataException("Release truth requires a consistent, separate LN head.");
        var source = QuantizationInferenceFeasibilityResearch.Evaluate(new QuantizationObservedEndpoint(
            $"{truth.TruthId}:source", truth.SourceSerializedTimestamp, truth.TransitionKind,
            truth.SourceEndpointType), domain, serializer);
        var destination = QuantizationInferenceFeasibilityResearch.Evaluate(new QuantizationObservedEndpoint(
            $"{truth.TruthId}:destination", truth.DestinationSerializedTimestamp, truth.TransitionKind,
            truth.DestinationEndpointType), domain, serializer);
        var sourceContains = Contains(source, truth.SourceLatentBeat);
        var destinationContains = Contains(destination, truth.DestinationLatentBeat);
        var state = Classify(source, destination, sourceContains, destinationContains);
        return new TransitionDomainValidationResult(truth.TruthId, domain.Id, domain.ContentHash,
            truth.TruthSourceKind, independence, state, source, destination);
    }

    private static TransitionDomainValidationState Classify(QuantizationCompatibilitySet source,
        QuantizationCompatibilitySet destination, bool sourceContains, bool destinationContains)
    {
        if (source.State == QuantizationCompatibilityState.InferenceNotApplicable
            || destination.State == QuantizationCompatibilityState.InferenceNotApplicable)
            return TransitionDomainValidationState.NotApplicable;
        if (source.State == QuantizationCompatibilityState.UnsupportedUnderModel
            || destination.State == QuantizationCompatibilityState.UnsupportedUnderModel)
            return TransitionDomainValidationState.UnsupportedUnderModel;
        var bothUnique = source.State == QuantizationCompatibilityState.UniqueUnderModel
                         && destination.State == QuantizationCompatibilityState.UniqueUnderModel;
        if (bothUnique)
            return sourceContains && destinationContains
                ? TransitionDomainValidationState.UniqueCorrect
                : TransitionDomainValidationState.UniqueIncorrect;
        return sourceContains && destinationContains
            ? TransitionDomainValidationState.AmbiguousContainsTruth
            : TransitionDomainValidationState.AmbiguousExcludesTruth;
    }

    private static bool Contains(QuantizationCompatibilitySet set, decimal beat) =>
        set.CompatibleHypotheses.Any(x => x.AssumedLatentBeatPosition == beat);

    private static (TypedGapEndpointType Source, TypedGapEndpointType Destination) Endpoints(
        TypedGapTransitionKind kind) => kind switch
    {
        TypedGapTransitionKind.TapHeadToTapHead =>
            (TypedGapEndpointType.TapHead, TypedGapEndpointType.TapHead),
        TypedGapTransitionKind.TapHeadToLongNoteHead =>
            (TypedGapEndpointType.TapHead, TypedGapEndpointType.LongNoteHead),
        TypedGapTransitionKind.LongNoteReleaseToTapHead =>
            (TypedGapEndpointType.LongNoteRelease, TypedGapEndpointType.TapHead),
        _ => (TypedGapEndpointType.LongNoteRelease, TypedGapEndpointType.LongNoteHead)
    };
}
