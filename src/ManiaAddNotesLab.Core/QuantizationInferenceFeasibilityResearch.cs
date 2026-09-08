using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ManiaAddNotesLab.Core;

public enum QuantizationHypothesisDomainSource
{
    ExplicitExternalFiniteVocabulary,
    SyntheticLatentTruth,
    ExactlyDerivedChartRelations,
    ObservedDenominatorsCircular,
    ExternalLabeledGroundTruth
}

public enum QuantizationCompatibilityState
{
    InferenceNotApplicable,
    UnsupportedUnderModel,
    UniqueUnderModel,
    AmbiguousUnderModel
}

public enum SyntheticCompatibilityTruthState
{
    UniqueCorrect,
    UniqueIncorrect,
    AmbiguousContainsTruth,
    AmbiguousExcludesTruth,
    UnsupportedUnderModel
}

public sealed record LatentTimingCandidate(
    string Id,
    decimal BeatPosition,
    ImmutableArray<string> Aliases = default);

/// <summary>An explicit, finite research assumption. There is deliberately no default vocabulary.</summary>
public sealed class QuantizationHypothesisDomain
{
    public const string CanonicalizationVersion = "latent-beat-coordinate.1";

    public QuantizationHypothesisDomain(string id, QuantizationHypothesisDomainSource source,
        IEnumerable<LatentTimingCandidate> candidates, string justification)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A domain id is required.", nameof(id));
        ArgumentNullException.ThrowIfNull(candidates);
        if (string.IsNullOrWhiteSpace(justification))
            throw new ArgumentException("A non-empty domain justification is required.", nameof(justification));
        var supplied = candidates.ToArray();
        if (supplied.Any(x => string.IsNullOrWhiteSpace(x.Id)))
            throw new ArgumentException("Every latent candidate requires a label.", nameof(candidates));
        var conflictingAliases = supplied.GroupBy(x => x.Id, StringComparer.Ordinal)
            .Where(group => group.Select(x => x.BeatPosition).Distinct().Count() > 1)
            .Select(group => group.Key).ToArray();
        if (conflictingAliases.Length > 0)
            throw new ArgumentException("A candidate label cannot identify multiple latent coordinates.",
                nameof(candidates));
        Id = id;
        Source = source;
        Justification = justification;
        Candidates = supplied.GroupBy(x => x.BeatPosition)
            .Select(group =>
            {
                var aliases = group.SelectMany(x => x.Aliases.IsDefaultOrEmpty
                        ? [x.Id] : x.Aliases.Add(x.Id))
                    .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToImmutableArray();
                return new LatentTimingCandidate(aliases[0], group.Key, aliases);
            })
            .OrderBy(x => x.BeatPosition).ThenBy(x => x.Id, StringComparer.Ordinal)
            .ToImmutableArray();
        var canonical = string.Join(";", Candidates.Select(x => CanonicalBeat(x.BeatPosition)));
        ContentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{CanonicalizationVersion}|{canonical}"))).ToLowerInvariant();
    }

    public string Id { get; }
    public QuantizationHypothesisDomainSource Source { get; }
    public string Justification { get; }
    public ImmutableArray<LatentTimingCandidate> Candidates { get; }
    public string ContentHash { get; }
    public bool IsCircular => Source == QuantizationHypothesisDomainSource.ObservedDenominatorsCircular;

    public static string SemanticIdentity(decimal beatPosition) => $"beat:{CanonicalBeat(beatPosition)}";
    private static string CanonicalBeat(decimal value) => value.ToString("G29", CultureInfo.InvariantCulture);
}

public sealed record QuantizationObservedEndpoint(
    string ObservationId,
    int SerializedTimestamp,
    TypedGapTransitionKind TransitionKind,
    TypedGapEndpointType EndpointType,
    bool InferenceApplicable = true);

public sealed record QuantizationInferenceAssumptionCertificate(
    string HypothesisDomainId,
    string HypothesisDomainHash,
    string HypothesisDomainCanonicalizationVersion,
    QuantizationHypothesisDomainSource HypothesisDomainSource,
    bool HypothesisDomainIsCircular,
    string SerializationModelVersion,
    string TimingMapVersion);

public sealed record QuantizationModelCompatibility(
    string CandidateId,
    string SemanticIdentity,
    ImmutableArray<string> CandidateAliases,
    decimal AssumedLatentBeatPosition,
    int ForwardSerializedTimestamp);

public sealed record QuantizationCompatibilitySet(
    QuantizationObservedEndpoint ObservedFileFact,
    QuantizationInferenceAssumptionCertificate Assumptions,
    QuantizationCompatibilityState State,
    ImmutableArray<QuantizationModelCompatibility> CompatibleHypotheses);

public sealed record QuantizationNonIdentifiabilityCertificate(
    string WorldAId,
    decimal WorldALatentBeatPosition,
    string WorldBId,
    decimal WorldBLatentBeatPosition,
    int WorldASerializedTimestamp,
    int WorldBSerializedTimestamp,
    bool LatentHistoriesDiffer,
    bool ObservableInputsAreIdentical,
    bool DemonstratesNonIdentifiability);

public sealed record SerializedInterval(decimal SourceBeat, decimal DestinationBeat,
    int SourceTimestamp, int DestinationTimestamp)
{
    public int SerializedGapMilliseconds => DestinationTimestamp - SourceTimestamp;
    public decimal LatentGapBeats => DestinationBeat - SourceBeat;
}

