using System.Reflection;
using ManiaAddNotesLab.Core;

namespace ManiaAddNotesLab.Tests;

public sealed class PhaseF22QuantizationInferenceFeasibilityTests
{
    [Fact]
    public void ForwardSerializerHandlesIntegralPosition() => Assert.Equal(500, Model(500).Serialize(1));

    [Fact]
    public void ForwardSerializerUsesAwayFromZeroAtFractionalMillisecond() =>
        Assert.Equal(250, Model(499).Serialize(.5m));

    [Fact]
    public void ForwardSerializerHandlesTimingPointOffset()
    {
        var model = Model(new TimingPoint(100, 500));
        Assert.Equal(100, model.Serialize(0));
        Assert.Equal(350, model.Serialize(.5m));
    }

    [Fact]
    public void ForwardSerializerUsesPositiveRedlinesAcrossBpmChange()
    {
        var model = Model(new TimingPoint(0, 500), new TimingPoint(1000, 250));
        Assert.Equal(1000, model.Serialize(2));
        Assert.Equal(1250, model.Serialize(3));
    }

    [Fact]
    public void ForwardSerializerRepresentsLnReleaseAcrossTimingChange()
    {
        var model = Model(new TimingPoint(0, 500), new TimingPoint(1000, 250));
        var hold = model.SerializeInterval(1.5m, 3);
        Assert.Equal(750, hold.SourceTimestamp);
        Assert.Equal(1250, hold.DestinationTimestamp);
        Assert.Equal(1.5m, hold.LatentGapBeats);
    }

    [Fact]
    public void ForwardSerializerIgnoresInheritedNegativeTimingPoints()
    {
        var withSv = Model(new TimingPoint(0, 500), new TimingPoint(250, -50));
        Assert.Equal(500, withSv.Serialize(1));
    }

    [Fact]
    public void DistinctLatentWorldsCanHaveIdenticalObservableInputs()
    {
        var model = Model(499);
        var proof = QuantizationInferenceFeasibilityResearch.CertifyNonIdentifiability(
            "world_a_half", .5m, "world_b_exact_250ms", 250m / 499m, model);
        Assert.True(proof.LatentHistoriesDiffer);
        Assert.True(proof.ObservableInputsAreIdentical);
        Assert.Equal(250, proof.WorldASerializedTimestamp);
        Assert.Equal(proof.WorldASerializedTimestamp, proof.WorldBSerializedTimestamp);
        Assert.True(proof.DemonstratesNonIdentifiability);
    }

    [Fact]
    public void CompatibilityCanBeEmpty()
    {
        var set = Evaluate(250, Domain("empty-control", C("zero", 0), C("quarter", .25m)), Model(500));
        Assert.Equal(QuantizationCompatibilityState.UnsupportedUnderModel, set.State);
        Assert.Empty(set.CompatibleHypotheses);
    }

    [Fact]
    public void CompatibilityCanBeSingleton()
    {
        var set = Evaluate(250, Domain("finite-a", C("quarter", .25m), C("half", .5m)), Model(500));
        Assert.Equal(QuantizationCompatibilityState.UniqueUnderModel, set.State);
        Assert.Equal("half", Assert.Single(set.CompatibleHypotheses).CandidateId);
    }

    [Fact]
    public void CompatibilityPreservesMultipleCandidates()
    {
        var set = Evaluate(250, Domain("finite-b", C("half", .5m), C("exact", 250m / 499m)), Model(499));
        Assert.Equal(QuantizationCompatibilityState.AmbiguousUnderModel, set.State);
        Assert.Equal(2, set.CompatibleHypotheses.Length);
    }

    [Fact]
    public void CompatibilityIsOrderIndependent()
    {
        var model = Model(499);
        var left = Evaluate(250, Domain("d", C("half", .5m), C("exact", 250m / 499m)), model);
        var right = Evaluate(250, Domain("d", C("exact", 250m / 499m), C("half", .5m)), model);
        Assert.Equal(QuantizationInferenceFeasibilityResearchJson.Serialize(left),
            QuantizationInferenceFeasibilityResearchJson.Serialize(right));
    }

