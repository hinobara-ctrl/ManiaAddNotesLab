using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManiaAddNotesLab.Core;

internal static class G1DesignValidationHardeningRunner
{
    private const string EntryHead = "964831978da269ccc2ada56b6fed7fe6a895ac2c";
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string Run(string corpusRoot, string contractPath, string publicOutputDirectory,
        string detailPath)
    {
        var run = G1DesignMembershipRunner.Run(corpusRoot, contractPath, publicOutputDirectory, detailPath);
        var summaryPath = Path.Combine(publicOutputDirectory, "g1_design_summary.json");
        using var summaryDocument = JsonDocument.Parse(File.ReadAllText(summaryPath));
        var summary = summaryDocument.RootElement;
        var expected = HistoricalCore();
        var actual = CoreAggregate(summary);
        var aggregateMatch = JsonSerializer.Serialize(expected) == JsonSerializer.Serialize(actual);
        var synthetic = AuditSyntheticNormalization(corpusRoot);
        var controls = AuditControls();
        var rngApiFree = typeof(InteriorRelationMembershipResearch).GetMethods()
            .Where(x => x.DeclaringType == typeof(InteriorRelationMembershipResearch))
            .SelectMany(x => x.GetParameters()).All(x => !typeof(IRandomSource).IsAssignableFrom(x.ParameterType));
        var constructionUnattributable = summary.GetProperty("constructionSupportProvenance")
            .GetProperty("Unattributable").GetInt32();
        var pass = aggregateMatch && synthetic.AggregateUnchanged && controls.RealEvaluatorControlsPass
            && controls.DesignContractControlsPass && rngApiFree && run.Deterministic
            && constructionUnattributable == 0 && run.ContractHash ==
                "15A16B6EFBF779CFF2C42A8C9A0DD46E025019252BEFA968E831233DC802AA68";
        var result = new
        {
            schema = "g1-design-validation-hardening.1",
            phase = "G1.DESIGN VALIDATION HARDENING / RECERTIFICATION",
            repositoryEntryHead = EntryHead,
            originMainAtEntry = EntryHead,
            baselineTests = 698,
            behaviorChange = false,
            recertificationStatus = pass ? "PASS" : "FAIL_NEEDS_REVIEW",
            designStatus = "COMPLETE_READY_SHADOW",
            readyMeaning = "Design readiness only; not behavioral promotion or authorization",
            contractHash = run.ContractHash,
            contractUnchanged = run.ContractHash == "15A16B6EFBF779CFF2C42A8C9A0DD46E025019252BEFA968E831233DC802AA68",
            corpusFingerprint = summary.GetProperty("corpusFingerprint").GetString(),
            implementationSnapshotHash = ImplementationSnapshotHash(),
            aggregateReproduction = new { exact = aggregateMatch, historical = expected, hardened = actual },
            candidateUniverseInterpretation = "Membership over enumerated candidate-builder shapes; not post-geometry placements or behavioral effects",
            syntheticNormalization = synthetic,
            constructionMembershipProvenance = new
            {
                exactAttributionAvailable = true,
                identity = "Reference-exact original object route to OriginalObservationId; candidate endpoint by exact materialized end time",
                decisionAuthority = false,
                counts = summary.GetProperty("constructionSupportProvenance")
            },
            rngCertification = new
            {
                method = InteriorRelationMembershipResearch.RngCertification,
                evaluatorApiAcceptsRng = !rngApiFree,
                weightedSelectionInvoked = false,
                repeatedEvaluationDeterministic = run.Deterministic,
                zeroCountersAreProof = false
            },
            badControls = controls,
            queryDenominators = new
            {
                universe = summary.GetProperty("queryCountUniverse").GetString(),
                reachedDistinctExactQueries = summary.GetProperty("reachedDistinctExactQueries").GetInt32(),
                noObservedResult = summary.GetProperty("queriesWithNoObservedResult").GetInt32(),
                oneObservedResult = summary.GetProperty("queriesWithOneObservedResult").GetInt32(),
                multipleObservedResults = summary.GetProperty("queriesWithMultipleObservedResults").GetInt32()
            },
            opportunityDistribution = summary.GetProperty("opportunityDistribution"),
            nextCandidate = "G1.GATE",
            nextAuthorized = false
        };
        File.WriteAllText(Path.Combine(publicOutputDirectory, "g1_design_validation_hardening_summary.json"),
            JsonSerializer.Serialize(result, Json) + Environment.NewLine, new UTF8Encoding(false));
        return pass ? "PASS" : "FAIL_NEEDS_REVIEW";
    }

