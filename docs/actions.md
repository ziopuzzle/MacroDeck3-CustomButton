# Actions and data updates

The plugin uses the public native action editor and plugin events; no host patch is required. Actions is the initial configuration tab. Empty flows are not generated automatically.

New **Set display value** and **Set display data from JSON** actions use the widget's current Channel instead of the default `demo`, including inside loops and branches. Their Channel can still be edited. Pasted or duplicated actions retain explicit non-default channels. Changing the sidebar Channel also updates these actions when their channel exactly matches the previous sidebar value; other channels, variable expressions and other providers' actions remain unchanged.

After copying a widget, choose **Target this widget in all events** to overwrite every event widget-target field with the current widget ID, then save. Element IDs, variable filters and action parameters remain unchanged; events without widget targets are unaffected.

From 0.37.0, saving configuration records its owning widget ID in `configurationWidgetId`. After copying or importing such a widget, **open the destination configuration and save it**: event targets matching the recorded source ID are automatically replaced with the destination ID. References to other widgets remain unchanged. Save the source once with this version before copying or exporting it. Older data without this ID cannot be safely inferred; use the bulk button or edit targets manually. This is a configuration-time correction, not a copy/import hook: until the destination is saved, its stored event filters still refer to the source. References between multiple separately imported widgets are not remapped as a group.

An initial correction is published shortly after opening configuration, making the corrected draft available to the host's Save button without editing another field. The saved JSON is only changed when you save. Opening older data without an owner ID also makes the new ownership metadata available for saving.

Choose an input target, then select an event in **+ Add event**. Target widget/component IDs are inserted automatically. Select **Other (updates and variables)** for Display update (1 second) or the standard Variable changed event. For the latter, select the variable to watch in the created flow. Identical event conditions are not added twice; existing actions are preserved.

Native Short Press, Long Press, Touch Start and Touch End flows are converted to scoped Custom Button events. Saved `$self` targets are resolved to the widget ID when opening/editing configuration. Save before testing and review targets after duplicating a widget. Previews do not publish press events.

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

