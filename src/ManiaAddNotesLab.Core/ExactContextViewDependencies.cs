using System.Collections.Immutable;

namespace ManiaAddNotesLab.Core;

/// <summary>Structural refinements implied by exact signatures. These edges describe nesting, not authority.</summary>
public static class ExactContextViewDependencies
{
    public static readonly ImmutableArray<(ExactCompletionContextView Parent, ExactCompletionContextView Child)> Graph =
    [
        (ExactCompletionContextView.ReducedOnly, ExactCompletionContextView.ReducedHeld),
        (ExactCompletionContextView.ReducedOnly, ExactCompletionContextView.ReducedPrevious),
        (ExactCompletionContextView.ReducedPrevious, ExactCompletionContextView.ReducedPreviousTransition),
        (ExactCompletionContextView.ReducedOnly, ExactCompletionContextView.ReducedNext),
        (ExactCompletionContextView.ReducedNext, ExactCompletionContextView.ReducedNextTransition),
        (ExactCompletionContextView.ReducedPreviousTransition, ExactCompletionContextView.ReducedPrevNext),
        (ExactCompletionContextView.ReducedNextTransition, ExactCompletionContextView.ReducedPrevNext),
        (ExactCompletionContextView.ReducedHeld, ExactCompletionContextView.ReducedHeldPrevNext),
        (ExactCompletionContextView.ReducedPrevNext, ExactCompletionContextView.ReducedHeldPrevNext)
    ];

    public static bool IsDependent(ExactCompletionContextView left, ExactCompletionContextView right)
    {
        bool Reachable(ExactCompletionContextView from, ExactCompletionContextView to,
            HashSet<ExactCompletionContextView> visited)
        {
            if (!visited.Add(from)) return false;
            return Graph.Where(x => x.Parent == from)
                .Any(edge => edge.Child == to || Reachable(edge.Child, to, visited));
        }
        return Reachable(left, right, []) || Reachable(right, left, []);
    }
}
