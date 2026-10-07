# Interactive components (0.15.0)

A widget can contain several independent press targets and sliders. This uses the host's native UI events and the normal action editor; it needs no host patch. Preview/ghost/sample surfaces do not publish input events.

## Start with the media template

1. Select **Now playing (ADVANCED)** in Drawing's template dropdown. It applies `examples/widget_nowplaying_player.xml` and the matching JSON. The separate **Open editor** button is above the dropdown.
2. Open Actions, choose `playPause` in **Input target**, then choose **Element press** in **+ Add event**. Selecting the event creates its flow immediately.
3. The widget and element IDs are filled in automatically. Add the installed player's play/pause action inside that flow.
4. Choose `next` and add Element press for next track, then repeat for `previous`.
5. To add volume control, insert a slider with `id="volumeSlider"`, `interactive="true"`, and `key="volume"`. Choose its ID and add **Element value changed**. Add the desired volume-setting action and select **Input value** from its event-value picker. The host represents this reference as `{ "$event": "value" }`.
6. Save in Macro Deck and operate the real widget on the client. The template does not automatically choose a player or attach actions.

The existing tile-level Short Press/Long Press flows are unchanged. A native child control claims its gesture, so pressing it does not also press the outer tile. Pressing an unclaimed area can still invoke the tile's own flow. The element's XML ID is independent of internal rendering paths and alpha wrappers. If an XML ID is renamed, update the matching event filters too. Copied widgets also need their target-widget filters reviewed.

## Event shortcuts (0.16.0)

**Input target** lists Whole button and interactive stack/layer/slider IDs from the current XML. The event menu only offers events supported by that target: four press gestures for buttons, or adjusting/changed for sliders. Conditional interactive elements are also listed; they emit only while enabled. Noninteractive text is excluded. The selectors are temporary UI state and are not saved as drawing settings.

Select **Other (updates and variables)** in the target menu to add **Display update (1 second)** or **Variable changed** from the event menu. Variable changed uses Macro Deck's standard `macro-deck / variable-changed` event. Select a watched variable in the new flow; Changed to/from are left unset so you can optionally add those filters. Once a variable has been chosen, select the shortcut again to create a flow for another variable. A pending unconfigured flow is not duplicated. Addition results and target-ID guidance appear above the action editor.

Shortcuts preserve all existing actions and avoid creating the same event with identical conditions twice. Deleting a flow allows the shortcut to recreate it. Other providers and advanced conditions remain available through the native Events menu.

## Press targets

Enable `interactive="true"` on a `stack` or `layer`. Its entire allocated rectangle is the hit area; give it `mainSize`, `fill` or content so it has space. Decorative child text and shapes need no separate event settings.

```xml
<stack id="playPause" interactive="true" mainSize="30%"
       background="#2196f3" justify="center" align="center">
  <text id="playLabel" size="14%">Play / Pause</text>
</stack>
```

| Event in the action editor | Event ID | Payload |
| --- | --- | --- |
| Element press | `element-press` | `widgetId`, `elementId` |
| Element long press | `element-long-press` | Same |
| Element touch start | `element-press-start` | Same |
| Element touch end | `element-press-end` | Same |
| Element value adjusting | `element-adjust` | `widgetId`, `elementId`, `value`, `level`, `key` |
| Element value changed | `element-change` | Same |

Choose the target widget and element ID in the event's configuration filters. The host's standard event-condition mechanism performs matching. `interactive` defaults to false and supports `style` conditions. Invisible or noninteractive elements cannot publish. A disabled child can allow its interactive ancestor to receive the gesture; disabling is not a pointer-blocking overlay.

If the host offers **This widget** (`$self`), the plugin resolves it to the actual widget ID when editing or reopening the configuration. Save the configuration once after upgrading to 0.15.1 to repair existing self-target filters. Explicit targets and other providers' events are preserved.

