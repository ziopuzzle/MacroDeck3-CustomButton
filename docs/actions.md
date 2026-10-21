# Actions and data updates

The plugin uses the public native action editor and plugin events; no host patch is required. Actions is the initial configuration tab. Empty flows are not generated automatically.

New display-data actions (including the structured editing actions below) use the widget's current Channel instead of the default `demo`, including inside loops and branches. Their Channel can still be edited. Pasted or duplicated actions retain explicit non-default channels. Changing the sidebar Channel also updates these actions when their channel exactly matches the previous sidebar value; other channels, variable expressions and other providers' actions remain unchanged.

## Edit arrays and objects (0.48.2)

The action list groups data editing first, then copying/export, and finally Show modal.

| Action | Fields and purpose |
| --- | --- |
| **Set display value** | Channel, Target, Value, Input format. Set a whole value, object property or array item. This extends the existing action; there is no separate Update data value action. |
| **Set display data from JSON** | Initialize or replace the entire Channel with a JSON object. |
| **Insert array item** | Channel, Target array, Item to insert, Input format, optional Insert before index. Empty index appends; 0 inserts at the beginning. |
| **Remove display value** | Channel, Target, optional Store removed value in variable. Remove a value, object property or array item. |

Actions created with the earlier experimental multi-field editor must be removed and added again. Old parameters are rejected to prevent editing the wrong target.

### One target field

All three single-target editing actions use the same notation:

| Target | Meaning |
| --- | --- |
| `volume` | The whole value |
| `settings.volume` | A property inside an object |
| `playlist[2]` | The third array item (indices start at 0) |
| `playlist[2].title` | A property of the third item |
| `playlist[-1]` | The last array item; useful for popping a stack |
| `settings["a.b"]` | A literal property name containing a dot |

Target fields start empty; examples are help text, not prefilled data names. Quoted names use JSON string escaping. Existing exact top-level names take precedence: if a Channel contains a key literally named `settings.volume`, that key is selected. Use `["settings"].volume` to explicitly select the nested property instead. Legacy saved Set display value actions without an Input format parameter keep their original literal-name behavior.

Host variable bindings can supply part of a target (for example the index inside `playlist[...].title`). Insert the event's itemIndex using the host variable picker. These fields do not evaluate Custom Button XML functions.

### Values and initialization

**Text / variable value** is the default and preserves the existing Set display value behavior, including whitespace. **JSON** accepts a number, boolean, null, quoted string, object or array, such as `42`, `true`, `{"title":"Song"}` or `[]`.

Editing actions operate on **live Channel data only**. They do not load a widget's saved Initial JSON and have no source-widget selector. Initialize editable arrays/objects with **Set display data from JSON** before editing them; sidebar Initial JSON alone is only a rendering fallback. No intermediate object or array is created implicitly. Set may create the final object property or a new top-level value. Insert requires an existing array.

Changes notify renderers automatically and keep unrelated values. Missing targets, invalid JSON, out-of-range indices and payload-limit violations fail without changing the Channel. Removing an array item shifts later indices down. Removing a top-level live value may expose a value of the same name in a widget's rendering Initial JSON; deleting live data does not edit saved widget settings. Use JSON null with Set display value to explicitly override a fallback.

### Remove and store (pop)

Choose an existing writable Macro Deck variable in **Store removed value in variable**, or leave it empty to discard. The action owner's widget-local variable takes precedence over a global variable. Strings remain text; arrays/objects become JSON text; null becomes empty text. The host converts to the destination variable's type.

For a stack, remove `playlist[-1]`; for a queue, remove `playlist[0]`. Write the removed value to a text variable, then reference that variable in following actions.

Variable writing happens before deletion. A rejected write leaves data unchanged. The host variable store and Channel cannot form one transaction: if the source changes while the variable write is pending, the variable may already contain the old value, but removal fails rather than deleting a different item. Avoid concurrent writers for a pop sequence.

### Repeated control example

Initialize the Channel using Set display data from JSON:

```json
{"playlist":[{"title":"First","enabled":0},{"title":"Second","enabled":0}]}
```

