# Native block editor

Open **Drawing > Open editor** to launch the separate Avalonia editor. The template dropdown is below the launch button and applies XML plus initial JSON immediately. The editor uses no browser or web server.

## Assemble a layout

- Click anywhere in a component row to select it. Buttons and the drag handle keep their own behavior.
- Use Add component to insert a component, or a container's add control to append inside it.
- Drag the left handle to reorder. The upper/lower edges insert before/after; a container's center inserts inside it.
- Blue insertion lines show ordering; a blue frame shows a nesting target. Cycles and invalid leaf targets are rejected. The root only accepts insertions inside itself.
- Use the row menu to duplicate, delete or wrap in a stack or layer. Collapse containers to hide their children in the editor without changing the drawing.
- Undo/Redo tracks up to 100 layout changes. Continuous edits to the same field (including color picker changes) form one step; changing fields or pausing for at least 800 ms starts another. It does not include host-side actions, initial JSON or template selection.
- Undo discards incomplete property input first, keeping the last valid layout. A further Undo reverts the preceding valid edit. Unavailable history buttons are disabled. Keyboard shortcuts outside text inputs are Ctrl/Cmd+Z, Ctrl/Cmd+Shift+Z and Ctrl/Cmd+Y; text inputs retain their own text-editing undo.

This edits order and parent/child structure, rather than directly dragging rendered controls on a free-position canvas.

## Properties and conditions

Valid property edits update the drawing automatically. Incomplete input keeps the last valid layout. A color swatch opens the picker; alpha uses CSS hex order. For data-key suggestions and preview checks, the editor reads the channel values, initial JSON and shared defaults in that order. Reopen the editor to pick up changes to shared or channel values. Scroll-follow layouts validate every available row, including those first displayed after scrolling.

**Discard incomplete input** restores fields to the last valid layout without reverting applied changes or adding a history step. Use **Undo** to revert valid edits.

A warning marks the affected block and, when the field can be identified, its label. If an edit blocks a tab switch or another action, the status retains the validation message. **Show field** expands the property group and focuses the input.

An expression can be accepted even when the current data prevents a preview. Use **Show in XML** to select the reported property or component. **Revert** restores that property's last previewable value and keeps your other edits. If the editor cannot isolate the property, **Undo last edit** is available when the session has an earlier edit.

Use `{{value}}`, `{{value:F1}}` or arithmetic expressions in supported fields. A chart/slider key is a literal data key. Host variables must be evaluated in update actions first. Conditional styles override attributes when their expression matches. The editor selection frame does not recolor the actual widget.

The **{ }** control opens a searchable editor for display-data names, functions and operators. Type directly or choose an entry and click **Insert**. Changes update the main editor as you type, and closing the window keeps them. Data-name-only fields use a plain-name selector. See [inserting expressions](expressions.md#inserting-data-functions-and-operators-in-the-gui).

## XML and saving

The XML tab supports validation and formatting. **Format** changes only the text in the XML editor; use **Apply XML** to validate and apply it. Container whitespace is normalized with two-space indentation, while text contents and mixed text/style whitespace are preserved. **Discard XML changes** restores the last applied XML without changing layout history. Invalid drafts are not applied. Input must be resolved before switching contexts; closing can offer apply/discard/cancel.

Validation messages identify the component and its XML line and column. Duplicate IDs include the first definition's location as well as the conflicting definition. Applying invalid XML selects the reported attribute or line. Rendering errors also identify the originating component; conditional content that is not currently rendered may require different preview data to validate its values.

Edits are sent to the host configuration draft. Save in Macro Deck to persist them. If the host XML changes while this editor is open, conflict detection stops overwriting it. Closing the host configuration disconnects the editor but keeps its window and work available.

While disconnected or in conflict, further edits remain in this editor and the status keeps that warning visible. **Copy XML** is available from either tab. It copies unapplied XML edits too; incomplete GUI property input is excluded, so correct or discard those fields first. Closing with unsent or rejected changes offers **Copy XML and close**, **Discard and close**, or **Cancel**. The recovery prompt is skipped if that same applied layout was already copied; unapplied input still requires a decision. Reopen the editor from Macro Deck to resume connected editing.

Input limits: 512 elements including styles, 32 levels including the root, and 65,536 XML characters. Generated drawing data must also fit the output limits described in [Components](components.md#layout-limits). Legacy saved forms still render, but editing XML or selecting a template switches to XML mode.

See [platform requirements](cross-platform.md) and [action configuration](actions.md).

