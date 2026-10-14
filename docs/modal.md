# Modal views

Version 0.42.0 adds **Show modal** to Custom Button's actions.
This proves that the plugin can serve arbitrary XML content in a native Macro Deck dialog.
Since 0.44.0, Show modal includes an Open editor control through its action-configuration UI. Custom Button events can open the modal when a destination device is selected.

1. Create a **standard Macro Deck button** and add Custom Button's **Show modal** to its Short Press actions.
2. Leave the sample XML and empty initial JSON (`{}`) in place for the first test.
3. Save and press the button on a connected graphical client. A native dialog should show Example inside a vertical scroll container.
4. Replace the XML/JSON with your own content, or set Channel to an existing Custom Button channel. Newly added Show modal actions inside Custom Button default to that widget ID (even if its sidebar Channel is customized); other widget types retain `demo`. Existing or explicitly configured action channels are preserved.
   Values already on that channel override initial JSON; subsequent Set display value/data actions update the open dialog.
5. To open it from a **Custom Button event**, add the same action there and select **Destination device**. The list uses the host's standard device picker source. A selected device takes precedence over the press origin. Its current connection is resolved on each execution, including after reconnection; an offline target fails without choosing another device.

XML and initial JSON are literal action parameters: `{{value}}` is expanded by Custom Button, not by the host action template renderer.
No channel data is overwritten when opening the dialog. Close it with the host's close button.
Interactive elements publish the existing Custom Button element events. **Event target widget** defaults to the button running Show modal; select another widget explicitly if needed. In the receiving event, select the same Target widget and the element's XML ID. Slider/control events include value, previousValue and other normal payload fields. Input also updates the dialog's Channel, just as it does in a widget.
Use distinct element IDs if the target widget itself has interactive elements and you want to distinguish them from dialog elements. Events deliberately share the same event types and target identity.
Whole-dialog presses and display-activated/tick events are not emitted. Modal opened is a separate initialization event (see below). If you tested the unreleased Show details action, remove it and add Show modal again: its action ID changed from `show-details` to `show-modal`, and its view ID is now `custom-button-modal`. Existing dialogs should be closed and reopened.
Lengths use the dialog's available area; a layout designed for a square tile may need different proportions.
The dialog has a transparent root, without tile rounding or tile clipping. Set a background in XML if needed. Text shadow outside an explicit native button context remains subject to the host's text-shadow limitation. GUI editing is available from the action settings; see below.

## GUI editing

Expand Show modal and click **Open editor**. The same native layout editor used for widgets opens with this action's XML and initial JSON. This action-level configuration does not require the owner to be a Custom Button; standard buttons can use it too. Clients without action configuration trees retain the ordinary parameter fields.

1. Edit the layout in the external editor.
2. Return to the action settings and click **Apply edited XML** under **Editor changes**. This explicit client change is necessary: the beta.16 action editor does not persist server-side value patches alone.
3. Save in Macro Deck, close the displayed modal and open it again.

Apply and Discard are matching side-by-side buttons. A warning above them explains that pending changes are temporary; after applying, a reminder asks you to save in Macro Deck. The reminder does not track the host Save button. Until step 2, changes are pending only. Apply them before collapsing the action or leaving the settings; closing a configuration session discards its pending edits. The **Edited XML (recovery copy)** field lets you keep a copy, and **Discard editor changes** restores the previous XML. Manual XML editing remains available after applying/discarding. If another edit changes the editor's base XML, its existing conflict protection stops overwrites.

There is no modal rendering preview in the external editor. Opening the editor does not open a modal or execute its events. Changing shared Channel data still refreshes an already open modal, but changing its action XML does not.

## Initial data and Modal opened

Initial data JSON supplies local fallback values only: values already written to the Channel take priority. Opening a modal never writes its initial JSON into the shared Channel. Use `{}` (the default for new actions) to disable fallback values. A widget's own initial JSON is local to that widget and is not automatically shared with the modal.

Add **Modal opened** under **Other (updates and variables)** in the target Custom Button's event shortcuts, or through the host event menu. Set Target widget to the same widget as the Show modal action's Event target widget. The event exposes `widgetId` and `channel`. Use the action editor's event-value picker to select Channel for Set display value(s), then read host variables through the usual variable picker.

The event runs once when the host requests the first tree for each modal session, including when missing values cause a rendering error. Channel updates then refresh the open modal. Snapshot requests and data refreshes within the same session do not repeat the event; closing and reopening creates a new session. A host session replacement also counts as a new session, so initialization actions should be safe to repeat. No event is emitted without an Event target widget. The target widget's event flow must be active in the host, as with other modal element events.

