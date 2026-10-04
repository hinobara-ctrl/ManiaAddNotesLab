using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;

internal sealed record Lane0IntegrationOfficialPaths(
    string AuthorizationRoot, string CanonicalBindingPath, string DurableReceiptPath,
    string StagingRoot, string FinalArtifactsRoot);

internal static class Lane0IntegrationOfficialLayout
{
    internal const string OneShotNamespace =
        "LANE.0.CORRECTIVE_SUCCESSOR.INTEGRATION.ONE_SHOT";
    internal const string AuthorizationDirectory =
        ".artifacts/lane0-corrective-successor-integration-authorization";

    internal static Lane0IntegrationOfficialPaths Derive(string canonicalSourceRoot,
        string canonicalExecutionRoot)
    {
        var authorization = Under(canonicalSourceRoot, AuthorizationDirectory);
        return new(authorization,
            Under(authorization, "canonical-binding.json"),
            Under(authorization, $"{OneShotNamespace}/receipt.json"),
            StagingRoot(canonicalExecutionRoot), FinalArtifactsRoot(canonicalExecutionRoot));
    }

    internal static string StagingRoot(string executionRoot) =>
        Under(executionRoot, ".artifacts/integration-exchange/staging");
    internal static string FinalArtifactsRoot(string executionRoot) =>
        Under(executionRoot, ".artifacts/integration-exchange/final-artifacts");

    private static string Under(string root, string relative)
    {
        var canonicalRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var path = Path.GetFullPath(Path.Combine(canonicalRoot,
            relative.Replace('/', Path.DirectorySeparatorChar)));
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!path.StartsWith(canonicalRoot + Path.DirectorySeparatorChar, comparison))
            throw new InvalidDataException("Official authority path escaped its canonical root.");
        return path;
    }
}

internal sealed record Lane0IntegrationCanonicalBindingDocument(
    Lane0IntegrationBindingFields Fields, string CanonicalSha256);

internal sealed record Lane0IntegrationWorkerAuthorityState(
    string CanonicalSourceRoot, string ExpectedHead, string CanonicalBindingSha256,
    Lane0IntegrationRuntimeIdentities ExpectedIdentities);

internal sealed class Lane0IntegrationCanonicalBindingLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = false
    };

    internal Lane0IntegrationVerifiedBinding Load(string canonicalBindingPath)
    {
        if (!File.Exists(canonicalBindingPath))
            throw new InvalidDataException("Canonical binding is absent from AuthorizationRoot.");
        var exactBytes = File.ReadAllBytes(canonicalBindingPath);
        var document = JsonSerializer.Deserialize<Lane0IntegrationCanonicalBindingDocument>(
            exactBytes, JsonOptions) ?? throw new InvalidDataException("Canonical binding is malformed.");
        var fieldBytes = Lane0CorrectiveSuccessorIsolatedLauncher.CanonicalBindingBytes(document.Fields);
        var hash = Convert.ToHexString(SHA256.HashData(fieldBytes));
        if (document.CanonicalSha256 != hash)
            throw new InvalidDataException("Canonical binding declared hash drifted.");
        var reconstructed = CanonicalDocumentBytes(document.Fields, hash);
        if (!exactBytes.AsSpan().SequenceEqual(reconstructed))
            throw new InvalidDataException("Canonical binding file bytes are not canonical.");
        return new(fieldBytes.ToImmutableArray(), hash, document.Fields);
    }

    internal static byte[] CanonicalDocumentBytes(Lane0IntegrationBindingFields fields,
        string? declaredHash = null)
    {
        var fieldBytes = Lane0CorrectiveSuccessorIsolatedLauncher.CanonicalBindingBytes(fields);
        var hash = declaredHash ?? Convert.ToHexString(SHA256.HashData(fieldBytes));
        return [.. JsonSerializer.SerializeToUtf8Bytes(
            new Lane0IntegrationCanonicalBindingDocument(fields, hash), JsonOptions), (byte)'\n'];
    }
}

internal static class Lane0IntegrationDurableReceiptVerifier
{
    internal static string VerifyCanonicalReceipt(
        string canonicalSourceRoot, string canonicalExecutionRoot, string expectedHead,
        string canonicalBindingSha256, Lane0IntegrationRuntimeIdentities identities)
    {
        var paths = Lane0IntegrationOfficialLayout.Derive(canonicalSourceRoot, canonicalExecutionRoot);
        if (!File.Exists(paths.DurableReceiptPath))
            throw new InvalidDataException("Canonical durable receipt is absent.");
        var expected = Lane0CorrectiveSuccessorIsolatedLauncher.BuildReceipt(
            expectedHead, identities, canonicalBindingSha256);
        var actual = File.ReadAllBytes(paths.DurableReceiptPath);
        if (!actual.AsSpan().SequenceEqual(expected))
            throw new InvalidDataException("Canonical durable receipt bytes or semantics drifted.");
        return paths.DurableReceiptPath;
    }
}
