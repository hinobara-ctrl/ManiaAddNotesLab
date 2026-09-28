using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ManiaAddNotesLab.Core;

namespace ManiaAddNotesLab.Tests;

public sealed class Lane0CorrectivePreparationTests
{
    private const string ContractPath = "docs/lane_0_corrective_evaluation_preregistration_contract.json";

    [Fact]
    public void CorrectivePreregistrationPreservesFrozenQuestionPopulationsAndStops()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(FindRepositoryRoot(), ContractPath)));
        var artifact = document.RootElement;
        var contract = artifact.GetProperty("contract");

        Assert.Equal("lane-0-corrective-evaluation-preregistration.1",
            contract.GetProperty("schemaVersion").GetString());
        Assert.Equal("f40e4d3e4be29909c54be33528d4e167f4593dbe",
            contract.GetProperty("preparationBaselineHead").GetString());
        Assert.Equal("PENDING_LIVE_GITHUB_VERIFICATION",
            contract.GetProperty("remoteVerification").GetString());
        Assert.Equal("lane-0-future-held-counter-closure.3",
            contract.GetProperty("instrumentVersion").GetString());
        Assert.Equal("221D5133D8058FEBEAEA0A13F83058219D900261B033282EE389A82BA20FAAF7",
            contract.GetProperty("instrumentContractSha256").GetString());
        Assert.Equal("2F938F18D98C6299214C080195633A68B68C688CABF49DC0F4DEFFCA9C1897D9",
            contract.GetProperty("repairImplementationSha256").GetString());
        Assert.Equal("AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445",
            contract.GetProperty("expectedC11ManifestSha256").GetString());

        var families = contract.GetProperty("families").EnumerateArray()
            .Select(x => x.GetString()!).ToArray();
        Assert.Equal(["RICE_HEAD_COMPLETION", "G1_INTERIOR_SPATIAL"], families);
        Assert.Equal(3, contract.GetProperty("populations").GetArrayLength());
        Assert.Equal(["INVALID", "BLOCKED", "FEASIBILITY_DEMONSTRATED", "LIMITED_PARK"],
            contract.GetProperty("outcomes").EnumerateArray().Select(x => x.GetString()!).ToArray());

        var route = contract.GetProperty("executionRoute");
        Assert.False(route.GetProperty("evaluationBodyPresent").GetBoolean());
        Assert.False(route.GetProperty("authorizationBindingPresent").GetBoolean());
        Assert.False(route.GetProperty("humanAuthorizationGranted").GetBoolean());
        Assert.Equal("BLOCKED", route.GetProperty("currentState").GetString());
        Assert.Contains("C11_NOT_AUTHORIZED",
            contract.GetProperty("stopConditions").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public void CorrectivePreregistrationCanonicalIdentityIsStableAndDeclared()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(FindRepositoryRoot(), ContractPath)));
        var artifact = document.RootElement;
        var canonical = CanonicalHash(artifact.GetProperty("contract"));
        Assert.Equal(artifact.GetProperty("canonicalSha256").GetString(), canonical);
    }

    [Fact]
    public void HistoricalSnapshotAndEmptyCorrectiveColumnsRemainExact()
    {
        var root = FindRepositoryRoot();
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ContractPath)));
        var historical = document.RootElement.GetProperty("contract").GetProperty("historicalSnapshot");
        var rice = historical.GetProperty("riceHeadCompletion");
        Assert.Equal(40360, rice.GetProperty("universeAB").GetInt32());
        Assert.Equal(39596, rice.GetProperty("comparableContext").GetInt32());
        Assert.Equal(37080, rice.GetProperty("jointSupport").GetInt32());
        Assert.Equal(693, rice.GetProperty("unique").GetInt32());
        Assert.Equal(36387, rice.GetProperty("alternatives").GetInt32());
        Assert.Equal(2516, rice.GetProperty("contradiction").GetInt32());
        Assert.Equal(764, rice.GetProperty("noContext").GetInt32());
        var g1 = historical.GetProperty("g1InteriorSpatial");
        Assert.Equal(16881, g1.GetProperty("universeAB").GetInt32());
        Assert.Equal(9540, g1.GetProperty("comparableContext").GetInt32());
        Assert.Equal(3373, g1.GetProperty("jointSupport").GetInt32());
        Assert.Equal(192, g1.GetProperty("unique").GetInt32());
        Assert.Equal(3181, g1.GetProperty("alternatives").GetInt32());
        Assert.Equal(6167, g1.GetProperty("contradiction").GetInt32());
        Assert.Equal(7341, g1.GetProperty("noContext").GetInt32());
        Assert.Equal(288, g1.GetProperty("universeCOperational").GetInt32());
        Assert.Equal(11, g1.GetProperty("historicallySupportedOperational").GetInt32());

        var comparison = File.ReadAllLines(Path.Combine(root,
            "docs/lane_0_corrective_evaluation_comparison_template.csv"));
        Assert.Equal(3, comparison.Length);
        Assert.All(comparison.Skip(1), row => Assert.EndsWith(",,,,,,,", row));

        var cases = File.ReadAllLines(Path.Combine(root,
            "docs/lane_0_corrective_g1_case_template.csv"));
        Assert.Equal(12, cases.Length);
        Assert.Equal(9, cases.Skip(1).Count(x => x.Contains("20651C9B", StringComparison.Ordinal)));
        Assert.Equal(2, cases.Skip(1).Count(x => x.Contains("E73B098A", StringComparison.Ordinal)));
        Assert.All(cases.Skip(1), row => Assert.EndsWith(",,,,,,,,,,", row));
    }

    [Fact]
    public void SyntheticControlsRejectEveryForbiddenDonorAndAdmitIndependentPriorDonor()
    {
        var target = Occurrence("target", "target-group", "target-event", "target-parent", [1, 2],
            releases: [7], future: [8, 9], anchorTime: 2000);
        var legitimate = Occurrence("legitimate", "legitimate-group", "legitimate-event",
            "legitimate-parent", [10, 11], anchorTime: 1000);
        var controls = new[]
        {
            target,
            legitimate,
            Occurrence("group", target.GroupId, "group-event", "group-parent", [12], anchorTime: 1000),
            Occurrence("parent", "parent-group", "parent-event", target.ParentId!, [13], anchorTime: 1000),
            Occurrence("release", "release-group", "release-event", "release-parent", [14],
                releases: [7], anchorTime: 1000),
            Occurrence("temporal", "temporal-group", "temporal-event", "temporal-parent", [15],
                anchorTime: 2000),
            Occurrence("identity", "identity-group", "identity-event", "identity-parent", [8],
                anchorTime: 1000),
            Occurrence("both", "both-group", "both-event", "both-parent", [9], anchorTime: 3000),
            Occurrence("event", "event-group", target.EventId, "event-parent", [16], anchorTime: 1000),
            Occurrence("synthetic", "synthetic-group", "synthetic-event", "synthetic-parent", [17],
                anchorTime: 1000) with { IsSynthetic = true },
            Occurrence("cross", "cross-group", "cross-event", "cross-parent", [18], anchorTime: 1000)
                with { ChartId = "OTHER" },
            Occurrence("composition", "composition-group", "composition-event", "composition-parent",
                [19], anchorTime: 1000) with { ComponentOccurrenceIds = ["temporal", "spatial"] }
        };

        var audit = Lane0SpatialResearch.Audit(target, controls);
        Assert.Single(audit.Eligible);
        Assert.Equal("legitimate", audit.Eligible[0].OccurrenceId);
        Assert.True(audit.Target > 0);
        Assert.True(audit.TargetGroup > 0);
        Assert.True(audit.Parent > 0);
        Assert.True(audit.ReleaseEndpoint > 0);
        Assert.Equal(3, audit.TemporalExclusion);
        Assert.Equal(2, audit.FutureHeldIdentityExclusion);
        Assert.Equal(1, audit.BothTemporalAndIdentity);
        Assert.Equal(4, audit.UniqueFutureHeldExcluded);
        Assert.True(audit.SameEvent > 0);
        Assert.True(audit.SyntheticTeaching > 0);
        Assert.True(audit.CrossChart > 0);
        Assert.True(audit.ArtificialComposition > 0);
        Assert.True(audit.PartitionValid);
    }

    [Fact]
    public void CorrectedG1ObservationIsDeterministicNonMutatingAndRiceSentinelIsUnaffected()
    {
        var chart = new ManiaChart
        {
            KeyCount = 7,
            Lines = [],
            OriginalObjects =
            [
                ManiaObject.Ln(0, 0, 4000) with { Sequence = 0 },
                ManiaObject.Ln(1, 1000, 2000) with { Sequence = 1 },
                ManiaObject.Ln(0, 5000, 9000) with { Sequence = 2 },
                ManiaObject.Ln(1, 6000, 7000) with { Sequence = 3 },
                ManiaObject.Ln(0, 10000, 14000) with { Sequence = 4 },
                ManiaObject.Ln(1, 11000, 12000) with { Sequence = 5 }
            ],
            TimingPoints = [new TimingPoint(0, 500)]
        };
        var before = MapperEvidenceProfileBuilder.ComputeFingerprint(chart);
        var censusA = InteriorRelationFeasibilityResearch.Evaluate(chart);
        var builtA = Lane0FeasibilityRunner.BuildG1Occurrences("CHART", censusA);
        var censusB = InteriorRelationFeasibilityResearch.Evaluate(chart);
        var builtB = Lane0FeasibilityRunner.BuildG1Occurrences("CHART", censusB);
        Assert.Equal(builtA.Select(StableOccurrence), builtB.Select(StableOccurrence));
        Assert.Equal(0, censusA.ResearchRngCalls);
        Assert.Equal(before, MapperEvidenceProfileBuilder.ComputeFingerprint(chart));

        var riceTarget = new Lane0Occurrence(Lane0Family.RiceHeadCompletion, "CHART", 7, "rice-target",
            "rice-target-group", "rice-target-event", null, "Q", "J", "T", "S", [100], [], [],
            AnchorTime: 1000);
        var riceDonor = riceTarget with
        {
            OccurrenceId = "rice-donor", GroupId = "rice-donor-group", EventId = "rice-donor-event",
            ObservationIds = [101], AnchorTime = 9000
        };
        var rice = Lane0SpatialResearch.Audit(riceTarget, [riceDonor]);
        Assert.Single(rice.Eligible);
        Assert.Equal(0, rice.TemporalExclusion);
        Assert.Equal(0, rice.FutureHeldIdentityExclusion);

        static string StableOccurrence(Lane0Occurrence x) => string.Join('|',
            x.Family, x.ChartId, x.KeyCount, x.OccurrenceId, x.GroupId, x.EventId, x.ParentId,
            x.QuerySignature, x.JointResultSignature, x.TemporalResultSignature,
            x.SpatialResultSignature, string.Join(',', x.ObservationIds),
            string.Join(',', x.ReleaseEndpointObservationIds), string.Join(',', x.FutureHeldObservationIds),
            x.IsSynthetic, x.ComponentOccurrenceIds.IsDefault
                ? "" : string.Join(',', x.ComponentOccurrenceIds), x.CompleteIdentity, x.AnchorTime);
    }

    private static Lane0Occurrence Occurrence(string id, string group, string eventId, string parent,
        int[] ids, int[]? releases = null, int[]? future = null, int anchorTime = 1000) =>
        new(Lane0Family.G1InteriorSpatial, "CHART", 7, id, group, eventId, parent, "Q", "J", "T", "S",
            ids.ToImmutableArray(), (releases ?? []).ToImmutableArray(),
            (future ?? []).ToImmutableArray(), AnchorTime: anchorTime);

    private static string CanonicalHash(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) WriteCanonical(writer, element);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray()) WriteCanonical(writer, item);
                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null;
             directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ManiaAddNotesLab.sln")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