    [Fact]
    public void CompatibilityUsesExactSerializedEqualityOnly()
    {
        var set = Evaluate(249, Domain("no-epsilon", C("half", .5m)), Model(499));
        Assert.Equal(QuantizationCompatibilityState.UnsupportedUnderModel, set.State);
    }

    [Fact]
    public void NotApplicableAbstainsWithoutEnumeratingAWinner()
    {
        var observation = Observation(250) with { InferenceApplicable = false };
        var set = QuantizationInferenceFeasibilityResearch.Evaluate(observation,
            Domain("explicit", C("half", .5m)), Model(500));
        Assert.Equal(QuantizationCompatibilityState.InferenceNotApplicable, set.State);
        Assert.Empty(set.CompatibleHypotheses);
    }

    [Fact]
    public void AssumptionCertificateExposesDomainAndModelVersions()
    {
        var set = Evaluate(250, Domain("external-finite-v1", C("half", .5m)), Model(500));
        Assert.Equal("external-finite-v1", set.Assumptions.HypothesisDomainId);
        Assert.Equal(set.Assumptions.HypothesisDomainHash,
            Domain("external-finite-v1", C("half", .5m)).ContentHash);
        Assert.Equal(QuantizationHypothesisDomain.CanonicalizationVersion,
            set.Assumptions.HypothesisDomainCanonicalizationVersion);
        Assert.Equal(QuantizationHypothesisDomainSource.ExplicitExternalFiniteVocabulary,
            set.Assumptions.HypothesisDomainSource);
        Assert.Equal(QuantizationSerializationForwardModel.ModelVersion,
            set.Assumptions.SerializationModelVersion);
        Assert.NotEmpty(set.Assumptions.TimingMapVersion);
    }

    [Fact]
    public void DomainChangeMakesVocabularySensitivityVisible()
    {
        var model = Model(499);
        var narrow = Evaluate(250, Domain("narrow", C("half", .5m)), model);
        var expanded = Evaluate(250, Domain("expanded", C("half", .5m), C("exact", 250m / 499m)), model);
        Assert.Equal(QuantizationCompatibilityState.UniqueUnderModel, narrow.State);
        Assert.Equal(QuantizationCompatibilityState.AmbiguousUnderModel, expanded.State);
        Assert.NotEqual(narrow.Assumptions.HypothesisDomainId, expanded.Assumptions.HypothesisDomainId);
    }

    [Fact]
    public void NoHiddenDenominatorDomainExists()
    {
        Assert.DoesNotContain(typeof(QuantizationHypothesisDomain).GetConstructors(),
            constructor => constructor.GetParameters().All(parameter =>
                parameter.ParameterType != typeof(IEnumerable<LatentTimingCandidate>)));
        Assert.Throws<ArgumentNullException>(() => new QuantizationHypothesisDomain("x",
            QuantizationHypothesisDomainSource.ExplicitExternalFiniteVocabulary, null!, "explicit"));
    }

    [Fact]
    public void CircularDomainSourceIsExplicitlyMarked()
    {
        var domain = new QuantizationHypothesisDomain("circular",
            QuantizationHypothesisDomainSource.ObservedDenominatorsCircular, [C("half", .5m)],
            "Would require snaps to derive the snap vocabulary.");
        Assert.True(domain.IsCircular);
        Assert.True(Evaluate(250, domain, Model(500)).Assumptions.HypothesisDomainIsCircular);
    }

    [Fact]
    public void DomainHashIsOrderIndependentAndDeterministic()
    {
        var left = Domain("same", C("half", .5m), C("quarter", .25m));
        var right = Domain("same", C("quarter", .25m), C("half", .5m));
        Assert.Equal(left.ContentHash, right.ContentHash);
        Assert.Equal(left.ContentHash, Domain("same", C("half", .5m), C("quarter", .25m)).ContentHash);
    }

    [Fact]
    public void SameDomainIdWithChangedSemanticContentHasDifferentHash()
    {
        Assert.NotEqual(Domain("foo", C("half", .5m)).ContentHash,
            Domain("foo", C("half", .5m), C("quarter", .25m)).ContentHash);
    }