Initialization is asynchronous: initial fallback values or a missing-data error can briefly appear before the actions finish. To avoid this, update the Channel in the action sequence before Show modal. Do not open the same modal from its Modal opened handler, which would create an opening loop.

## Vertical scrolling

Since 0.43.0, use `<scroll id="page" direction="vertical">` in XML to let content grow beyond a bounded area. Scroll works in both Custom Button widgets and modals and appears under Layout in the GUI editor. The Basic Scroll template provides an example. Show modal has no scrolling checkbox. This uses the host's native vertical `UiList`, also used by the weather modal; the host handles the scrollbar, mouse wheel and touch gestures. Use direction="horizontal" for horizontal scrolling; see the example below.

Use a vertical stack for a page of sections. Text and ordinary vertical stacks have natural heights. `fill="true"` alone cannot give a chart, layer or control a height on an unbounded vertical axis: give a section inside the stack `mainSize`, or wrap it in a modifier with `height`. Percentages still use the modal's fixed drawing basis (its smaller dimension), not the entire page height. A `mainSize` on the single root directly inside the list is not used by the host list; put sized sections inside the root stack instead. Clipping modifiers or bordered button containers can still clip their own contents.

For a scroll check, paste the following XML (initial JSON can remain `{}`). The four sections total more than a landscape modal's height. Scroll to the last section and test its button using element ID `bottomButton`.

```xml
<scroll id="page" padding="4%">
<stack id="content" gap="3%">
  <stack id="first" mainSize="60%" background="#243447" padding="4%">
    <text id="firstTitle" size="8%">Top of the page</text>
    <text id="firstText" size="5%">Scroll down to see the other sections.</text>
  </stack>
  <stack id="second" mainSize="60%" background="#344724" padding="4%">
    <text id="secondTitle" size="8%">Section 2</text>
  </stack>
  <stack id="third" mainSize="60%" background="#473424" padding="4%">
    <text id="thirdTitle" size="8%">Section 3</text>
  </stack>
  <stack id="last" mainSize="60%" background="#382447" padding="4%">
    <text id="lastTitle" size="8%">End of the page</text>
    <stack id="bottomButton" interactive="true" background="#335577" padding="3%">
      <text id="buttonLabel" size="6%">Tap here</text>
    </stack>
  </stack>
</stack>
</scroll>
```

Ordinary value patches keep the list root mounted so the host retains the scroll position. Closing/reopening the modal creates a new view; scroll position is not saved across sessions. Mouse/touch behaviour must be verified on the actual client.

To scroll only part of a widget, put a scroll next to other elements in a stack and give the scroll `mainSize="60%"`, or let it fill the remaining height. A scroll needs a bounded height; a scroll nested inside another scroll needs a modifier with an explicit height or a bounded parent stack. Children directly in a scroll use natural height; put `mainSize`-sized sections inside a stack as in the example. To avoid whole-widget press effects during scrolling, disable Whole button interaction when only child controls should be actionable. Touch drags over sliders or trackpads may be captured by those controls; leave non-interactive space for scrolling.

## Horizontal scrolling

Select Horizontal in the GUI Direction field, or set `direction="horizontal"`. Scrolling then uses natural widths instead of natural heights. For sized cards, put a horizontal stack inside the scroll and assign `mainSize` to that stack's children (their width). Alternatively, give a direct child a modifier with an explicit `width`. Changing only the scroll direction does not change the layout direction of nested stacks.

```xml
<scroll id="carousel" direction="horizontal" padding="4%">
  <stack id="cards" direction="horizontal" gap="3%">
    <stack id="firstCard" mainSize="75%" background="#243447" padding="4%">
      <text id="firstCaption" size="12%">First</text>
    </stack>
    <stack id="secondCard" mainSize="75%" background="#344724" padding="4%">
      <text id="secondCaption" size="12%">Second</text>
    </stack>
    <stack id="lastCard" mainSize="75%" background="#382447" padding="4%" interactive="true">
      <text id="lastCaption" size="12%">Tap here</text>
    </stack>
  </stack>
</scroll>
```

The three cards exceed a square widget's width. A very wide modal may fit all three; add more cards to check scrolling there. Use a horizontal touch swipe, trackpad gesture or the horizontal scrollbar. The plugin does not convert a vertical mouse-wheel gesture into horizontal movement; that behaviour depends on the client/browser. Controls that capture horizontal dragging may compete with scrolling.

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
