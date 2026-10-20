# Scroll anchor proof of concept

## Automatic follow variant

`follow-widget.json` uses the same eight rows with a slider selecting a zero-based target index. Paste it into a new Custom Button and save. Initial target 4 with offset 1 should leave rows 4–6 visible. Move the slider, release it, and wait about a second. The plugin performs the empty/prefix/full sequence internally; no phase variable or Wait action is configured. `followDelayMs` defaults to 300 ms per stage and can be increased. The original manual experiment below is retained for comparison.

## Manual phase variant

Development experiment, not a built-in template or an arbitrary-scroll guarantee. Requires Custom Button 0.48.1 and a client supporting native List version 3 (host beta.17).

Paste `widget.json` as the settings JSON of a new, disposable Custom Button. Open its settings and save so the event's `$self` target is resolved. This uses the isolated Channel `scroll-anchor-probe`; use only one test widget, or change the Channel and matching action parameters together. No music-player or external actions are configured.

Press **Run probe** once and wait about four seconds. Do not touch the list during the sequence. The viewport is three fixed-height rows tall:

| Phase | Anchor | Rendered rows | Expected observation |
| --- | --- | --- | --- |
| 0 | start | 1–8 | Reset following; position may be wherever you left it. |
| 1 | end | 1–5 | Move to bottom: rows 3, 4, 5. |
| 2 | end | 1–6 | Append row 6: rows 4, 5, 6; row 5 centered. |
| 3 | start | 1–6 | Disable following without changing the children. |
| 4 | start | 1–8 | Restore rows 7 and 8; ideally rows 4, 5, 6 remain visible. |

The flow uses Set display value (phase) and standard Wait blocks (800 ms). Phase 3 deliberately gets its own wait: disabling following and restoring rows in one update could race with the client's pending size observations. The delays give the client time to paint but are not acknowledgements. For slower clients, increase each Wait to 1500 ms. Keep the widget visible throughout. Concurrent runs are not serialized; do not press Run again until phase 4.

After phase 4, manually scroll to rows 1 and 8 to verify that all rows remain present. Run again from a manually changed position. Repeat in the actual client; a settings preview alone is insufficient. Check both first opening and repeated runs.

The XML keeps the Scroll node and its immediate Repeat child stable. Only rows inside Repeat are rebuilt. Hidden rows are absent from the generated tree, not just transparent. Anchor changes are property patches, not List replacement. Fixed row heights, zero gaps and a three-row viewport make the center prediction testable.

Possible failures: delayed/coalesced patches can skip phases; user movement can unpin the list; image/font reflow can change the position after following is stopped; resizing, session replacement or client scroll anchoring can move the viewport. This experiment does not preserve focus or input state inside regenerated rows. It does not add direct element targeting or horizontal anchoring.

`widget.json` includes a ready-made action flow based on the beta.17 action schema. If the host does not accept the pasted flow, add **Button press** for `run`, then repeat **Set display value → Wait** for phases 0, 1, 2, 3, finally **Set display value** to 4. Set Channel to `scroll-anchor-probe`, Data name to `phase`, and each Wait to 800 ms.