/// <summary>
/// Research-only forward serializer. It answers what an explicit latent beat coordinate would
/// serialize to; it does not infer that coordinate and never participates in generation.
/// </summary>
public sealed class QuantizationSerializationForwardModel
{
    public const string ModelVersion = "osu-integer-ms-away-from-zero.1";
    private readonly BeatTimeline _timeline;

    public QuantizationSerializationForwardModel(IEnumerable<TimingPoint> timingPoints)
    {
        ArgumentNullException.ThrowIfNull(timingPoints);
        var positive = timingPoints.Where(x => x.BeatLength > 0).ToArray();
        if (positive.Length == 0) throw new InvalidDataException("The timing map has no positive redline.");
        _timeline = new BeatTimeline(positive);
        var canonical = string.Join(";", positive.Select((x, order) => (x, order))
            .OrderBy(x => x.x.Time).ThenBy(x => x.order)
            .Select(x => $"{D(x.x.Time)}@{D(x.x.BeatLength)}"));
        TimingMapVersion = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
    }

    public string TimingMapVersion { get; }
    public int Serialize(decimal latentBeatPosition) => _timeline.ToTimeMilliseconds(latentBeatPosition);

    public SerializedInterval SerializeInterval(decimal sourceBeat, decimal destinationBeat) =>
        new(sourceBeat, destinationBeat, Serialize(sourceBeat), Serialize(destinationBeat));

    private static string D(decimal value) => value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// F2.2 feasibility model. Compatibility is exact forward equality. Cardinality is preserved;
/// the model has no nearest candidate, score, probability, confidence, frequency or selector.
/// </summary>
public static class QuantizationInferenceFeasibilityResearch
{
    public const string ResearchSchemaVersion = "phase-f2-2-quantization-feasibility.2";

    public static QuantizationCompatibilitySet Evaluate(QuantizationObservedEndpoint observation,
        QuantizationHypothesisDomain domain, QuantizationSerializationForwardModel serializer)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(domain);
        ArgumentNullException.ThrowIfNull(serializer);
        var certificate = new QuantizationInferenceAssumptionCertificate(domain.Id, domain.ContentHash,
            QuantizationHypothesisDomain.CanonicalizationVersion, domain.Source, domain.IsCircular,
            QuantizationSerializationForwardModel.ModelVersion, serializer.TimingMapVersion);
        if (!observation.InferenceApplicable)
            return new QuantizationCompatibilitySet(observation, certificate,
                QuantizationCompatibilityState.InferenceNotApplicable, []);
        var compatible = domain.Candidates.Select(candidate => new QuantizationModelCompatibility(
                candidate.Id, QuantizationHypothesisDomain.SemanticIdentity(candidate.BeatPosition),
                candidate.Aliases, candidate.BeatPosition, serializer.Serialize(candidate.BeatPosition)))
            .Where(x => x.ForwardSerializedTimestamp == observation.SerializedTimestamp)
            .OrderBy(x => x.AssumedLatentBeatPosition).ThenBy(x => x.CandidateId, StringComparer.Ordinal)
            .ToImmutableArray();
        var state = compatible.Length switch
        {
            0 => QuantizationCompatibilityState.UnsupportedUnderModel,
            1 => QuantizationCompatibilityState.UniqueUnderModel,
            _ => QuantizationCompatibilityState.AmbiguousUnderModel
        };
        return new QuantizationCompatibilitySet(observation, certificate, state, compatible);
    }

    public static SyntheticCompatibilityTruthState EvaluateSyntheticTruth(
        QuantizationCompatibilitySet compatibility, string latentTruthCandidateId)
    {
        ArgumentNullException.ThrowIfNull(compatibility);
        var contains = compatibility.CompatibleHypotheses.Any(x =>
            string.Equals(x.CandidateId, latentTruthCandidateId, StringComparison.Ordinal)
            || string.Equals(x.SemanticIdentity, latentTruthCandidateId, StringComparison.Ordinal)
            || x.CandidateAliases.Contains(latentTruthCandidateId, StringComparer.Ordinal));
        return compatibility.State switch
        {
            QuantizationCompatibilityState.UnsupportedUnderModel =>
                SyntheticCompatibilityTruthState.UnsupportedUnderModel,
            QuantizationCompatibilityState.UniqueUnderModel when contains =>
                SyntheticCompatibilityTruthState.UniqueCorrect,
            QuantizationCompatibilityState.AmbiguousUnderModel when contains =>
                SyntheticCompatibilityTruthState.AmbiguousContainsTruth,
            QuantizationCompatibilityState.UniqueUnderModel =>
                SyntheticCompatibilityTruthState.UniqueIncorrect,
            _ => SyntheticCompatibilityTruthState.AmbiguousExcludesTruth
        };
    }

    public static QuantizationNonIdentifiabilityCertificate CertifyNonIdentifiability(
        string worldAId, decimal worldABeat, string worldBId, decimal worldBBeat,
        QuantizationSerializationForwardModel serializer)
    {
        ArgumentNullException.ThrowIfNull(serializer);
        var a = serializer.Serialize(worldABeat);
        var b = serializer.Serialize(worldBBeat);
        var latentDifferent = worldABeat != worldBBeat;
        var observableSame = a == b;
        return new QuantizationNonIdentifiabilityCertificate(worldAId, worldABeat, worldBId, worldBBeat,
            a, b, latentDifferent, observableSame, latentDifferent && observableSame);
    }
}

public static class QuantizationInferenceFeasibilityResearchJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
}