Use the interactive container's ID, not the ID of its text: in the media template these are `previous`, `playPause`, and `next`, not `previousLabel`, `playLabel`, or `nextLabel`. The Actions tab lists input target IDs and explains invalid local targets. Two labels inside the same container share its event; use an If/Else action for state-dependent play/pause behavior.

## Sliders

```xml
<slider id="volumeSlider" interactive="true" key="volume"
        min="0" max="100" step="1" direction="horizontal"
        mainSize="20%" color="#54dfcc" />
```

- `key` reads and writes the widget's display-data channel. It is required for interactive sliders. Use initial JSON such as `{"volume":50}`.
- `value` remains available for read-only meters and expressions; do not specify both `value` and `key`.
- `min`/`max` define the application value range. `step` is in those same units; zero/omitted means continuous. Ties round upward from min, matching the native slider.
- `value` in the event is the scaled/snapped application value (e.g. 75). `level` is its normalized 0–1 fraction. `key` is the display-data key, and `elementId` is the XML ID.
- Adjusting is sent during dragging (host limit: at most ten times per second). Changed is sent once on completion, including when the last adjusting event already carried that value. Use Changed for expensive operations.
- Changes immediately update display data and other widgets sharing that channel/key. This is transient plugin data, not an automatic write to Macro Deck variables, system volume or a media player. Connect an action to the event to affect those destinations.
- Incoming update actions remain authoritative and can overwrite the same display key. Keep periodic readback and your control action pointed at the same external setting. Restarting the plugin clears transient input data; initial JSON applies until fresh data arrives.

The client paints its local slider position during dragging. The plugin sends property patches for ordinary data changes to preserve the native control and pointer capture. Structural XML changes, hiding a control or styles that change its rendering structure may still replace its subtree and interrupt a gesture.

## Scope

This release implements press targets and one-dimensional sliders. Trackpads, arbitrary XY drag events, keyboard text inputs and other native input components remain future work. The current event payload and element-ID routing can be extended without making every component a tile-level button.


## Dial, toggle and segmented controls

The BASIC Dial, Toggle and Segmented templates provide working starting points. These components require a beta.15-capable client. Set `interactive="true"` and a `key` to enable input. Without interaction enabled they only display data. Use either `key` or `value`, never both. External data updates redraw the controls; user input updates the shared channel before publishing its event.

```xml
<dial id="volume" key="volume" min="0" max="100" step="1"
      startAngle="-135" endAngle="135" thickness="5%"
      color="#54dfcc" interactive="true" />
<toggle id="enabled" key="enabled" size="20%"
        mainSize="30%" color="#54dfcc" interactive="true" />
<segmented id="mode" key="mode" mainSize="25%" interactive="true">
  <text id="manual" size="10%" align="center">Manual</text>
  <text id="automatic" size="10%" align="center">Auto</text>
</segmented>
```

- **Dial:** numeric data in `min..max` (defaults 0..100), optional `step`, and start/end angles in degrees (zero up, clockwise positive). Events are Element value adjusting and Element value changed, with the scaled value and normalized level, as on a slider.
- **Toggle:** reads `true`/`false` or 0/1, and writes a JSON boolean to its data key. Element value changed reports numeric `value` and `level` as 0 or 1 to preserve the existing event contract. Use the shared data key when a boolean is needed.
- **Segmented:** each direct child is one choice. Choice content is centered vertically and horizontally by default; direct text children default to centered text alignment. Explicit text alignment and layouts inside child stacks remain configurable. The data key and Element value changed event use its zero-based index. Missing or out-of-range values show no selection. A hidden child keeps an empty slot so other indices remain stable. Child components are display-only; their interaction handlers are suppressed. Give the control an explicit height with `mainSize` when placing it in a vertical stack.

Choose the component under Input target in Actions, then add its event. Toggle and Segmented support changed events only. Color alpha and component opacity fade the entire control, including its native track and any segment contents.
