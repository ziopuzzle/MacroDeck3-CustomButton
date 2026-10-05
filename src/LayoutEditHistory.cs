namespace Ziopuzzle.CustomButton;

// The native editor and tests use the same transactional document operations.
public sealed class LayoutEditHistory
{
    private readonly List<string> undo = [], redo = [];
    private string? group;
    private DateTimeOffset lastEdit;
    private readonly TimeProvider clock;
    public string Xml { get; private set; }
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;
    public LayoutEditHistory(string xml, TimeProvider? clock = null) { _ = new LayoutDocument(xml); Xml = xml; this.clock = clock ?? TimeProvider.System; }
    public void EndGroup() => group = null;
    public void SetGrouped(string xml, string key)
    {
        var now = clock.GetUtcNow();
        var merge = group == key && now - lastEdit < TimeSpan.FromMilliseconds(800) && CanUndo;
        Set(xml, merge);
        group = key; lastEdit = now;
    }
    public string Edit(Func<LayoutDocument, string> edit)
    {
        var document = new LayoutDocument(Xml);
        var selected = edit(document);
        Set(document.Serialize());
        return selected;
    }
    public void Set(string xml, bool coalesce = false)
    {
        _ = new LayoutDocument(xml);
        if (xml == Xml) return;
        EndGroup();
        if (!coalesce || !CanUndo) { undo.Add(Xml); if (undo.Count > 100) undo.RemoveAt(0); }
        redo.Clear(); Xml = xml;
    }
    public void Undo() { EndGroup(); if (!CanUndo) return; redo.Add(Xml); Xml = undo[^1]; undo.RemoveAt(undo.Count - 1); }
    public void Redo() { EndGroup(); if (!CanRedo) return; undo.Add(Xml); Xml = redo[^1]; redo.RemoveAt(redo.Count - 1); }
}