    private static object HistoricalCore() => new
    {
        currentInteriorOpportunities = 298, candidateShapes = 4226, exactlyRepresentable = 4226,
        hypotheticalAdmit = 133, hypotheticalAbstain = 4093,
        states = new { CandidateObservedUnique = 76, CandidateObservedAmongAlternatives = 57,
            CandidateNotObserved = 2907, NoObservedRelation = 1186, UnresolvableExactIdentity = 0 },
        supportProvenance = new { SameParentOnly = 91, OtherParentOnly = 15, Both = 27, None = 0 },
        relationClasses = new { Contained = 1877, Crossing = 2221, EqualEnd = 128 }
    };

    private static object CoreAggregate(JsonElement s) => new
    {
        currentInteriorOpportunities = s.GetProperty("currentInteriorOpportunities").GetInt32(),
        candidateShapes = s.GetProperty("candidateShapes").GetInt32(),
        exactlyRepresentable = s.GetProperty("exactlyRepresentable").GetInt32(),
        hypotheticalAdmit = s.GetProperty("hypotheticalAdmit").GetInt32(),
        hypotheticalAbstain = s.GetProperty("hypotheticalAbstain").GetInt32(),
        states = JsonSerializer.Deserialize<object>(s.GetProperty("states").GetRawText()),
        supportProvenance = JsonSerializer.Deserialize<object>(s.GetProperty("supportProvenance").GetRawText()),
        relationClasses = JsonSerializer.Deserialize<object>(s.GetProperty("relationClasses").GetRawText())
    };

    private static SyntheticAudit AuditSyntheticNormalization(string corpusRoot)
    {
        var discovery = C11CorpusDiscovery.Discover(corpusRoot);
        var clean = new List<string>();
        var contaminated = new List<string>();
        foreach (var descriptor in discovery.UniqueHumanCharts)
        {
            var chart = OsuBeatmap.Parse(File.ReadAllText(descriptor.RuntimePath));
            var baseline = InteriorRelationMembershipResearch.EvaluateCurrentCandidateUniverse(chart);
            var maximum = chart.OriginalObjects.Max(x => x.EndTime ?? x.StartTime);
            var synthetic = ManiaObject.Ln(0, maximum + 1000, maximum + 1500, true, int.MaxValue,
                AddedObjectOrigin.LnInteriorOpportunity);
            var polluted = new ManiaChart { KeyCount = chart.KeyCount, Lines = chart.Lines,
                OriginalObjects = [synthetic, .. chart.OriginalObjects], TimingPoints = chart.TimingPoints };
            var hardened = InteriorRelationMembershipResearch.EvaluateCurrentCandidateUniverse(polluted);
            clean.Add(Projection(baseline));
            contaminated.Add(Projection(hardened));
            if (hardened.IgnoredSyntheticObjectCount != 1)
                contaminated.Add("SYNTHETIC_NOT_COUNTED");
        }
        var cleanHash = Hash(string.Join('\n', clean));
        var contaminatedHash = Hash(string.Join('\n', contaminated));
        return new SyntheticAudit(cleanHash == contaminatedHash, discovery.UniqueHumanCharts.Length,
            cleanHash, contaminatedHash, "Synthetic object injected into source.OriginalObjects for every C11 chart");
    }

    private static string Projection(InteriorRelationMembershipCorpusResult result) => string.Join('|',
        result.ChartFingerprint, result.CurrentInteriorOpportunityCount,
        string.Join(';', result.CurrentOpportunityIds), string.Join(';', result.Candidates.Select(x =>
            $"{x.Candidate.CandidateId}:{x.State}:{x.SupportProvenance}:{x.ConstructionSupportProvenance}")));

