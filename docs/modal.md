# Modal views

Version 0.42.0 adds **Show modal** to Custom Button's actions.
This proves that the plugin can serve arbitrary XML content in a native Macro Deck dialog.
It is not yet a dedicated modal-view editor. Custom Button events can open it when a destination device is selected.

1. Create a **standard Macro Deck button** and add Custom Button's **Show modal** to its Short Press actions.
2. Leave the sample XML and initial JSON in place for the first test.
3. Save and press the button on a connected graphical client. A native dialog should show Example and 42%.
4. Replace the XML/JSON with your own content, or set Channel to an existing Custom Button channel.
   Values already on that channel override initial JSON; subsequent Set display value/data actions update the open dialog.
5. To open it from a **Custom Button event**, add the same action there and select **Destination device**. The list uses the host's standard device picker source. A selected device takes precedence over the press origin. Its current connection is resolved on each execution, including after reconnection; an offline target fails without choosing another device.

XML and initial JSON are literal action parameters: `{{value}}` is expanded by Custom Button, not by the host action template renderer.
No channel data is overwritten when opening the dialog. Close it with the host's close button.
Interactive elements publish the existing Custom Button element events. **Event target widget** defaults to the button running Show modal; select another widget explicitly if needed. In the receiving event, select the same Target widget and the element's XML ID. Slider/control events include value, previousValue and other normal payload fields. Input also updates the dialog's Channel, just as it does in a widget.
Use distinct element IDs if the target widget itself has interactive elements and you want to distinguish them from dialog elements. Events deliberately share the same event types and target identity.
Whole-dialog presses and display-activated/tick events are not emitted. If you tested the unreleased Show details action, remove it and add Show modal again: its action ID changed from `show-details` to `show-modal`, and its view ID is now `custom-button-modal`. Existing dialogs should be closed and reopened.
Lengths use the dialog's available area; a layout designed for a square tile may need different proportions.
The dialog has a transparent stack root, without tile rounding or tile clipping. Set a background in XML if needed. Text shadow outside an explicit native button context remains subject to the host's text-shadow limitation. Scrolling and dedicated modal-layout editing are not part of this proof of concept.

## Checking interaction

For an interaction check, use initial JSON `{"value":42}` and this XML. Add Custom Button's element press event with ID `tap`, or its slider value-change event with ID `level`, to the opening standard button. Set the event's Target widget to that same button.

```xml
<stack id="details" padding="4%" gap="3%">
  <stack id="tap" interactive="true" background="#335577" padding="2%">
    <text id="caption" size="6%">Tap here</text>
  </stack>
  <slider id="level" key="value" interactive="true" min="0" max="100" />
  <text id="valueLabel" size="8%">{{value}}%</text>
</stack>
```

## Destination selection in beta.16

Custom Button publishes custom events for its presses. Host event flows do not retain an originating client ID.
The public modal API needs a client ID, but that ID can be supplied explicitly. Like the host's Change Folder action, Show modal resolves the selected device through the public `Deck.GetClients()` API and passes its current ClientId to the modal API.
With no selected destination, standard button press flows use their originating client; Custom Button events and backend flows report an error asking for a destination. There is no broadcast fallback. A device must support graphical dialogs; hardware-only device origins cannot render this view. Host acceptance does not guarantee that a device disconnecting during delivery actually displayed it.

See the official [modal views documentation](https://docs.macro-deck.app/ui/views/modal/).
