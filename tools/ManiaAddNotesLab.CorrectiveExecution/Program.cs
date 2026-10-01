internal static class Program
{
    internal static int Main(string[] args)
    {
        if (!Lane0CorrectiveExecutionCommand.TryParseArguments(
                args, out var repositoryRoot, out var authorizationRoot,
                out var corpusRoot, out var error))
        {
            Console.Error.WriteLine(error);
            return 64;
        }

        var result = Lane0CorrectiveExecutionHardening.Execute(
            new Lane0HardenedExecutionRequest(repositoryRoot!, authorizationRoot!, corpusRoot!));
        Console.WriteLine($"{result.Outcome}: {result.Reason}");
        return result.Outcome switch
        {
            "FEASIBILITY_DEMONSTRATED" or "LIMITED_PARK" => 0,
            "BLOCKED" => 2,
            _ => 1
        };
    }
}