    private static ControlAudit AuditControls()
    {
        var candidate = Candidate("R:B");
        var synthetic = InteriorRelationMembershipResearch.Evaluate(candidate,
            [new(Occurrence("R:B"), true)]).State == InteriorRelationMembershipState.NoObservedRelation;
        var cross = InteriorRelationMembershipResearch.Evaluate(candidate,
            [new(Occurrence("R:B") with { ChartFingerprint = "other" })]).State
            == InteriorRelationMembershipState.NoObservedRelation;
        var frequency = InteriorRelationMembershipResearch.Evaluate(candidate,
            Enumerable.Repeat(new InteriorRelationVocabularyEntry(Occurrence("R:A")), 20)
                .Append(new(Occurrence("R:B")))).State
            == InteriorRelationMembershipState.CandidateObservedAmongAlternatives;
        var marginal = InteriorRelationMembershipResearch.Evaluate(Candidate("R:joint"),
            [new(Occurrence("R:duration")), new(Occurrence("R:endpoint"))]).State
            == InteriorRelationMembershipState.CandidateNotObserved;
        var mismatch = InteriorRelationMembershipResearch.Evaluate(Candidate("R:C"),
            [new(Occurrence("R:A"))]).State == InteriorRelationMembershipState.CandidateNotObserved;
        var clean = new InteriorRelationAdmissionTrace("x", "x", null, false, false, false, false, 1, 1);
        var replacement = InteriorRelationMembershipResearch.AuditTrace(clean with
            { ReplacementCandidateId = "y" }).CandidateSubstitutionViolations == 1;
        var articulation = InteriorRelationMembershipResearch.AuditTrace(clean with
            { ArticulationIntentCreated = true }).ArticulationFallbackViolations == 1;
        var rng = InteriorRelationMembershipResearch.AuditTrace(clean with
            { RngPositionAfter = 2 }).RngConsumptionViolations == 1;
        return new ControlAudit(synthetic && cross && frequency && marginal && mismatch,
            replacement && articulation && rng,
            new Dictionary<string, bool> { ["syntheticEvidenceRejected"] = synthetic,
                ["crossChartEvidenceRejected"] = cross, ["frequencySkewKeepsMinorityMember"] = frequency,
                ["marginalFragmentsDoNotJoin"] = marginal, ["exactMismatchRemainsMismatch"] = mismatch },
            new Dictionary<string, bool> { ["replacementTraceDetected"] = replacement,
                ["articulationTraceDetected"] = articulation, ["rngDivergenceTraceDetected"] = rng });
    }

    private static InteriorRelationCandidateIdentity Candidate(string result) => new("candidate", "chart",
        new OriginalObservationId(1), 0, 1000, 2m, InteriorAnchorKind.Head, 2000, 4m,
        InteriorEndRelation.Contained, "query", result, true);
    private static InteriorRelationOccurrence Occurrence(string result) => new($"occurrence-{result}", "chart",
        new OriginalObservationId(2), new OriginalObservationId(12), 0, 1, 0, 4000, 1000, 2000,
        0m, 8m, 2m, 4m, 2m, -4m, InteriorAnchorKind.Head, InteriorRelationClass.Contained,
        false, true, true, "query", result);

    private static string ImplementationSnapshotHash()
    {
        var files = new[] { "src/ManiaAddNotesLab.Core/InteriorRelationMembershipResearch.cs",
            "src/ManiaAddNotesLab.Core/InteriorRelationFeasibilityResearch.cs",
            "tools/ManiaAddNotesLab.Experiments/G1DesignMembershipRunner.cs",
            "tools/ManiaAddNotesLab.Experiments/G1DesignValidationHardeningRunner.cs",
            "tools/ManiaAddNotesLab.Experiments/Program.cs",
            "tools/DocConsistency/Program.cs",
            "tests/ManiaAddNotesLab.Tests/PhaseG1DesignInteriorRelationMembershipTests.cs",
            "tests/ManiaAddNotesLab.Tests/PhaseG1DesignValidationHardeningTests.cs" };
        return Hash(string.Join('\n', files.Select(path => $"{path}|{File.ReadAllText(path)}")));
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private sealed record SyntheticAudit(bool AggregateUnchanged, int InjectedCharts, string CleanHash,
        string ContaminatedHash, string Method);
    private sealed record ControlAudit(bool RealEvaluatorControlsPass, bool DesignContractControlsPass,
        IReadOnlyDictionary<string, bool> RealEvaluatorControls,
        IReadOnlyDictionary<string, bool> DesignContractControls);
}
