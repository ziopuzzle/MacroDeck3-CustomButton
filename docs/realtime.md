# Event-driven display updates

Version 0.18.0 separates data arrival from periodic acquisition. **Set display value**, **Set display values (JSON)** and native slider input notify open views on the same channel immediately. No drawing setting or XML change is required. The configuration preview receives the same notifications.

After an idle period, rendering starts when the notification is handled. During continuous traffic, the background worker waits 16 ms after each render, coalescing intermediate readings into the latest snapshot. This is a throughput limit, not a guaranteed 60 FPS or end-to-end latency. Complex layouts, host action execution, transport and the client add latency. Native input refreshes its own view directly after dispatch.

The queue holds one wake-up rather than a growing list of readings. Patches retain their base revision until drained, so a slow consumer receives a valid combined change. Data-only changes preserve live controls instead of replacing the widget tree.

## Connect a changing variable

1. In Actions, select **Other (updates and variables)** and add **Variable changed**.
2. Select the source variable, for example `game_speed`. Leave Changed to/from empty to accept every change.
3. Add **Set display value**: use the widget's channel, key `speed`, and value `{{ vars.game_speed }}`.
4. Display `{{speed:0}}` in a text element, or use `value="{{speed}}"` on a bar or slider.

The source must supply fresh values. A flow triggered by **Display update (1 second)** still acquires data once per second. Use source events/variable-change events for fast telemetry; this release does not add a game-data collector or a network listener. For related values, prefer one JSON update so one render sees a consistent group.

For sliders, use **Element value adjusting** when actions must run during dragging, and **Element value changed** for the final released value. The host controls native input-event frequency; faster rendering does not override its throttling. Avoid slow actions in frequent event flows.

## History and input safety

History remains limited to one point per second (the latest value in that second), with at most 120 points per key. It is not a high-frequency recording buffer. Background maintenance also keeps history and selection previews current when no data arrives.

A click or slider event can arrive after a newer data patch. Such input is accepted if the target node has remained present with the same type since the event's revision. Events targeting a removed and recreated node, an unknown node, a negative revision or a future revision are rejected. The current view still validates the declared event and payload.

## Verification scope

Headless tests cover burst coalescing, shared-channel previews, unrelated channels, history density, shutdown and input during data changes. Timing measurements describe plugin notification latency only. Test responsiveness on your actual Macro Deck client using the variable-change flow above. Arbitrary property interpolation and keyframe animation are separate future work; existing native clock/progress components continue to use the host's client-side timing.

