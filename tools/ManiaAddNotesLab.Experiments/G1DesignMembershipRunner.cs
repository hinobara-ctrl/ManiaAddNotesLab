using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManiaAddNotesLab.Core;

internal static class G1DesignMembershipRunner
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static G1DesignRunSummary Run(string corpusRoot, string contractPath,
        string publicOutputDirectory, string detailPath)
    {
        var discovery = C11CorpusDiscovery.Discover(corpusRoot);
        var keymodes = discovery.UniqueHumanCharts.Select(x => x.KeyCount).Distinct().Order().ToArray();
        if (discovery.UniqueHumanCharts.Length != 11
            || discovery.UniqueHumanCharts.Select(x => x.FamilyKey).Distinct(StringComparer.Ordinal).Count() != 11
            || !keymodes.SequenceEqual(new[] { 4, 7, 10 }))
            throw new InvalidDataException("G1.DESIGN requires the frozen 11-chart C11 inventory.");

        var rows = discovery.UniqueHumanCharts.Select(descriptor =>
        {
            var chart = OsuBeatmap.Parse(File.ReadAllText(descriptor.RuntimePath));
            return new G1DesignChartResult(descriptor with { RuntimePath = "" },
                InteriorRelationMembershipResearch.EvaluateCurrentCandidateUniverse(chart));
        }).ToArray();
        var repeated = discovery.UniqueHumanCharts.Select(descriptor =>
        {
            var chart = OsuBeatmap.Parse(File.ReadAllText(descriptor.RuntimePath));
            return InteriorRelationMembershipResearch.EvaluateCurrentCandidateUniverse(chart);
        }).ToArray();
        var firstIdentity = Identity(rows.Select(x => x.Result));
        var secondIdentity = Identity(repeated);
        var deterministic = firstIdentity == secondIdentity;
        var candidates = rows.SelectMany(x => x.Result.Candidates.Select(y => (Row: x, Value: y))).ToArray();
        var admitFamilies = candidates.Where(x => x.Value.HypotheticalAdmit)
            .Select(x => x.Row.Chart.FamilyKey).Distinct(StringComparer.Ordinal).Count();
        var status = candidates.Length > 0 && admitFamilies > 1 && deterministic
            && candidates.All(x => x.Value.RngCalls == 0) ? "READY" : "BLOCKED";
        var contractHash = CanonicalJsonHash(contractPath);

        Directory.CreateDirectory(publicOutputDirectory);
        WriteGlobal(Path.Combine(publicOutputDirectory, "g1_design_membership_summary.csv"), rows);
        WriteFamilies(Path.Combine(publicOutputDirectory, "g1_design_membership_by_family.csv"), rows);
        WriteProvenance(Path.Combine(publicOutputDirectory, "g1_design_support_provenance.csv"), rows);
        WriteDeterminism(Path.Combine(publicOutputDirectory, "g1_design_determinism_summary.csv"),
            contractHash, firstIdentity, deterministic, rows);
        var summary = Summary(status, contractHash, firstIdentity, deterministic, rows);
        File.WriteAllText(Path.Combine(publicOutputDirectory, "g1_design_summary.json"),
            JsonSerializer.Serialize(summary, Json) + Environment.NewLine, new UTF8Encoding(false));
        Directory.CreateDirectory(Path.GetDirectoryName(detailPath)!);
        File.WriteAllText(detailPath, JsonSerializer.Serialize(new
        {
            phase = "G1.DESIGN — Interior Relation Admission Contract / Shadow",
            behaviorChange = false,
            contractHash,
            corpusFingerprint = CorpusHash(discovery),
            charts = rows
        }, Json), new UTF8Encoding(false));
        return new G1DesignRunSummary(status, contractHash, rows.Sum(x => x.Result.CurrentInteriorOpportunityCount),
            candidates.Length, candidates.Count(x => x.Value.Candidate.ExactlyResolvable),
            candidates.Count(x => x.Value.HypotheticalAdmit), candidates.Count(x => !x.Value.HypotheticalAdmit),
            admitFamilies, firstIdentity, deterministic);
    }

    private static object Summary(string status, string contractHash, string identity, bool deterministic,
        IReadOnlyList<G1DesignChartResult> rows)
    {
        var candidates = rows.SelectMany(x => x.Result.Candidates).ToArray();
        return new
        {
            schema = InteriorRelationMembershipResearch.SchemaVersion,
            phase = "G1.DESIGN",
            status,
            behaviorChange = false,
            contractHash,
            corpusFingerprint = CorpusHash(rows.Select(x => x.Chart)),
            currentInteriorOpportunities = rows.Sum(x => x.Result.CurrentInteriorOpportunityCount),
            candidateShapes = candidates.Length,
            exactlyRepresentable = candidates.Count(x => x.Candidate.ExactlyResolvable),
            unresolvableExactIdentity = Count(candidates, InteriorRelationMembershipState.UnresolvableExactIdentity),
            hypotheticalAdmit = candidates.Count(x => x.HypotheticalAdmit),
            hypotheticalAbstain = candidates.Count(x => !x.HypotheticalAdmit),
            states = Enum.GetValues<InteriorRelationMembershipState>().ToDictionary(x => x.ToString(),
                x => Count(candidates, x)),
            supportProvenance = Enum.GetValues<InteriorRelationSupportProvenance>().ToDictionary(x => x.ToString(),
                x => candidates.Count(y => y.HypotheticalAdmit && y.SupportProvenance == x)),
            relationClasses = Enum.GetValues<InteriorEndRelation>()
                .Where(x => x is InteriorEndRelation.Contained or InteriorEndRelation.EqualEnd
                    or InteriorEndRelation.Crossing).ToDictionary(x => x.ToString(),
                    x => candidates.Count(y => y.Candidate.EndRelation == x)),
            queriesWithOneObservedResult = QueryCount(candidates, 1),
            queriesWithMultipleObservedResults = QueryCount(candidates, 2),
            memberInsideMultiResultSet = candidates.Count(x => x.HypotheticalAdmit
                && x.ObservedResultSignatures.Length > 1),
            absentFromMultiResultSet = candidates.Count(x => x.State == InteriorRelationMembershipState.CandidateNotObserved
                && x.ObservedResultSignatures.Length > 1),
            researchRngCalls = rows.Sum(x => x.Result.ResearchRngCalls),
            deterministic,
            semanticCandidateHash = identity,
            nextCandidate = "G1.GATE",
            nextAuthorized = false
        };
    }

    private static void WriteGlobal(string path, IReadOnlyList<G1DesignChartResult> rows)
    {
        using var w = Writer(path);
        w.WriteLine("scope,current_opportunities,candidate_shapes,exactly_representable,unresolvable,candidate_observed_unique,candidate_observed_among_alternatives,candidate_not_observed,no_observed_relation,hypothetical_admit,hypothetical_abstain,contained,equal_end,crossing,queries_one_result,queries_multiple_results,member_in_multi,absent_from_multi");
        WriteMembershipRow(w, "GLOBAL", rows);
        foreach (var row in rows.OrderBy(x => x.Chart.RelativePath, StringComparer.Ordinal))
            WriteMembershipRow(w, Csv($"CHART:{row.Chart.RelativePath}"), [row]);
    }

    private static void WriteFamilies(string path, IReadOnlyList<G1DesignChartResult> rows)
    {
        using var w = Writer(path);
        w.WriteLine("family,keymode,current_opportunities,candidate_shapes,exactly_representable,unresolvable,candidate_observed_unique,candidate_observed_among_alternatives,candidate_not_observed,no_observed_relation,hypothetical_admit,hypothetical_abstain,contained,equal_end,crossing,member_in_multi,absent_from_multi");
        foreach (var group in rows.GroupBy(x => (x.Chart.FamilyKey, x.Chart.KeyCount))
            .OrderBy(x => x.Key.FamilyKey, StringComparer.Ordinal))
        {
            var values = group.ToArray();
            var c = values.SelectMany(x => x.Result.Candidates).ToArray();
            w.WriteLine(string.Join(',', Csv(group.Key.FamilyKey), I(group.Key.KeyCount),
                MembershipValues(values, c, includeQueries: false)));
        }
    }

    private static void WriteMembershipRow(StreamWriter w, string scope, IReadOnlyList<G1DesignChartResult> rows)
    {
        var c = rows.SelectMany(x => x.Result.Candidates).ToArray();
        w.WriteLine(string.Join(',', scope, MembershipValues(rows, c, includeQueries: true)));
    }

    private static string MembershipValues(IReadOnlyList<G1DesignChartResult> rows,
        IReadOnlyList<InteriorRelationMembershipEvaluation> c, bool includeQueries)
    {
        var values = new List<string>
        {
            I(rows.Sum(x => x.Result.CurrentInteriorOpportunityCount)), I(c.Count),
            I(c.Count(x => x.Candidate.ExactlyResolvable)),
            I(Count(c, InteriorRelationMembershipState.UnresolvableExactIdentity)),
            I(Count(c, InteriorRelationMembershipState.CandidateObservedUnique)),
            I(Count(c, InteriorRelationMembershipState.CandidateObservedAmongAlternatives)),
            I(Count(c, InteriorRelationMembershipState.CandidateNotObserved)),
            I(Count(c, InteriorRelationMembershipState.NoObservedRelation)),
            I(c.Count(x => x.HypotheticalAdmit)), I(c.Count(x => !x.HypotheticalAdmit)),
            I(c.Count(x => x.Candidate.EndRelation == InteriorEndRelation.Contained)),
            I(c.Count(x => x.Candidate.EndRelation == InteriorEndRelation.EqualEnd)),
            I(c.Count(x => x.Candidate.EndRelation == InteriorEndRelation.Crossing))
        };
        if (includeQueries)
        {
            values.Add(I(QueryCount(c, 1)));
            values.Add(I(QueryCount(c, 2)));
        }
        values.Add(I(c.Count(x => x.HypotheticalAdmit && x.ObservedResultSignatures.Length > 1)));
        values.Add(I(c.Count(x => x.State == InteriorRelationMembershipState.CandidateNotObserved
            && x.ObservedResultSignatures.Length > 1)));
        return string.Join(',', values);
    }

    private static void WriteProvenance(string path, IReadOnlyList<G1DesignChartResult> rows)
    {
        using var w = Writer(path);
        w.WriteLine("support_provenance,hypothetical_admits,percentage_of_all_admits,charts,families");
        var totalAdmits = rows.Sum(row => row.Result.Candidates.Count(x => x.HypotheticalAdmit));
        foreach (var kind in Enum.GetValues<InteriorRelationSupportProvenance>())
        {
            var matching = rows.SelectMany(row => row.Result.Candidates.Where(x => x.HypotheticalAdmit
                && x.SupportProvenance == kind).Select(x => row)).ToArray();
            w.WriteLine(string.Join(',', kind, I(matching.Length),
                totalAdmits == 0 ? "0" : (100m * matching.Length / totalAdmits)
                    .ToString("0.0000", CultureInfo.InvariantCulture),
                I(matching.Select(x => x.Chart.RelativePath).Distinct(StringComparer.Ordinal).Count()),
                I(matching.Select(x => x.Chart.FamilyKey).Distinct(StringComparer.Ordinal).Count())));
        }
    }

    private static void WriteDeterminism(string path, string contractHash, string identity,
        bool deterministic, IReadOnlyList<G1DesignChartResult> rows)
    {
        using var w = Writer(path);
        w.WriteLine("contract_hash,corpus_hash,semantic_candidate_hash,research_rng_calls,deterministic_repeat,behavior_change");
        w.WriteLine(string.Join(',', contractHash, CorpusHash(rows.Select(x => x.Chart)), identity,
            I(rows.Sum(x => x.Result.ResearchRngCalls)), deterministic.ToString().ToLowerInvariant(), "false"));
    }

    private static int Count(IEnumerable<InteriorRelationMembershipEvaluation> values,
        InteriorRelationMembershipState state) => values.Count(x => x.State == state);
    private static int QueryCount(IEnumerable<InteriorRelationMembershipEvaluation> values, int cardinality) => values
        .GroupBy(x => $"{x.Candidate.ChartFingerprint}|{x.Candidate.QuerySignature}", StringComparer.Ordinal)
        .Count(group => cardinality == 1
            ? group.First().ObservedResultSignatures.Length == 1
            : group.First().ObservedResultSignatures.Length > 1);
    private static string Identity(IEnumerable<InteriorRelationMembershipCorpusResult> values) => Hash(string.Join('\n',
        values.SelectMany(x => x.Candidates).OrderBy(x => x.Candidate.CandidateId, StringComparer.Ordinal).Select(x =>
            $"{x.Candidate.CandidateId}|{x.State}|{x.SupportProvenance}|{string.Join(';', x.ObservedResultSignatures)}")));
    private static string CanonicalJsonHash(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return Hash(JsonSerializer.Serialize(document.RootElement));
    }
    private static string CorpusHash(C11CorpusDiscoveryResult value) => CorpusHash(value.UniqueHumanCharts);
    private static string CorpusHash(IEnumerable<C11CorpusChartDescriptor> values) => Hash(string.Join('\n',
        values.OrderBy(x => x.Sha256, StringComparer.Ordinal).Select(x =>
            $"{x.Sha256}|{x.FamilyKey}|{x.KeyCount}|{x.ObjectCount}|{x.LongNoteCount}")));
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static StreamWriter Writer(string path) => new(path, false, new UTF8Encoding(false));
    private static string I(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string Csv(string value) => '"' + value.Replace("\"", "\"\"") + '"';
}

internal sealed record G1DesignChartResult(C11CorpusChartDescriptor Chart,
    InteriorRelationMembershipCorpusResult Result);
internal sealed record G1DesignRunSummary(string Status, string ContractHash, int CurrentOpportunities,
    int CandidateShapes, int ExactlyRepresentable, int HypotheticalAdmit, int HypotheticalAbstain,
    int AdmitFamilies, string SemanticCandidateHash, bool Deterministic);
