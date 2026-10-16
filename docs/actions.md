# Actions and data updates

The plugin uses the public native action editor and plugin events; no host patch is required. Actions is the initial configuration tab. Empty flows are not generated automatically.

New **Set display value** and **Set display data from JSON** actions use the widget's current Channel instead of the default `demo`, including inside loops and branches. Their Channel can still be edited. Pasted or duplicated actions retain explicit non-default channels. Changing the sidebar Channel also updates these actions when their channel exactly matches the previous sidebar value; other channels, variable expressions and other providers' actions remain unchanged.

After copying a widget, choose **Target this widget in all events** to overwrite every event widget-target field with the current widget ID, then save. Element IDs, variable filters and action parameters remain unchanged; events without widget targets are unaffected.

From 0.37.0, saving configuration records its owning widget ID in `configurationWidgetId`. After copying or importing such a widget, **open the destination configuration and save it**: event targets matching the recorded source ID are automatically replaced with the destination ID. References to other widgets remain unchanged. Save the source once with this version before copying or exporting it. Older data without this ID cannot be safely inferred; use the bulk button or edit targets manually. This is a configuration-time correction, not a copy/import hook: until the destination is saved, its stored event filters still refer to the source. References between multiple separately imported widgets are not remapped as a group.

An initial correction is published shortly after opening configuration, making the corrected draft available to the host's Save button without editing another field. The saved JSON is only changed when you save. Opening older data without an owner ID also makes the new ownership metadata available for saving.

Choose an input target, then select an event in **+ Add event**. Target widget/component IDs are inserted automatically. Select **Other (updates and variables)** for Display update (1 second) or the standard Variable changed event. For the latter, select the variable to watch in the created flow. Identical event conditions are not added twice; existing actions are preserved.

Native Short Press, Long Press, Touch Start and Touch End flows are converted to scoped Custom Button events. Saved `$self` targets are resolved to the widget ID when opening/editing configuration. Save before testing and review targets after duplicating a widget. Previews do not publish press events.

## Copy and export display data

These actions combine the source Custom Button's **saved Initial JSON** with its current Channel data. Channel values override matching initial keys, including explicit null. Unsaved editor drafts are not read. Copying is a one-time operation, not a subscription.

**Initial JSON from widget** can normally stay empty: the action reads the widget running it, only if that widget's Channel matches Source channel. To copy defaults from another Custom Button, select it explicitly. An explicitly selected widget with a mismatching Channel or unavailable configuration fails instead of silently using a different source. With no matching source widget, only live Channel data is available (for example in a standalone script or modal-only Channel). Modal-local fallback JSON is not stored in widget settings and is not included automatically.

| Action | Behavior |
|---|---|
| Copy display value | Copies one effective value to Destination channel. An empty Destination data name keeps the source name. Other destination keys remain unchanged. |
| Copy display data | Copies all effective keys. Merge overwrites matching keys and keeps other destination keys; Replace removes other destination keys. The default is Merge. |
| Write display value to variable | Sets an existing writable Macro Deck variable using one effective display value. Select the destination variable name, not `{{ vars.name }}`. |

Channel copies preserve JSON strings, numbers, booleans and null without converting their types. History is not copied; destination history follows the normal update rules. Missing keys or an empty/missing effective source fail without changing the destination. A limit-exceeding merge also fails without a partial update. Same-channel copying is supported, including copying a value to a different name.

New actions added inside Custom Button use the widget's Channel as the source. Set the destination explicitly. Changing the sidebar Channel or adopting a copied widget's automatic Channel updates matching source and destination references in that widget's transfer actions; unrelated channels are kept.

For variable writes, **Which variable to write** selects the destination, not the write operation: both options set the value. **Prefer this widget's variable; otherwise use global** follows the standard Set Variable action: a variable belonging to the action's owner widget takes precedence over a global variable with the same name. Widget-less runs use global variables. **Use the global variable (ignore this widget's variable)** bypasses widget-local variables. If there is no local variable, both choices reach the same global variable. This scope refers to the action owner, not the source Channel. Widget-local variables are created through Variables in the host action editor; they are separate from Custom Button initial JSON. Neither option searches other widgets. Variables must already exist; the action never creates them.

To verify a copy independently of the drawing, open the receiving Custom Button's **Diagnostics**, press **Refresh diagnostics**, and check **Current display data**. Copied values appear as `live`; the sidebar's Initial JSON remains unchanged. The receiving XML must use the copied names (for example `{{volume}}`), and its Channel must match Destination channel exactly. Copies run once; they do not keep two channels synchronized automatically. A later data-update action can overwrite a copied value.

The host applies the destination variable's type and precision. Strings retain whitespace, numbers use JSON's invariant numeric notation, booleans become `true`/`false`, and JSON null becomes an empty string. Empty or incompatible values may be rejected by a numeric variable. Read-only, missing and unavailable variables return the host's failure rather than reporting success.

Example: after a slider changes `volume` on Channel `player`, use Copy display value with source `player` / `volume` and destination `overlay` / `volume` to update a separate modal. To persist it in a Macro Deck variable, create a numeric variable named `saved_volume`, then use Write display value to variable with source `player` / `volume` and destination `saved_volume`.

## Display a variable

1. Add a display update or variable-change event.
2. Add **Set display value**, or **Set display values (JSON)** for multiple values.
3. Match the action channel to the sidebar channel.
4. Use a data key such as `cpu_value` and a Macro Deck expression such as `{{ vars.cpu }}` as its value.
5. Read it in XML as `{{cpu_value}}` or use `key="cpu_value"` on a chart.
6. Save in Macro Deck and test on the client.

The update event runs approximately once per second while the real widget or its preview is displayed. Both views share a timer; closing the last view stops it. Avoid long-running work in frequent event flows because host action execution can overlap later events.

History records the latest numeric value each second, including unchanged values. It does not invent values for periods when the widget was hidden. Non-numeric/removed values clear the affected history. Initial JSON supplies defaults until live data arrives. Data and history are transient.

The old per-button script selectors are retired. Run scripts through a normal action inside the desired event.

Display-data actions now wake the renderer as soon as data arrives. For updates faster than one second, use a source/variable-change event rather than the periodic display event. See [event-driven updates](realtime.md).

See [component inputs](controls.md) for slider values, nested buttons and the media-control example.

