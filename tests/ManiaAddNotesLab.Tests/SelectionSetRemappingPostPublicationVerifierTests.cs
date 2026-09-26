using System.Security.Cryptography;
using System.Text;

namespace ManiaAddNotesLab.Tests;

public sealed class SelectionSetRemappingPostPublicationVerifierTests
{
    [Fact]
    public void IntactArtifactHashManifestPasses()
    {
        using var fixture = new HashFixture();
        fixture.WriteArtifact("result.json", "{\"outcome\":\"E_DEMONSTRATED\"}\n");
        fixture.WriteManifest((fixture.Hash("result.json"), "result.json"));
        SafetySelectionSetRemappingPostPublicationVerifier.VerifyHashManifest(
            fixture.Root, fixture.Manifest, requireExactFileSet: true);
    }

    [Fact]
    public void AlteredArtifactWithHistoricalHashFails()
    {
        using var fixture = new HashFixture();
        fixture.WriteArtifact("result.json", "original");
        var hash = fixture.Hash("result.json");
        fixture.WriteArtifact("result.json", "altered");
        fixture.WriteManifest((hash, "result.json"));
        Assert.Throws<InvalidDataException>(() =>
            SafetySelectionSetRemappingPostPublicationVerifier.VerifyHashManifest(
                fixture.Root, fixture.Manifest, requireExactFileSet: true));
    }

    [Fact]
    public void MissingHashedArtifactFails()
    {
        using var fixture = new HashFixture();
        fixture.WriteManifest((new string('0', 64), "missing.json"));
        Assert.Throws<InvalidDataException>(() =>
            SafetySelectionSetRemappingPostPublicationVerifier.VerifyHashManifest(
                fixture.Root, fixture.Manifest, requireExactFileSet: true));
    }

    [Fact]
    public void UnexpectedUnhashedArtifactFails()
    {
        using var fixture = new HashFixture();
        fixture.WriteArtifact("result.json", "expected");
        fixture.WriteArtifact("untracked.json", "unexpected");
        fixture.WriteManifest((fixture.Hash("result.json"), "result.json"));
        Assert.Throws<InvalidDataException>(() =>
            SafetySelectionSetRemappingPostPublicationVerifier.VerifyHashManifest(
                fixture.Root, fixture.Manifest, requireExactFileSet: true));
    }

    private sealed class HashFixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(),
            "selection-remap-verifier-" + Guid.NewGuid().ToString("N"));
        public string Manifest => Path.Combine(Root, "sha256sums.txt");

        public HashFixture() => Directory.CreateDirectory(Root);

        public void WriteArtifact(string name, string content) =>
            File.WriteAllText(Path.Combine(Root, name), content, new UTF8Encoding(false));

        public string Hash(string name) => Convert.ToHexString(
            SHA256.HashData(File.ReadAllBytes(Path.Combine(Root, name))));

        public void WriteManifest(params (string Hash, string Path)[] entries) =>
            File.WriteAllLines(Manifest, entries.Select(x => $"{x.Hash}  {x.Path}"),
                new UTF8Encoding(false));

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
