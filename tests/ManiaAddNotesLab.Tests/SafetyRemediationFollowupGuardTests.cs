using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using DocConsistencyTool;

namespace ManiaAddNotesLab.Tests;

public sealed class SafetyRemediationFollowupGuardTests
{
    [Fact]
    public void PublishedArtifactsPassStructuralAndHistoricalIdentityValidation()
    {
        var errors = SafetyRemediationFollowupGuard.Validate(ContractPath(), FollowupPath());

        Assert.Empty(errors);
    }

    [Fact]
    public void EveryContractLeafHasAnAdversarialBadControl()
    {
        var original = JsonNode.Parse(File.ReadAllText(ContractPath()))!;
        AssertEveryLeafMutationFails(original, mutateContract: true, expectedBadControls: 12);
    }

    [Fact]
    public void EveryFollowupLeafAndEmptyArrayHasAnAdversarialBadControl()
    {
        var original = JsonNode.Parse(File.ReadAllText(FollowupPath()))!;
        AssertEveryLeafMutationFails(original, mutateContract: false, expectedBadControls: 147);
    }

    [Fact]
    public void MissingAndUnexpectedPropertiesFailStructuralValidation()
    {
        var contract = JsonNode.Parse(File.ReadAllText(ContractPath()))!.AsObject();
        var followup = JsonNode.Parse(File.ReadAllText(FollowupPath()))!.AsObject();
        contract["contract"]!.AsObject().Remove("scope");
        AssertNotEmpty(ValidateTemporary(contract, followup));

        contract = JsonNode.Parse(File.ReadAllText(ContractPath()))!.AsObject();
        followup = JsonNode.Parse(File.ReadAllText(FollowupPath()))!.AsObject();
        followup["unexpected"] = true;
        AssertNotEmpty(ValidateTemporary(contract, followup));
    }

    private static void AssertEveryLeafMutationFails(JsonNode original, bool mutateContract,
        int expectedBadControls)
    {
        var paths = new List<int[]>();
        EnumerateMutationPaths(original, [], paths);
        Assert.Equal(expectedBadControls, paths.Count);
        foreach (var path in paths)
        {
            var contract = JsonNode.Parse(File.ReadAllText(ContractPath()))!.AsObject();
            var followup = JsonNode.Parse(File.ReadAllText(FollowupPath()))!.AsObject();
            var target = mutateContract ? contract : followup;
            MutateAtPath(target, path);
            var errors = ValidateTemporary(contract, followup);
            Assert.True(errors.Count > 0, $"Bad control at JSON child path [{string.Join(',', path)}] was accepted.");
        }
    }

    private static void EnumerateMutationPaths(JsonNode node, int[] path, List<int[]> paths)
    {
        if (node is JsonObject obj)
        {
            var index = 0;
            foreach (var property in obj)
            {
                if (property.Value is not null) EnumerateMutationPaths(property.Value, [.. path, index], paths);
                else paths.Add([.. path, index]);
                index++;
            }
            return;
        }
        if (node is JsonArray array)
        {
            if (array.Count == 0) paths.Add(path);
            else for (var i = 0; i < array.Count; i++)
                if (array[i] is not null) EnumerateMutationPaths(array[i]!, [.. path, i], paths);
                else paths.Add([.. path, i]);
            return;
        }
        paths.Add(path);
    }

    private static void MutateAtPath(JsonNode root, int[] path)
    {
        JsonNode current = root;
        for (var depth = 0; depth < path.Length - 1; depth++) current = ChildAt(current, path[depth])!;
        if (path.Length == 0 && current is JsonArray empty)
        {
            empty.Add("BAD_CONTROL");
            return;
        }
        var slot = path[^1];
        var existing = ChildAt(current, slot);
        JsonNode replacement;
        if (existing is JsonArray existingArray && existingArray.Count == 0)
        {
            existingArray.Add("BAD_CONTROL");
            return;
        }
        if (existing is null) replacement = JsonValue.Create("BAD_CONTROL")!;
        else
        {
            using var value = JsonDocument.Parse(existing.ToJsonString());
            replacement = value.RootElement.ValueKind switch
            {
                JsonValueKind.String => JsonValue.Create(value.RootElement.GetString() + "__BAD_CONTROL")!,
                JsonValueKind.Number => JsonValue.Create(value.RootElement.GetInt64() + 1)!,
                JsonValueKind.True => JsonValue.Create(false)!,
                JsonValueKind.False => JsonValue.Create(true)!,
                JsonValueKind.Null => JsonValue.Create("BAD_CONTROL")!,
                _ => throw new InvalidOperationException("Mutation path did not resolve to a scalar.")
            };
        }
        SetChild(current, slot, replacement);
    }

    private static JsonNode? ChildAt(JsonNode node, int index) => node switch
    {
        JsonObject obj => obj.ElementAt(index).Value,
        JsonArray array => array[index],
        _ => throw new InvalidOperationException("Path traversed through a scalar.")
    };

    private static void SetChild(JsonNode node, int index, JsonNode value)
    {
        if (node is JsonObject obj) obj[obj.ElementAt(index).Key] = value;
        else if (node is JsonArray array) array[index] = value;
        else throw new InvalidOperationException("Path traversed through a scalar.");
    }

    private static IReadOnlyList<string> ValidateTemporary(JsonObject contract, JsonObject followup)
    {
        var directory = Path.Combine(Path.GetTempPath(), "followup-guard-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var contractPath = Path.Combine(directory, "contract.json");
        var followupPath = Path.Combine(directory, "followup.json");
        try
        {
            File.WriteAllText(contractPath, contract.ToJsonString());
            File.WriteAllText(followupPath, followup.ToJsonString());
            return SafetyRemediationFollowupGuard.Validate(contractPath, followupPath);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void AssertNotEmpty(IReadOnlyList<string> errors) => Assert.True(errors.Count > 0);
    private static string ContractPath() => Path.Combine(FindRepositoryRoot(), "docs", "safety_remediation_gate_followup_contract.json");
    private static string FollowupPath() => Path.Combine(FindRepositoryRoot(), "docs", "safety_remediation_gate_unresolved_followup.json");

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
