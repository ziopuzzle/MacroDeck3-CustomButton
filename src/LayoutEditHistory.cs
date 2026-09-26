namespace Ziopuzzle.CustomButton;

// The native editor and tests use the same transactional document operations.
public sealed class LayoutEditHistory
{
    private readonly List<string> undo = [], redo = [];
    public string Xml { get; private set; }
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;
    public LayoutEditHistory(string xml) { _ = new LayoutDocument(xml); Xml = xml; }
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
        if (!coalesce) { undo.Add(Xml); if (undo.Count > 30) undo.RemoveAt(0); }
        redo.Clear(); Xml = xml;
    }
    public void Undo() { if (!CanUndo) return; redo.Add(Xml); Xml = undo[^1]; undo.RemoveAt(undo.Count - 1); }
    public void Redo() { if (!CanRedo) return; undo.Add(Xml); Xml = redo[^1]; redo.RemoveAt(redo.Count - 1); }
}
