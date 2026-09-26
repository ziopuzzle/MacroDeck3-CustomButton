# Actions and data updates

The plugin uses the public native action editor and plugin events; no host patch is required. Actions is the initial configuration tab. Empty flows are not generated automatically.

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

