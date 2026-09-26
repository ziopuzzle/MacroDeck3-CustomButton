# Native block editor

Open **Drawing > Open editor** to launch the separate Avalonia editor. The template dropdown is below the launch button and applies XML plus initial JSON immediately. The editor uses no browser or web server.

## Assemble a layout

- Click anywhere in a component row to select it. Buttons and the drag handle keep their own behavior.
- Use Add component to insert a component, or a container's add control to append inside it.
- Drag the left handle to reorder. The upper/lower edges insert before/after; a container's center inserts inside it.
- Blue insertion lines show ordering; a blue frame shows a nesting target. Cycles and invalid leaf targets are rejected. The root only accepts insertions inside itself.
- Use the row menu to duplicate, delete or wrap in a stack. Collapse containers to hide their children in the editor without changing the drawing.
- Undo/Redo tracks up to 30 layout changes. It does not include host-side actions, initial JSON or template selection.

This edits order and parent/child structure, rather than directly dragging rendered controls on a free-position canvas.

## Properties and conditions

Valid property edits update the drawing automatically. Incomplete input keeps the last valid layout. A color swatch opens the picker; alpha uses CSS hex order. Data-key suggestions come from the initial JSON captured when opening the editor.

Use `{{value}}`, `{{value:F1}}` or arithmetic expressions in supported fields. A chart/slider key is a literal data key. Host variables must be evaluated in update actions first. Conditional styles override attributes when their expression matches. The editor selection frame does not recolor the actual widget.

## XML and saving

The XML tab supports validation and formatting. Container whitespace is normalized with two-space indentation, while text contents and mixed text/style whitespace are preserved. Invalid drafts are not applied. Input must be resolved before switching contexts; closing can offer apply/discard/cancel.

Edits are sent to the host configuration draft. Save in Macro Deck to persist them. If the host XML changes while this editor is open, conflict detection stops overwriting it; copy any needed draft, close and reopen. Closing the host configuration closes its editor.

Limits: 64 elements including styles, eight levels and 16,000 XML characters. Legacy saved forms still render, but editing XML or selecting a template switches to XML mode.

See [platform requirements](cross-platform.md) and [action configuration](actions.md).

