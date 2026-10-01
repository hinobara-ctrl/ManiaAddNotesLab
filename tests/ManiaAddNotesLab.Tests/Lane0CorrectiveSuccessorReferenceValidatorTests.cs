using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using ManiaAddNotesLab.Core;

namespace ManiaAddNotesLab.Tests;

public sealed class Lane0CorrectiveSuccessorReferenceValidatorTests
{
    private const string ContractPath =
        "docs/lane_0_corrective_successor_preregistration_contract.json";
    private const string HistoricalContractPath =
        "docs/lane_0_corrective_evaluation_preregistration_contract.json";
    private const string ManifestPath = "docs/g1_gate_runtime_manifest.json";

    [Fact]
    public void PublishedSuccessorContractIsCanonicalAndHasNoAuthority()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), ContractPath)));
        var artifact = document.RootElement;
        var contract = artifact.GetProperty("contract");

        Assert.Equal("66B0D27535B8EEE5B466DDAB1209DD0C548B982C24913E1993E611D57E1BE505",
            artifact.GetProperty("canonicalSha256").GetString());
        var computedCanonical = CanonicalHash(contract);
        Assert.True(artifact.GetProperty("canonicalSha256").GetString() == computedCanonical,
            $"Declared canonical identity differs from computed {computedCanonical}.");
        Assert.Equal("lane-0-corrective-successor-preregistration.1",
            contract.GetProperty("schemaVersion").GetString());
        Assert.Equal("NO_C11_AUTHORIZATION", contract.GetProperty("authorization").GetString());
        Assert.Equal("SCIENTIFIC_REFERENCE_REMEDIATION_ONLY",
            contract.GetProperty("scientificRemediation").GetString());

        var authority = contract.GetProperty("authorityNamespace");
        Assert.Equal("docs/lane_0_corrective_successor_publication_binding.json",
            authority.GetProperty("successorBindingPath").GetString());
        Assert.Equal(".artifacts/lane_0_corrective_successor.attempt.json",
            authority.GetProperty("successorReceiptPath").GetString());
        Assert.NotEqual(authority.GetProperty("historicalBindingPath").GetString(),
            authority.GetProperty("successorBindingPath").GetString());
        Assert.NotEqual(authority.GetProperty("historicalReceiptPath").GetString(),
            authority.GetProperty("successorReceiptPath").GetString());
        Assert.False(authority.GetProperty("bindingPresent").GetBoolean());
        Assert.False(authority.GetProperty("receiptPresent").GetBoolean());
        Assert.False(authority.GetProperty("humanAuthorizationGranted").GetBoolean());
        Assert.False(authority.GetProperty("corpusAccessAuthorized").GetBoolean());
    }

    [Fact]
    public void HistoricalBRemainsHistoricalWhileSuccessorFreezesA()
    {
        using var historical = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(Root(), HistoricalContractPath)));
        var oldReferences = historical.RootElement.GetProperty("contract")
            .GetProperty("historicalG1OperationalReferences").EnumerateArray()
            .Select(x => x.GetProperty("chartId").GetString()).ToArray();
        Assert.Contains(Lane0CorrectiveSuccessorReferenceValidator.HistoricalInvalidSpringIdentity,
            oldReferences);

        using var successor = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), ContractPath)));
        var newReferences = successor.RootElement.GetProperty("contract")
            .GetProperty("historicalG1CaseRule").GetProperty("distribution").EnumerateArray()
            .Select(x => x.GetProperty("chartId").GetString()).ToArray();
        Assert.Contains(Lane0CorrectiveSuccessorReferenceValidator.CorrectedSpringIdentity,
            newReferences);
        Assert.DoesNotContain(Lane0CorrectiveSuccessorReferenceValidator.HistoricalInvalidSpringIdentity,
            newReferences);
    }

    [Fact]
    public void CorrectedReferencesPassPublicManifestSemanticJoin()
    {
        AssertValid(References());
    }

    [Fact]
    public void HistoricalBAndUnknownIdentityFailBeforeCorpusAccess()
    {
        var values = References();
        AssertInvalid(values.SetItem(1, values[1] with
        {
            ChartId = Lane0CorrectiveSuccessorReferenceValidator.HistoricalInvalidSpringIdentity
        }));
        AssertInvalid(values.SetItem(1, values[1] with { ChartId = new string('F', 64) }));
    }

    [Fact]
    public void MissingDuplicateExtraAndValidButWrongChartFail()
    {
        var manifest = Manifest();
        var values = References();
        AssertInvalid(values.RemoveAt(1));
        AssertInvalid(values.Add(values[1]));
        AssertInvalid(values.Add(values[0] with { ChartId = new string('E', 64) }));
        var wrong = manifest.Charts.First(x => x.Sha256 != values[0].ChartId
            && x.Sha256 != values[1].ChartId);
        AssertInvalid(values.SetItem(1, new(wrong.Sha256, "G1_INTERIOR_SPATIAL",
            wrong.FamilyKey, wrong.KeyCount, 2)), manifest);
    }

    [Fact]
    public void FamilyAndKeymodeDriftFail()
    {
        var values = References();
        AssertInvalid(values.SetItem(1, values[1] with { ExpectedManifestFamily = "WRONG" }));
        AssertInvalid(values.SetItem(1, values[1] with { Keymode = 4 }));
    }

    [Fact]
    public void AlteredNinePlusTwoDistributionAndTotalFail()
    {
        var values = References();
        AssertInvalid(values.SetItem(0, values[0] with { HistoricallySupportedCount = 10 })
            .SetItem(1, values[1] with { HistoricallySupportedCount = 1 }));
        AssertInvalid(values.SetItem(1, values[1] with { HistoricallySupportedCount = 1 }));
        AssertInvalid(values, expectedTotal: 12);
    }

    [Fact]
    public void WrongManifestIdentityFails()
    {
        AssertInvalid(References(), manifestIdentity: new string('0', 64));
    }

    [Fact]
    public void StaticValidatorHasNoCorpusOrRngInputSurface()
    {
        var method = typeof(Lane0CorrectiveSuccessorReferenceValidator).GetMethod("Validate")!;
        var types = method.GetParameters().Select(x => x.ParameterType).ToArray();
        Assert.DoesNotContain(types, x => x == typeof(Random) || x == typeof(DirectoryInfo)
            || x == typeof(FileInfo));
    }

    private static ImmutableArray<Lane0CorrectiveSuccessorReference> References() =>
        Lane0CorrectiveSuccessorReferenceValidator.FrozenReferences;

    private static VerifiedFrozenC11Manifest Manifest() =>
        FrozenC11ManifestResearch.Load(Path.Combine(Root(), ManifestPath));

    private static void AssertValid(ImmutableArray<Lane0CorrectiveSuccessorReference> references)
    {
        var result = Validate(references);
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    private static void AssertInvalid(ImmutableArray<Lane0CorrectiveSuccessorReference> references,
        VerifiedFrozenC11Manifest? manifest = null, int expectedTotal = 11,
        string? manifestIdentity = null)
    {
        var result = Validate(references, manifest, expectedTotal, manifestIdentity);
        Assert.False(result.IsValid);
    }

    private static Lane0CorrectiveSuccessorReferenceValidation Validate(
        ImmutableArray<Lane0CorrectiveSuccessorReference> references,
        VerifiedFrozenC11Manifest? manifest = null,
        int expectedTotal = 11,
        string? manifestIdentity = null)
    {
        manifest ??= Manifest();
        return Lane0CorrectiveSuccessorReferenceValidator.Validate(
            manifestIdentity ?? manifest.CanonicalSha256, manifest.Charts, references, expectedTotal);
    }

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
                foreach (var property in element.EnumerateObject()
                             .OrderBy(x => x.Name, StringComparer.Ordinal))
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

    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null;
             directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ManiaAddNotesLab.sln")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
