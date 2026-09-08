using System.Reflection;
using ManiaAddNotesLab.Core;

namespace ManiaAddNotesLab.Tests;

public sealed class PhaseF23QuantizationDomainGroundTruthTests
{
    [Fact]
    public void DescriptorSerializesSourceHashCircularityAndGroundTruthRelationship()
    {
        var descriptor = QuantizationHypothesisDomainDescriptor.Create(Domain("manual", .5m),
            QuantizationDomainSourceKind.ManualResearchDomain, "Declared by the experiment",
            DomainCircularityStatus.NonCircular, "Coordinates are supplied before evaluation",
            DomainGroundTruthRelationship.IndependentValidationPlanned,
            QuantizationDomainIntendedUse.MethodDemonstration);
        var json = QuantizationInferenceFeasibilityResearchJson.Serialize(descriptor);
        Assert.Contains("contentHash", json);
        Assert.Contains("manualResearchDomain", json);
        Assert.Contains("nonCircular", json);
        Assert.False(descriptor.MapperDerivedJustified);
    }

    [Fact]
    public void CircularDomainCannotMasqueradeAsMapperDerived()
    {
        Assert.Throws<ArgumentException>(() => QuantizationHypothesisDomainDescriptor.Create(
            Domain("circular", .5m), QuantizationDomainSourceKind.ChartDerived,
            "Denominators inferred from already quantized notes", DomainCircularityStatus.Circular,
            "It needs the inference it is intended to supply", DomainGroundTruthRelationship.SameSourceAsDomain,
            QuantizationDomainIntendedUse.ObservationOnly, mapperDerivedJustified: true));
    }

    [Theory]
    [InlineData(TypedGapTransitionKind.TapHeadToTapHead)]
    [InlineData(TypedGapTransitionKind.TapHeadToLongNoteHead)]
    [InlineData(TypedGapTransitionKind.LongNoteReleaseToTapHead)]
    [InlineData(TypedGapTransitionKind.LongNoteReleaseToLongNoteHead)]
    public void CompleteTruthRepresentsBothEndpointsAndGapForEveryTransition(
        TypedGapTransitionKind transition)
    {
        var release = transition is TypedGapTransitionKind.LongNoteReleaseToTapHead
            or TypedGapTransitionKind.LongNoteReleaseToLongNoteHead;
        var truth = Truth("all-kinds", transition, .5m, 1m,
            sourceLnHead: release ? 0m : null);
        Assert.Equal(.5m, truth.SourceLatentBeat);
        Assert.Equal(1m, truth.DestinationLatentBeat);
        Assert.Equal(.5m, truth.LatentGapBeats);
        Assert.Equal(250, truth.SourceSerializedTimestamp);
        Assert.Equal(500, truth.DestinationSerializedTimestamp);
        Assert.Equal(transition, truth.TransitionKind);
        Assert.Equal(release, truth.SourceEndpointType == TypedGapEndpointType.LongNoteRelease);
        Assert.Equal(release ? 0 : null, truth.SourceLongNoteHeadSerializedTimestamp);
    }

    [Fact]
    public void ReleaseTruthRequiresAndSeparatesLongNoteHead()
    {
        Assert.Throws<ArgumentException>(() => Truth("missing-head",
            TypedGapTransitionKind.LongNoteReleaseToTapHead, .5m, 1m));
        var truth = Truth("release", TypedGapTransitionKind.LongNoteReleaseToTapHead, .5m, 1m, 0m);
        Assert.Equal(TypedGapEndpointType.LongNoteRelease, truth.SourceEndpointType);
        Assert.Equal(0m, truth.SourceLongNoteHeadLatentBeat);
        Assert.NotEqual(truth.SourceLongNoteHeadLatentBeat, truth.SourceLatentBeat);
    }

    [Fact]
    public void TimingChangeCrossingAndLnReleaseAreFullyRecorded()
    {
        var truth = Truth("cross-bpm", TypedGapTransitionKind.LongNoteReleaseToLongNoteHead,
            2.5m, 3m, 1.5m, timing: [new TimingPoint(0, 500), new TimingPoint(1000, 250)]);
        Assert.Equal(1125, truth.SourceSerializedTimestamp);
        Assert.Equal(1250, truth.DestinationSerializedTimestamp);
        Assert.Equal(750, truth.SourceLongNoteHeadSerializedTimestamp);
        Assert.Equal(.5m, truth.LatentGapBeats);
        Assert.Equal(2, truth.TimingMap.Length);
    }

    [Fact]
    public void RedundantRedlineIsPreservedInTruthProvenance()
    {
        var truth = Truth("redundant", TypedGapTransitionKind.TapHeadToTapHead, 1.5m, 2.5m,
            null, timing: [new TimingPoint(0, 500), new TimingPoint(1000, 500)]);
        Assert.Equal(2, truth.TimingMap.Length);
        Assert.Equal(750, truth.SourceSerializedTimestamp);
        Assert.Equal(1250, truth.DestinationSerializedTimestamp);
    }

    [Fact]
    public void TransitionValidationCanBeUniqueCorrect()
    {
        var result = Evaluate(Domain("correct", .5m, 1m),
            Truth("truth", TypedGapTransitionKind.TapHeadToTapHead, .5m, 1m));
        Assert.Equal(TransitionDomainValidationState.UniqueCorrect, result.State);
    }

    [Fact]
    public void TransitionValidationCanBeUniqueIncorrect()
    {
        var result = Evaluate(Domain("wrong", 250m / 499m, 498.6m / 499m),
            Truth499("truth", .5m, 1m));
        Assert.Equal(TransitionDomainValidationState.UniqueIncorrect, result.State);
    }