In the repeated control's event, use Set display value. Build the Target as `playlist[<event itemIndex>].enabled` using the host variable picker for the index, select JSON input, and bind Value to the event's value (Toggle uses 0/1). For text, target `playlist[<event itemIndex>].title` and use Text input.

Repeat aliases remain read-only; controls still need separate Channel keys for their direct binding. Indices in a sliced/transformed Repeat refer to the result, not the original array. Account for the slice offset when building the target.


## Event targets

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
| Write display value to variable | Sets an existing writable Macro Deck variable using Source target, including nested properties and array items such as `settings.volume` or `playlist[-1].title`. Exact top-level names take precedence; use `["settings"].volume` to explicitly select a nested property. Select the destination variable name, not `{{ vars.name }}`. |

Channel copies preserve JSON strings, numbers, booleans, arrays, objects and null without converting their types. Arrays and objects are copied as whole values, not merged recursively. History is not copied; destination history follows the normal update rules. Missing keys or an empty/missing effective source fail without changing the destination. A limit-exceeding merge also fails without a partial update. Same-channel copying is supported, including copying a value to a different name.

New actions added inside Custom Button use the widget's Channel as the source. Set the destination explicitly. Changing the sidebar Channel or adopting a copied widget's automatic Channel updates matching source and destination references in that widget's transfer actions; unrelated channels are kept.

For variable writes, **Which variable to write** selects the destination, not the write operation: both options set the value. **Prefer this widget's variable; otherwise use global** follows the standard Set Variable action: a variable belonging to the action's owner widget takes precedence over a global variable with the same name. Widget-less runs use global variables. **Use the global variable (ignore this widget's variable)** bypasses widget-local variables. If there is no local variable, both choices reach the same global variable. This scope refers to the action owner, not the source Channel. Widget-local variables are created through Variables in the host action editor; they are separate from Custom Button initial JSON. Neither option searches other widgets. Variables must already exist; the action never creates them.

To verify a copy independently of the drawing, open the receiving Custom Button's **Diagnostics**, press **Refresh diagnostics**, and check **Current display data**. Copied values appear as `live`; the sidebar's Initial JSON remains unchanged. The receiving XML must use the copied names (for example `{{volume}}`), and its Channel must match Destination channel exactly. Copies run once; they do not keep two channels synchronized automatically. A later data-update action can overwrite a copied value.

The host applies the destination variable's type and precision. Strings retain whitespace, numbers use JSON's invariant numeric notation, booleans become `true`/`false`, and JSON null becomes an empty string. Arrays/objects become JSON text; use a text variable for these. Empty or incompatible values may be rejected by a numeric variable. Read-only, missing and unavailable variables return the host's failure rather than reporting success.

Example: after a slider changes `volume` on Channel `player`, use Copy display value with source `player` / `volume` and destination `overlay` / `volume` to update a separate modal. To persist it in a Macro Deck variable, create a numeric variable named `saved_volume`, then use Write display value to variable with source `player` / `volume` and destination `saved_volume`.

## Automatic artwork color updates

To obtain color directly from an Image/SVG element, use [averageColorKey](images.md#average-color-in-xml). To share artwork color through Channel data or other widgets, use the host variable below.

Macro Deck beta.17 already exposes an album-color variable for each player (`music_player_<player key>_album_color`). Select the actual variable from Macro Deck's variable picker; player keys depend on player names and name collisions.

To follow color changes without configuring a periodic Custom Button action:

1. Add **Variable changed** for that album-color variable.
2. Use **Set display value** to write its value (`{{ vars.SELECTED_VARIABLE_NAME }}`) to your Channel's `artworkColor` data name.
3. Read the same variable on **Display activated** to initialize the button when it becomes visible.

The host refreshes this variable every two seconds. This reuses the host's refresh mechanism; it is not a push notification from the player. `InstancesChanged` in the SDK only reports changes to the player list.

Both the variable and XML artwork color extraction use the host's artwork-ID cache. If a provider replaces image bytes while retaining the same internal artwork ID, the host can keep returning the old color. Repeated requests do not bypass that cache. Beta.17's built-in WebNowPlaying implementation derives the artwork ID from a hash of the cover bytes; an unchanged external cover URL does not necessarily mean an unchanged internal artwork ID.

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

