namespace Ziopuzzle.CustomButton;

// The client returns an opaque identifier, never an arbitrary result payload.
// Keep a bounded set of recent render snapshots so an in-flight press can use
// the value that was actually displayed before a data update.
internal sealed class ModalCompletion
{
    internal const int Capacity = LayoutLimits.XmlElements * 2;
    internal sealed record Answer(string ElementId, string Result);
    private readonly object gate = new();
    private readonly Dictionary<string, Answer> answers = new(StringComparer.Ordinal);
    private readonly Dictionary<Answer, string> tokens = new();
    private readonly Queue<string> order = new();
    internal volatile bool Opened;

    internal string Register(string elementId, string result)
    {
        lock (gate)
        {
            var answer = new Answer(elementId, result);
            if (tokens.TryGetValue(answer, out var existing)) return existing;
            var token = Guid.NewGuid().ToString("N");
            answers.Add(token, answer);
            tokens.Add(answer, token);
            order.Enqueue(token);
            if (order.Count > Capacity)
            {
                var oldest = order.Dequeue();
                tokens.Remove(answers[oldest]);
                answers.Remove(oldest);
            }
            return token;
        }
    }

    internal Answer? Resolve(string? token)
    {
        lock (gate) return token != null ? answers.GetValueOrDefault(token) : null;
    }
}
