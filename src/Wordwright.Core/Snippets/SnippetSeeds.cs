namespace Wordwright.Core.Snippets;

/// <summary>
/// The examples a new install starts with (docs/PLAN.md P2.6): <c>;date</c>,
/// <c>;thanks</c> and <c>;sig</c>. They are ordinary snippets — the Snippets page
/// edits or deletes them like any other.
/// </summary>
public static class SnippetSeeds
{
    public static SnippetDocument Default()
    {
        var now = DateTimeOffset.UtcNow;

        return new SnippetDocument
        {
            Snippets =
            [
                Create("date", "Today's date", "{date}", now),
                Create("thanks", "Thanks", "Thanks!", now),
                Create("sig", "Email signature", "Best regards,\nJaspreet", now),
            ],
        };
    }

    private static Snippet Create(string trigger, string name, string body, DateTimeOffset now) => new()
    {
        Id = Snippet.NewId(),
        Trigger = trigger,
        Name = name,
        Body = body,
        Enabled = true,
        CreatedUtc = now,
        UpdatedUtc = now,
    };
}