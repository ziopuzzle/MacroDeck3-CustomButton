# Display activation

Version 0.22.0 adds **Display activated** (`display-activated`). Use it to read current host variables when a saved Custom Button becomes visible, then use **Variable changed** for subsequent updates.

1. Open the button's Actions tab.
2. Select **Other (updates and variables)**, then **+ Add event → Display activated**.
3. Add display-data actions that read the current variables into the button's display channel. Use the same data keys as the XML bindings.
4. Keep the corresponding reads in your Variable changed flows and save the button.
5. Leave its page so no client displays it, wait more than two seconds, change a watched variable, and return. The activation flow should refresh the value without waiting for another variable change.

The event is also available under **Events → Custom Button**. The shortcut sets a concrete target widget ID; verify that target when duplicating the button. Activation does not automatically infer which variables to copy from your XML or other flows.

## Lifecycle

- The first successful widget snapshot marks that view ready. The event waits until `IEventPublisher.GetBindings()` includes a potentially matching activation binding, and checks again on `BindingsChanged`. This handles the first binding push arriving after the snapshot without a guessed startup delay.
- One occurrence is published per active widget, shared across clients. Repeated snapshots, a second client and immediate session replacement do not generate extra occurrences.
- When the last real view closes, a two-second grace period preserves the activation state. Returning within that interval does not run it again, even if the user deliberately switched pages; use the periodic display-update event if that short absence must also be synchronized.
- Preview, sample and drag-ghost views do not activate the event. The saved button must be displayed on a real widget surface. Draft flows are not executed through this event.
- A newly saved activation binding can run while the widget is already visible if that activation has not published yet. After publication, editing a flow does not replay it until a later activation.
- Initial values or cached data may appear briefly before the actions finish. This event refreshes data asynchronously; it does not block the first paint or replay changes that happened while hidden.
- Shutdown releases the binding subscription and clears state. Idle widget records are bounded to 128 and expired on subsequent acquisition; no per-widget background timer is added.

The [SDK event contract](https://docs.macro-deck.app/features/events/#reading-what-is-bound) exposes bindings but `Publish` remains fire-and-forget, without an action-completion acknowledgement. Additional host filters can prevent a configured flow from running. The plugin publishes once and does not retry actions automatically. Actual host subscription/execution timing still needs on-device verification; tests cover readiness, late bindings, target scoping, repeated snapshots, client sharing, replacement and reopening.

This is a visibility event, not a plugin startup event. It does not change the existing one-second display-update event or animate stored data.