    [Fact]
    public void TransitionValidationCanBeAmbiguousAndContainTruth()
    {
        var result = Evaluate(Domain("ambiguous", .5m, 250m / 499m, 1m, 498.6m / 499m),
            Truth499("truth", .5m, 1m));
        Assert.Equal(TransitionDomainValidationState.AmbiguousContainsTruth, result.State);
    }

    [Fact]
    public void TransitionValidationCanBeAmbiguousAndExcludeTruth()
    {
        var result = Evaluate(Domain("misspecified", 249.6m / 499m, 250.4m / 499m,
                498.6m / 499m, 499.4m / 499m), Truth499("truth", .5m, 1m));
        Assert.Equal(TransitionDomainValidationState.AmbiguousExcludesTruth, result.State);
    }

    [Fact]
    public void TransitionValidationCanBeUnsupported()
    {
        var result = Evaluate(Domain("unsupported", .25m),
            Truth("truth", TypedGapTransitionKind.TapHeadToTapHead, .5m, 1m));
        Assert.Equal(TransitionDomainValidationState.UnsupportedUnderModel, result.State);
    }

    [Fact]
    public void DomainAndTruthSourcesAndIndependenceRemainSeparate()
    {
        var result = QuantizationDomainGroundTruthResearch.Evaluate(Domain("external", .5m, 1m),
            Truth("mapper", TypedGapTransitionKind.TapHeadToTapHead, .5m, 1m,
                truthSource: QuantizationTruthSourceKind.MapperAuthorAnnotation,
                truthLevel: QuantizationGroundTruthLevel.AuthorConfirmed),
            DomainTruthIndependenceStatus.Independent);
        Assert.Equal(QuantizationTruthSourceKind.MapperAuthorAnnotation, result.TruthSourceKind);
        Assert.Equal(DomainTruthIndependenceStatus.Independent, result.Independence);
        Assert.Equal("external", result.DomainId);
    }

    [Fact]
    public void TamperedSerializedTruthIsRejectedBeforeDomainValidation()
    {
        var truth = Truth("tampered", TypedGapTransitionKind.TapHeadToTapHead, .5m, 1m)
            with { DestinationSerializedTimestamp = 501 };
        Assert.Throws<InvalidDataException>(() => QuantizationDomainGroundTruthResearch.Evaluate(
            Domain("explicit", .5m, 1m), truth, DomainTruthIndependenceStatus.Independent));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(18)]
    public void ControlledTransitionTruthSupportsAllImplementedKeymodes(int keys)
    {
        var truth = QuantizationDomainGroundTruthResearch.CreateControlledTruth($"{keys}k", keys,
            TypedGapTransitionKind.TapHeadToTapHead, 0, .125m, [new TimingPoint(0, 500)],
            QuantizationTruthSourceKind.ProgrammaticSynthetic,
            QuantizationGroundTruthLevel.ControlledGeneration, "Synthetic multikey fixture");
        Assert.Equal(keys, truth.KeyCount);
        Assert.Equal(63, truth.DestinationSerializedTimestamp);
    }

    [Fact]
    public void RiceCloseIntervalsAreNotMergedWhenSerializationDistinguishesThem()
    {
        var left = Truth("rice-left", TypedGapTransitionKind.TapHeadToTapHead, 0, .124m);
        var right = Truth("rice-right", TypedGapTransitionKind.TapHeadToTapHead, 0, .125m);
        Assert.Equal(62, left.DestinationSerializedTimestamp);
        Assert.Equal(63, right.DestinationSerializedTimestamp);
        Assert.NotEqual(left.LatentGapBeats, right.LatentGapBeats);
    }

    [Fact]
    public void ResearchHasNoRandomOrGenerationDependency()
    {
        Assert.DoesNotContain(typeof(QuantizationDomainGroundTruthResearch).GetMethods(
                BindingFlags.Public | BindingFlags.Static).SelectMany(x => x.GetParameters()),
            x => typeof(IRandomSource).IsAssignableFrom(x.ParameterType));
        Assert.DoesNotContain(typeof(QuantizationDomainGroundTruthResearch).GetMethods(
                BindingFlags.Public | BindingFlags.Static).Select(x => x.ReturnType),
            x => x == typeof(AddNotesResult));
    }

    private static TransitionDomainValidationResult Evaluate(QuantizationHypothesisDomain domain,
        LabeledTransitionTruth truth) => QuantizationDomainGroundTruthResearch.Evaluate(domain, truth,
        DomainTruthIndependenceStatus.Independent);

    private static LabeledTransitionTruth Truth499(string id, decimal source, decimal destination) =>
        Truth(id, TypedGapTransitionKind.TapHeadToTapHead, source, destination, null,
            timing: [new TimingPoint(0, 499)]);

    private static LabeledTransitionTruth Truth(string id, TypedGapTransitionKind transition,
        decimal source, decimal destination, decimal? sourceLnHead = null,
        QuantizationTruthSourceKind truthSource = QuantizationTruthSourceKind.ProgrammaticSynthetic,
        QuantizationGroundTruthLevel truthLevel = QuantizationGroundTruthLevel.ControlledGeneration,
        params TimingPoint[] timing) => QuantizationDomainGroundTruthResearch.CreateControlledTruth(
            id, 4, transition, source, destination, timing.Length == 0 ? [new TimingPoint(0, 500)] : timing,
            truthSource, truthLevel, "Controlled test provenance", sourceLnHead);

    private static QuantizationHypothesisDomain Domain(string id, params decimal[] beats) => new(id,
        QuantizationHypothesisDomainSource.ExplicitExternalFiniteVocabulary,
        beats.Select((beat, index) => new LatentTimingCandidate($"candidate_{index}", beat)),
        "Explicit test domain independent from labels.");
}