    [Fact]
    public void AliasOnlyChangesDoNotChangeSemanticContentHash()
    {
        Assert.Equal(Domain("left", C("half", .5m)).ContentHash,
            Domain("right", C("two_quarters", .5m)).ContentHash);
    }

    [Fact]
    public void DuplicateAliasesAtSameBeatDoNotCreateFakeAmbiguity()
    {
        var domain = Domain("aliases", C("half", .5m), C("two_quarters", .5m));
        var candidate = Assert.Single(domain.Candidates);
        Assert.Equal(new[] { "half", "two_quarters" }, candidate.Aliases);
        Assert.Equal(QuantizationCompatibilityState.UniqueUnderModel,
            Evaluate(250, domain, Model(500)).State);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DomainJustificationMustContainText(string? justification)
    {
        Assert.Throws<ArgumentException>(() => new QuantizationHypothesisDomain("invalid",
            QuantizationHypothesisDomainSource.ExplicitExternalFiniteVocabulary,
            [C("half", .5m)], justification!));
    }

    [Fact]
    public void ValidDomainJustificationIsPreserved()
    {
        var domain = new QuantizationHypothesisDomain("valid",
            QuantizationHypothesisDomainSource.ExplicitExternalFiniteVocabulary,
            [C("half", .5m)], "A declared external research condition.");
        Assert.Equal("A declared external research condition.", domain.Justification);
    }

    [Fact]
    public void SyntheticTruthCanBeUniqueCorrect()
    {
        var set = Evaluate(250, Domain("truth", C("half", .5m)), Model(500));
        Assert.Equal(SyntheticCompatibilityTruthState.UniqueCorrect,
            QuantizationInferenceFeasibilityResearch.EvaluateSyntheticTruth(set, "half"));
    }

    [Fact]
    public void SyntheticTruthCanBeAmbiguousAndContainTruth()
    {
        var set = Evaluate(250, Domain("truth", C("half", .5m), C("exact", 250m / 499m)), Model(499));
        Assert.Equal(SyntheticCompatibilityTruthState.AmbiguousContainsTruth,
            QuantizationInferenceFeasibilityResearch.EvaluateSyntheticTruth(set, "half"));
    }

    [Fact]
    public void SyntheticTruthCanBeAmbiguousAndExcludeTruth()
    {
        var set = Evaluate(250, Domain("misspecified", C("left", 249.6m / 499m),
            C("right", 250.4m / 499m)), Model(499));
        Assert.Equal(SyntheticCompatibilityTruthState.AmbiguousExcludesTruth,
            QuantizationInferenceFeasibilityResearch.EvaluateSyntheticTruth(set, "half"));
    }

    [Fact]
    public void SyntheticTruthCanExposeUniqueIncorrectModel()
    {
        var set = Evaluate(250, Domain("misspecified", C("wrong", 250m / 499m)), Model(499));
        Assert.Equal(SyntheticCompatibilityTruthState.UniqueIncorrect,
            QuantizationInferenceFeasibilityResearch.EvaluateSyntheticTruth(set, "half"));
    }

    [Fact]
    public void SameLatentIntervalCanHaveAlternatingSerializedMillisecondGaps()
    {
        var model = Model(499);
        var first = model.SerializeInterval(0, .5m);
        var second = model.SerializeInterval(.5m, 1m);
        Assert.Equal(first.LatentGapBeats, second.LatentGapBeats);
        Assert.Equal(250, first.SerializedGapMilliseconds);
        Assert.Equal(249, second.SerializedGapMilliseconds);
    }

    [Fact]
    public void RedundantRedlineChangesTraversalButNotForwardTimestamp()
    {
        var plain = Model(500);
        var redundant = Model(new TimingPoint(0, 500), new TimingPoint(1000, 500));
        Assert.Equal(plain.Serialize(2.5m), redundant.Serialize(2.5m));
        Assert.NotEqual(plain.TimingMapVersion, redundant.TimingMapVersion);
    }

    [Theory]
    [InlineData(TypedGapTransitionKind.TapHeadToTapHead)]
    [InlineData(TypedGapTransitionKind.TapHeadToLongNoteHead)]
    [InlineData(TypedGapTransitionKind.LongNoteReleaseToTapHead)]
    [InlineData(TypedGapTransitionKind.LongNoteReleaseToLongNoteHead)]
    public void AllTransitionKindsRemainDescriptiveContext(TypedGapTransitionKind kind)
    {
        var set = QuantizationInferenceFeasibilityResearch.Evaluate(
            new QuantizationObservedEndpoint("transition", 250, kind, TypedGapEndpointType.TapHead),
            Domain("explicit", C("half", .5m)), Model(500));
        Assert.Equal(kind, set.ObservedFileFact.TransitionKind);
        Assert.Equal(QuantizationCompatibilityState.UniqueUnderModel, set.State);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(18)]
    public void ResearchRepresentationIsKeymodeNeutral(int keys)
    {
        var chart = new ManiaChart { KeyCount = keys, Lines = [], TimingPoints = [new(0, 500)],
            OriginalObjects = [ManiaObject.Tap(0, 250)] };
        var set = Evaluate(chart.OriginalObjects[0].StartTime,
            Domain($"synthetic-{keys}k", C("half", .5m)),
            new QuantizationSerializationForwardModel(chart.TimingPoints));
        Assert.Equal(QuantizationCompatibilityState.UniqueUnderModel, set.State);
    }

    [Fact]
    public void RiceSentinelKeepsCloseNeighborDistinctUnderSyntheticTruth()
    {
        var model = Model(500);
        var stream = new[] { 0m, .125m, .25m, .375m }.Select(model.Serialize).ToArray();
        Assert.Equal(new[] { 0, 63, 125, 188 }, stream);
        Assert.NotEqual(model.Serialize(.125m), model.Serialize(.124m));
    }

    [Fact]
    public void SerializationContainsNoSelectorOrPatternAuthority()
    {
        var json = QuantizationInferenceFeasibilityResearchJson.Serialize(
            Evaluate(250, Domain("explicit", C("half", .5m)), Model(500)));
        foreach (var forbidden in new[] { "nearest", "epsilon", "confidence", "probability", "score",
                     "frequencyWeight", "bestCandidate", "jackPenalty", "trillClassifier", "ricePatternClassifier" })
            Assert.DoesNotContain(forbidden, json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResearchIsDeterministicAndHasNoRandomOrGenerationDependency()
    {
        var domain = Domain("explicit", C("half", .5m), C("quarter", .25m));
        var first = QuantizationInferenceFeasibilityResearchJson.Serialize(Evaluate(250, domain, Model(500)));
        var second = QuantizationInferenceFeasibilityResearchJson.Serialize(Evaluate(250, domain, Model(500)));
        Assert.Equal(first, second);
        Assert.DoesNotContain(typeof(QuantizationInferenceFeasibilityResearch).GetMethods(
                BindingFlags.Public | BindingFlags.Static).SelectMany(x => x.GetParameters()),
            x => typeof(IRandomSource).IsAssignableFrom(x.ParameterType));
        Assert.DoesNotContain(typeof(QuantizationInferenceFeasibilityResearch).Assembly.GetTypes()
                .Where(x => x == typeof(QuantizationInferenceFeasibilityResearch))
                .SelectMany(x => x.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)),
            x => x.FieldType == typeof(AddNotesEngine));
    }

    private static QuantizationCompatibilitySet Evaluate(int timestamp, QuantizationHypothesisDomain domain,
        QuantizationSerializationForwardModel model) => QuantizationInferenceFeasibilityResearch.Evaluate(
        Observation(timestamp), domain, model);
    private static QuantizationObservedEndpoint Observation(int timestamp) => new("observed", timestamp,
        TypedGapTransitionKind.TapHeadToTapHead, TypedGapEndpointType.TapHead);
    private static QuantizationHypothesisDomain Domain(string id, params LatentTimingCandidate[] candidates) =>
        new(id, QuantizationHypothesisDomainSource.ExplicitExternalFiniteVocabulary, candidates,
            "Explicit synthetic research condition; not mapper-derived.");
    private static LatentTimingCandidate C(string id, decimal beat) => new(id, beat);
    private static QuantizationSerializationForwardModel Model(decimal beatLength) =>
        Model(new TimingPoint(0, beatLength));
    private static QuantizationSerializationForwardModel Model(params TimingPoint[] points) => new(points);
}
