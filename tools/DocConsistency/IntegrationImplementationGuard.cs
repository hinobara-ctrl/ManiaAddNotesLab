using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DocConsistencyTool;

public static class IntegrationImplementationGuard
{
    public const string ContractIdentity =
        "8763BC90212AF44947C6BAF3BD7FAADD1905289F0F8A1C3480199F771D3CAA0A";
    public const string ParentIdentity =
        "7C0A86BF3C6647CA7092C4D3390EB97EA6E11D7E5429ECBA6F499D55C5417A02";

    private static readonly string[] Steps =
    [
        "SOURCE_PUBLIC_HEAD_CHECKS", "CONTRACT_IDENTITIES", "RUNTIME_IDENTITIES",
        "CANONICAL_BINDING_PRESENCE", "CANONICAL_BINDING_BYTE_AND_HASH_VERIFICATION",
        "EXPLICIT_AUTHORIZATION_FIELD", "AUTHORIZED_EXECUTION_COUNT_ONE",
        "SOURCE_TRACKED_CLEANLINESS", "SOURCE_EXECUTION_RUNTIME_HEAD_EQUALITY",
        "SOURCE_EXECUTION_ROOT_ISOLATION", "DURABLE_RECEIPT_ABSENCE",
        "OUTPUT_AND_STAGING_ABSENCE", "DURABLE_RECEIPT_CREATION", "CORPUSROOT_INTERPRETATION",
        "CORPUS_SOURCE_EXECUTION_ROOT_ISOLATION", "CORPUS_ADMISSION", "SCIENTIFIC_PASS_1",
        "SCIENTIFIC_PASS_2", "BYTE_DETERMINISM", "DEEP_SEMANTIC_VERIFICATION",
        "FINAL_PUBLISHABLE_OR_NON_PUBLISHABLE_CLASSIFICATION"
    ];

    public static IReadOnlyList<string> Validate(JsonElement artifact, string repositoryRoot)
    {
        var errors = new List<string>();
        try
        {
            var contract = artifact.GetProperty("contract");
            Expect(errors, CanonicalJsonHash(contract) == ContractIdentity,
                "canonical contract content identity drifted");
            Expect(errors, String(artifact, "canonicalSha256") == ContractIdentity,
                "declared canonical contract identity drifted");
            Expect(errors, String(contract, "schemaVersion") ==
                "lane-0-corrective-successor-integration-execution.1", "schema drifted");
            Expect(errors, String(contract, "phaseId") ==
                "LANE.0.CORRECTIVE_SUCCESSOR.INTEGRATION_IMPLEMENTATION", "phase drifted");
            Expect(errors, String(contract, "baselinePublicHead") ==
                "94bba4825e1a3fafbcb44940b63bbd3e9b8b221c", "baseline drifted");
            Expect(errors, String(contract, "parentIntegrationPreregistrationSha256") == ParentIdentity,
                "parent preregistration lineage drifted");

            var authorization = contract.GetProperty("authorization");
            Expect(errors, String(authorization, "integrationImplementation") ==
                "AUTHORIZED_FOR_IMPLEMENTATION_ONLY", "implementation authority drifted");
            Expect(errors, String(authorization, "c11") == "NO_C11_AUTHORIZATION",
                "C11 authority drifted");
            Expect(errors, String(authorization, "binding") == "ABSENT"
                && String(authorization, "receipt") == "ABSENT"
                && !authorization.GetProperty("executionAuthorized").GetBoolean(),
                "binding/receipt/execution boundary drifted");

            var components = contract.GetProperty("components");
            ValidateComponent(errors, components, repositoryRoot, "officialFrozenC11Adapter",
                "B656DC414568FD9464B324FA20B120DDE333D1ADEE2EF5FE6BC9E4E6AF4EB08C");
            ValidateComponent(errors, components, repositoryRoot, "officialInternalResearchRunner",
                "CC26CF98AF721438362A0C5B6334E9215DB4C61F677C4D76D8864119A34CD0B4");
            ValidateComponent(errors, components, repositoryRoot, "isolatedExecutionLauncher",
                "D1AF3E14BF31104EFB61DA0A8D4B9447A2C7D069162256904F9AA0EB72FEC25F");
            ValidateComponent(errors, components, repositoryRoot, "deepSemanticPackageVerifier",
                "F3A7E703541343DC51C34B95D433ABBD86B6DDCBEC7FA26F006252725FD7737F");

            var dependencies = contract.GetProperty("frozenDependencies");
            Expect(errors, String(dependencies, "phase2ContractSha256") ==
                "11F55A6770BA78F008A6690C88561C714CAF45BA15B96E472C0C80B219C4D5B6",
                "Phase 2 contract identity drifted");
            Expect(errors, String(dependencies, "phase2ScientificImplementationSha256") ==
                "8F9AE545027F0F1364E3378324646C7F7711CDF7EFFC8CC879660644A3D905D0",
                "Phase 2 science identity drifted");
            Expect(errors, String(dependencies, "phase2HarnessSha256") ==
                "15DC978C24119E9832DD70C0C61F5BFB1BBAF11520A107A7728220009B793A61",
                "Phase 2 harness identity drifted");
            Expect(errors, String(dependencies, "phase2PackageVerifierSha256") ==
                "DFF48221A6FB087367B1C6B1062B7355806C53668C6876A4BB4EC59983232D38",
                "Phase 2 package identity drifted");
            Expect(errors, String(dependencies, "frozenDependenciesSha256") ==
                "D5931A815701C0FAB854B878373A8ED03F3E29BDEA17C98A428C7AF6B0F7023B",
                "frozen dependency identity drifted");
            Expect(errors, String(dependencies, "verifiedFrozenC11ManifestSha256") ==
                "AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445",
                "manifest identity drifted");

            var corpus = contract.GetProperty("corpusAdmission");
            Expect(errors, corpus.GetProperty("locationCount").GetInt32() == 12
                && corpus.GetProperty("uniqueContentCount").GetInt32() == 11
                && corpus.GetProperty("uniqueOriginalObjects").GetInt32() == 50_836,
                "corpus counts drifted");
            Expect(errors, String(corpus, "preReceiptCorpusRootState") == "OPAQUE_TOKEN"
                && !corpus.GetProperty("discovery").GetBoolean()
                && !corpus.GetProperty("arbitraryEnumeration").GetBoolean(),
                "corpus opacity/discovery boundary drifted");
            var steps = contract.GetProperty("authorityOrder").EnumerateArray()
                .Select(x => x.GetString()).ToArray();
            Expect(errors, steps.SequenceEqual(Steps), "authority order drifted");
            var boundary = contract.GetProperty("phaseBoundary");
            Expect(errors, String(boundary, "outcome") == "READY_FOR_INDEPENDENT_INTEGRATION_AUDIT"
                && !boundary.GetProperty("c11Accessed").GetBoolean()
                && !boundary.GetProperty("behaviorChange").GetBoolean()
                && !boundary.GetProperty("rngChange").GetBoolean()
                && !boundary.GetProperty("defaultChange").GetBoolean()
                && !boundary.GetProperty("productExposure").GetBoolean()
                && String(boundary, "nextRequiredAction") == "HUMAN_REVIEW_REQUIRED",
                "phase boundary drifted");
        }
        catch (Exception exception)
        {
            errors.Add($"integration implementation contract malformed: {exception.Message}");
        }
        return errors;
    }

