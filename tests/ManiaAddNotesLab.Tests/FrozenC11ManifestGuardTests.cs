using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using ManiaAddNotesLab.Core;

namespace ManiaAddNotesLab.Tests;

public sealed class FrozenC11ManifestGuardTests
{
    [Fact]
    public void PublishedManifestRecomputesToDeclaredAndTrustedHistoricalIdentity()
    {
        var verified = FrozenC11ManifestResearch.Load(ManifestPath());

        Assert.Equal(FrozenC11ManifestResearch.TrustedCanonicalSha256, verified.CanonicalSha256);
        Assert.Equal(11, verified.Charts.Length);
    }

    [Fact]
    public void AlteredContentWithOldDeclaredHashIsRejected()
    {
        var root = ReadManifest();
        root["manifest"]!["purpose"] = "altered purpose";

        var error = Assert.Throws<InvalidDataException>(() => LoadTemporary(root));

        Assert.Contains("does not match declared hash", error.Message);
    }

    [Fact]
    public void AlteredContentAndMatchingAlteredDeclaredHashCannotReplaceTrustedIdentity()
    {
        var root = ReadManifest();
        root["manifest"]!["purpose"] = "altered purpose";
        using var changed = JsonDocument.Parse(root["manifest"]!.ToJsonString());
        root["canonicalSha256"] = FrozenC11ManifestResearch.ComputeCanonicalSha256(changed.RootElement);

        var error = Assert.Throws<InvalidDataException>(() => LoadTemporary(root));

        Assert.Contains("does not match trusted historical identity", error.Message);
    }

    [Fact]
    public void MissingOrUnexpectedManifestFieldsAreRejectedBeforeHashComparison()
    {
        var missing = ReadManifest();
        missing["manifest"]!.AsObject().Remove("purpose");
        var missingError = Assert.Throws<InvalidDataException>(() => LoadTemporary(missing));
        Assert.Contains("shape drifted", missingError.Message);

        var unexpected = ReadManifest();
        unexpected["manifest"]!["unexpected"] = true;
        var unexpectedError = Assert.Throws<InvalidDataException>(() => LoadTemporary(unexpected));
        Assert.Contains("shape drifted", unexpectedError.Message);
    }

    private static JsonObject ReadManifest() =>
        JsonNode.Parse(File.ReadAllText(ManifestPath()))!.AsObject();

    private static VerifiedFrozenC11Manifest LoadTemporary(JsonObject root)
    {
        var directory = Path.Combine(Path.GetTempPath(), "c11-manifest-guard-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "manifest.json");
        try
        {
            File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            return FrozenC11ManifestResearch.Load(path);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string ManifestPath() => Path.Combine(FindRepositoryRoot(), "docs", "g1_gate_runtime_manifest.json");

    private static string FindRepositoryRoot([CallerFilePath] string source = "")
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory(), source })
        {
            var directory = File.Exists(start) ? Directory.GetParent(start) : new DirectoryInfo(start);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "ManiaAddNotesLab.sln")))
                    return directory.FullName;
                directory = directory.Parent;
            }
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