    public static string CanonicalJsonHash(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) WriteCanonical(writer, element);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    public static string NormalizedTextTreeIdentity(string repositoryRoot, IEnumerable<string> files)
    {
        var rows = files.OrderBy(x => x, StringComparer.Ordinal).Select(relative =>
        {
            var text = File.ReadAllText(Path.Combine(repositoryRoot,
                relative.Replace('/', Path.DirectorySeparatorChar)));
            if (text.Length > 0 && text[0] == '\uFEFF') text = text[1..];
            text = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            var hash = Convert.ToHexString(SHA256.HashData(new UTF8Encoding(false).GetBytes(text)));
            return $"{relative}|{hash}";
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', rows))));
    }

    private static void ValidateComponent(List<string> errors, JsonElement components,
        string root, string name, string expectedIdentity)
    {
        var component = components.GetProperty(name);
        var files = component.GetProperty("files").EnumerateArray()
            .Select(x => x.GetString() ?? "").ToArray();
        Expect(errors, files.Length > 0 && files.All(x => !string.IsNullOrWhiteSpace(x)),
            $"{name} file set is empty");
        Expect(errors, String(component, "normalizedTextTreeSha256") == expectedIdentity,
            $"{name} declared identity drifted");
        Expect(errors, NormalizedTextTreeIdentity(root, files) == expectedIdentity,
            $"{name} live implementation identity drifted");
    }

    private static string String(JsonElement parent, string name) =>
        parent.GetProperty(name).GetString() ?? throw new InvalidDataException($"{name} is null.");
    private static void Expect(List<string> errors, bool condition, string message)
    {
        if (!condition) errors.Add(message);
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal))
                { writer.WritePropertyName(property.Name); WriteCanonical(writer, property.Value); }
                writer.WriteEndObject(); break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray()) WriteCanonical(writer, item);
                writer.WriteEndArray(); break;
            case JsonValueKind.String: writer.WriteStringValue(element.GetString()); break;
            case JsonValueKind.Number: element.WriteTo(writer); break;
            case JsonValueKind.True: writer.WriteBooleanValue(true); break;
            case JsonValueKind.False: writer.WriteBooleanValue(false); break;
            case JsonValueKind.Null: writer.WriteNullValue(); break;
            default: throw new InvalidDataException($"Unsupported JSON kind {element.ValueKind}.");
        }
    }
}
