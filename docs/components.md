# Additional drawing components

Every component requires a unique `id`. Common attributes include `fill`, `mainSize`, `visible`, `visibleWhen`, and conditional styles. `{{name}}` reads initial or received display data. Evaluate Macro Deck expressions such as `{{ vars.name }}` in an update action first.

## Scroll

`scroll` is a scrolling container, available in widgets and modals since 0.43.0. Add it under Layout in the GUI editor or start with the Basic Scroll template. It accepts child components, `direction="vertical"` (default) or `direction="horizontal"`, `gap`, `padding`, `background`, and common layout/visibility attributes. Its default `fill` is true. Give it a bounded viewport using a parent stack and `mainSize`, or a modifier with `width`/`height`.

Direct children use natural size along the scroll axis; a direct child's `mainSize` is ignored by the native list. Wrap sized sections in a stack pointing in the same direction. See [vertical scrolling](modal.md#vertical-scrolling) and [horizontal scrolling](modal.md#horizontal-scrolling) for examples and input-gesture considerations.

Since 0.48.1, optional `anchor="start|end"` enables native List version 3 anchoring for vertical Scroll. `end` follows the bottom while pinned; manually scrolling away suspends following. `start` disables following; it does not jump to the top. Bindings and conditional styles can switch the anchor. Omit the attribute to retain the previous component compatibility. Horizontal anchoring is not supported. See the separate [scroll anchor experiment](../experiments/scroll-anchor/README.md) for a staged attempt to center a row without a direct scroll API.

## Experimental Scroll follow (0.48.1)

Use `<scroll id="s" followKey="currentIndex" followOffset="1" followDelayMs="300">` with exactly one direct `repeat` child. `currentIndex` is a display-data name containing a **zero-based array index**, not an item key. For the fifth item use 4. Offset is a signed number of items: 0 puts the target at the bottom; 1 includes the next item. The resulting index is clamped to the array bounds. This does not measure row heights or guarantee centering. Use one visual row per item; hidden items still count as array indices.

On initial display and changes to the target or offset, the renderer empties Repeat under end anchoring, waits, renders through target+offset under end anchoring, waits, then restores all items under start anchoring. The default delay is 300 ms per stage (0–5000); increase it for slower clients. Zero adds no intentional phase wait, but transport delivery and the roughly 16 ms refresh cadence still apply. Very short delays may miss scrolling. A new target replaces an unfinished request. The delay starts after the host takes the snapshot/patch, not after client paint acknowledgement. Unrelated data changes do not restart completed following. Session replacement starts a new request.

While `followKey` is configured, it controls anchoring; normal display uses start so manual scrolling remains possible until the next target change. Missing, nonnumeric, negative or fractional targets disable following and show all rows. Remove/clear `followKey` to use ordinary `anchor="start|end"` again. `followOffset` supports bindings; `followKey` accepts a data name or a single binding, not a calculation. Follow is vertical only and cannot be placed inside Repeat. Rows are removed/recreated, so avoid active text input or other transient control state in the list. It is not a native scroll-to API.

Try [follow-widget.json](../experiments/scroll-anchor/follow-widget.json): the slider selects indices 0–7; offset 1 centers row 5 when target=4 in this fixed three-row viewport. No phase variables or Wait actions are needed.

## Repeat array (0.48.0)

`repeat` renders its child template once per array item. Add **Repeat** under Layout in the GUI, or choose **Repeat array (BASIC)**. Edit one template in the editor; generated rows are not separate editable elements. It works in widgets and modals and can be placed inside `scroll`.

Repeat supplies **read-only local variables**, not new Channel values:

| Attribute | Default variable | Meaning |
| --- | --- | --- |
| `itemAlias` | `item` | Current array value; use `item.title` for object fields. |
| `indexAlias` | `index` | Zero-based array position (first item is 0). |
| `keyAlias` | `itemKey` | Stable key resolved using the `itemKey` property path, or the index as text. |

Names shadow same-named Channel data **only inside the Repeat children**. Channel data is never overwritten. To retain access to Channel data named `item` or `index`, choose different aliases, for example `<repeat id="r" items="tracks" itemAlias="track" indexAlias="trackIndex" keyAlias="trackKey">`, then use `{{track.title}}` and `{{trackIndex}}`. Aliases must be distinct literal names made of letters, digits and underscores, starting with a letter or underscore. They cannot be bound or changed through styles. Nested Repeat with different aliases can read outer aliases; reusing aliases shadows them. Alias names do not change the event payload field names (`itemKey`, `itemIndex`, `itemJson`).

The GUI shows this scope explanation on Repeat. Data-name fields (control keys, followKey and averageColorKey) offer plain-name selection rather than value/function insertion. Selecting a name replaces the whole field without adding `{{...}}`; manual typing remains available. Array source also offers the function editor since 0.48.2.

```json
{"tracks":[{"id":"a","title":"First song"},{"id":"b","title":"Second song"}]}
```

```xml
<scroll id="playlist">
  <repeat id="tracks" items="{{tracks}}" itemKey="id" gap="3%">
    <stack id="row" interactive="true" padding="4%">
      <text id="title" size="12%">{{= index + 1}}. {{item.title}}</text>
    </stack>
  </repeat>
</scroll>
```

- `items` accepts a data name (`tracks`), a single binding (`{{tracks}}`), or an array expression such as `{{= split(text, ',')}}` / `{{= slice(tracks, 0, 10)}}`. It does not parse a JSON string into an array. Initial JSON and **Set display data from JSON** accept arrays and objects; [structured editing actions](actions.md#edit-arrays-and-objects-0490) can modify individual items without replacing the Channel. Set display data from JSON still replaces all Channel data.
- With the default aliases, inside the template, `item` is the current value, `item.title` / `item.metadata.title` access object fields, `index` is zero-based, and `itemKey` is the resolved key as text. Global display data remains available. These local aliases also work in conditions, calculations and SVG content. Primitive arrays use `{{item}}`. Nested repeats can use `items="{{item.tracks}}"`; reused aliases and event metadata refer to the nearest repeat.
- Optional `itemKey="id"` reads a literal item property path. Its values must be unique, nonempty strings or numbers (a string and number with the same spelling count as duplicates). Explicit keys retain internal drawing identities across reordering. Without itemKey, identity follows the array index instead. This does not implement automatic scrolling or guarantee preservation of scroll position when the list changes shape.
- Repeat arranges generated children like a stack: `direction="vertical|horizontal"`, `justify`, `align`, `gap`, `padding`, `background`, and common visibility/layout attributes. Wrap each item's multiple elements in a stack/layer if they should move as one row. Use matching scroll/repeat directions for horizontal lists. The usual scroll sizing rules apply.
- Missing/null/empty arrays render no items. Non-array values and missing/duplicate explicit keys show a located error. An empty template is allowed while editing.
- Template XML IDs stay unique in the document. Generated instances get separate internal IDs; event filters still use the original template ID (such as `row`). **Button press** and other control events additionally expose `repeatId`, `itemKey`, `itemIndex` and `itemJson` (JSON text). Select one event for the template and use its event values in actions.
- Item bindings are read-only. Interactive input controls must write to separate Channel keys (for example `key="volume_{{itemKey}}"` with suitable key characters), not `key="item.volume"`. To edit the source array, run Set display value from the control event, using a target such as playlist[2].volume and the event value. Local bindings are never automatically written back. Expression-generated arrays use indices of the resulting array; when editing the original array, explicitly account for a slice offset. Repeated image average-color outputs are not supported; ordinary images and SVG are supported.
- Data remains limited to 16,000 JSON characters and JSON depth 8. A repeat accepts at most 256 items. Total expansion, generated UI nodes, JSON depth and payload size remain bounded by the rendering limits below; effects may exhaust those limits before reaching 256 items. This is eager rendering, not a virtualized large-list view.

## Arc gauge

```xml
<gauge id="meter" value="{{value}}" min="0" max="100"
       startAngle="-135" endAngle="135" thickness="6%" color="#54dfcc"
       transitionMs="300" transitionProperties="value color" />
```

`gauge` uses the host's native arc renderer. Values are normalized to `min..max`
and clamped; `max` must exceed `min`. It fills the available space by default.
Angles are degrees: zero is up and positive is clockwise. The host limits the
end-minus-start sweep to one turn. For a ring use `startAngle="0" endAngle="360"`.
Defaults are 0..100, -135..135 degrees and 4% thickness. `trackColor` sets the
unfilled track colour; leave it blank for the host theme. `color` and `trackColor`
support independent alpha, bindings and conditional styles. Since 0.41.7, colour
alpha affects only its own paint; use `opacity` to fade the entire gauge.
This is a display component, not an input control.

## Built-in icon

```xml
<icon id="symbol" name="{{symbol}}" size="25%" color="#54dfcc80" />
```

`name` selects a glyph from the beta.14 SDK's built-in catalogue, such as `play`,
`pause`, `music-note`, `arrow-up` or `star`. The editor lists the supported names.
Omitting `name` uses `star`; an unknown name shows a validation error.
The default `size` is 20%. By default the icon occupies a square of that size,
without expanding in a horizontal stack or centering inside a taller box. Use
the parent stack's `justify` and `align` to position it. Explicit `fill="true"`
or numeric `mainSize` opts into a larger native box, where the host centers the
glyph. This changes the pre-release icon sizing introduced in 0.33.0. Omit `color` to
use the host's `role` (`primary`, `secondary`, `muted`). Names, colors and sizes
accept data bindings and conditional styles. These glyphs do not use Icon Packs
or the protected host image API.

## Transform a group

```xml
<transform id="group" rotation="{{angle}}" zoom="1" originX="0.5" originY="0.5"
           offsetX="0" offsetY="0" transitionMs="500" transitionProperties="rotation">
  <icon id="arrow" name="arrow-up" size="50%" />
</transform>
```

Children overlap like a layer. The transform scales, rotates clockwise about the
pivot, then translates them together. `originX/Y` and `offsetX/Y` are decimal
fractions of the element's own dimensions, including negative values; they are
not widget-basis lengths. Defaults are rotation 0, pivot 0.5/0.5, zoom 1 and no
offset. Zoom must be positive (at least 0.001). Rotation, pivot, zoom and offsets
can be animated through `transitionProperties`. Transform containers support
the same editor insertion, nesting and drag operations as layers.

Transforms do not reserve extra layout space for the rotated content. The host
does not define slider pointer mapping under rotation; keep interactive sliders
outside rotated groups.

## Component coverage

The XML supports stacks, layers, scrolling, text, images, SVG, built-in icons, shapes, range bars, charts, gauges, transforms, modifiers, responsive layouts, dynamic time text, clocks and playback progress bars. Input components include sliders, dials, toggles, segmented controls, text fields and trackpads. Pressable stacks/layers provide button behavior; in modals they can also close the view and return a result.

Grid and progress text are not exposed as separate XML components. Scroll uses the host list component internally; combine it with Repeat to display array data. Internal use of a host component does not imply XML or editor support for all of its properties.

## Range bar

`bar` is the XML name for the native `ui.range-bar`; the editor calls it Range bar.
`start`, `value` (the end) and optional `marker` use the same `min..max` scale,
not normalized fractions. All three are clamped to that range. Omit `start` to
start at `min`; omit `marker` to hide it. Existing bar XML remains valid.

```xml
<bar id="range" start="{{low}}" value="{{high}}" marker="{{current}}"
     min="0" max="100" color="#54dfcc" endColor="#2196f3" thickness="6%" />
```

Use `transitionProperties="start value marker"` to animate these positions.
The host paints the track and marker; the marker has no separate color setting.
Both end colors must have the same alpha, which fades the whole component.

## Dynamic time text

```xml
<dynamic-text id="time" format="time-24h" seconds="true" zone="Asia/Tokyo"
              size="25%" minSize="12%" align="center" />
```

The client formats and updates its own clock; no display-update action is needed.
`format` defaults to `time` and the editor lists the SDK's formats, including
`date`, `date-iso`, `date-long`, `time-12h`, `time-24h`, `zone-name` and `zone-offset`.
Omitting `zone` uses the client's time zone. Seconds default to false; the host
draws them smaller and muted. `size`, `sizeCap`, `minSize`, `weight`, `color`,
`role` and `align` follow text conventions. This component does not accept literal
text, fontFace or arbitrary date-format strings.

## Modifier

```xml
<modifier id="clip" clip="circle" padding="5%" width="80%" height="80%" opacity="0.8">
  <icon id="art" name="music-note" size="60%" />
</modifier>
```

Children overlap inside an implicit layer. The native modifier wraps that layer.
Use a stack child for sequential layout. Supported attributes are `padding`,
`opacity`, `clip` (`none`, `bounds`, `circle`, `capsule`), `radius` (rounds bounds
clipping), `width`, `height`, `minWidth`, `maxWidth`, `minHeight`, `maxHeight` and
`disabled`. Lengths use the same widget-basis fractions/percentages as other XML
lengths. Unspecified frame sizes stay unset. `disabled="true"` disables descendant
interactions. Visibility and conditional styles work as on other components.
Backgrounds, borders, masks and additional gestures are not exposed by this XML
modifier yet; use existing stacks/shapes for decoration.

## Responsive layouts

```xml
<responsive id="layouts">
  <variant id="compact"><icon id="compactIcon" name="music-note" /></variant>
  <variant id="wide" minAspect="1.5">
    <stack id="wideRow" direction="horizontal" align="center" fill="true">
      <icon id="wideIcon" name="music-note" size="25%" />
      <text id="wideLabel" size="18%">Music</text>
    </stack>
  </variant>
</responsive>
```

The first variant is the default; its bounds are ignored. Subsequent variants
are tried in order, and the first whose bounds all match is displayed. Variant
children overlap like a layer. Add Responsive in the editor, select it, then add
Layout variant; drag variants to change their priority/default. Only variants
may be direct children of responsive, and variants cannot be placed elsewhere.

`minWidth/maxWidth/minHeight/maxHeight` are decimal **deck-cell counts**, not
percentages. `minAspect/maxAspect` are positive width/height ratios. Minimums are
inclusive and maximums exclusive, subject to the host's rounding tolerance.
The client reevaluates on resize; the plugin does not guess the client size.
All branches are built and count toward limits, including hidden branches. A
variant hidden by `visibleWhen` is blank if selected; it is not removed from the
selection order. Older clients show a compatibility message rather than duplicate
interactive default content. `basic_responsive` demonstrates compact/wide layouts.

## Clock

```xml
<clock id="dial" zone="Asia/Tokyo" seconds="true" color="#ffffff" />
```

`zone` is an IANA timezone; omitted/unknown zones use the client's fallback. `seconds` defaults to true. `color` controls the main hands and markings; other dial appearance is native. The client resolves current time, so no periodic script is needed. Set `mainSize` when a clock should not fill the remaining space.

## Time-based progress

```xml
<progress-bar id="playback" positionMs="{{positionMs}}"
  durationMs="{{durationMs}}" anchor="{{anchor}}" rate="{{rate}}"
  color="#54dfcc" endColor="#8b9dff" thickness="6%" />
```

| Attribute | Meaning |
| --- | --- |
| positionMs | Position at the anchor, integer milliseconds; default 0 |
| durationMs | Total duration, integer milliseconds; omitted/0 produces an empty bar |
| anchor | ISO timestamp with timezone when the position was measured |
| rate | -16 to 16; default 0 (paused), 1 for normal playback, -1 for reverse |
| color / endColor | Start/end colors; end inherits start when omitted |
| thickness | Track thickness, fraction or percentage; default 5% |

A paused initial value can be `{"positionMs":42000,"durationMs":100000,"rate":0}`. For playback, send a fresh anchor with the measured position and rate. The client advances from that anchor and clamps to the duration. A nonzero rate requires an anchor; missing anchors are not replaced with the current time on unrelated updates. Use `bar` for a simple received fraction.

## Slider

```xml
<slider id="meter" value="{{value}}" min="0" max="100"
  direction="vertical" thickness="4%" color="#54dfcc">
  <style when="value >= 80" color="#ff6666" />
</slider>
```

The value is normalized and clamped to min/max (defaults 0/100). Direction defaults to horizontal; vertical and row/column aliases are accepted. Native UI draws the thumb and track. Sliders are read-only unless `interactive="true"` is set. Interactive sliders require a data `key` instead of `value`; see [controls](controls.md) for step sizes and events.

## Text field

```xml
<text-field id="queryInput" key="query" placeholder="Enter a search term" size="12%" interactive="true" />
```

A native single-line input, supported in widgets and modals. `key` binds to a display-data string (missing/null becomes empty). Read-only fields may instead use `value`, including `{{...}}` bindings; do not specify both. Numeric/boolean data is shown as text and an accepted edit stores a string. `placeholder` supports bindings, and `size` defaults to 12%. Common layout, visibility and opacity properties apply. The host owns field styling; this is not a multiline text area.

Without `interactive="true"`, or in a non-interactive preview, the field is read-only. An interactive field requires `key`. Add Text changing or Text changed for its element ID in the action editor. See [controls](controls.md) for payload and update timing. The Basic Text field template echoes the input without requiring any actions.

## SDK mapping

| SDK component | XML support |
| --- | --- |
| UiStack / UiLayer | stack / layer |
| UiTextRun | text |
| UiTextField | text-field |
| UiRangeBar / UiChart | bar / chart |
| UiButton | Outer press target and stack borders |
| UiClockDial / UiProgressBar / UiSlider | clock / progress-bar / slider |
| UiShape | rect / circle / capsule / line / polygon / sector / path |
| UiModifier | Alpha, opacity and shape frames |

Dynamic/progress text can be composed with text and values. Text input, lists, toggles, dials, segmented controls and grids are not exposed in this XML schema. Images use registered host resources. The `svg` component renders inline variable-bound SVG to these same resources. See [shapes](shapes.md), [images](images.md) and [variable SVG](svg.md).

## Layout limits

XML input accepts 65,536 characters, 512 elements (including styles), and 32 levels
including the root. These bounds protect parsing and editing; they do not guarantee
that every combination can be sent to Macro Deck.

The generated drawing is checked on each render: at most 2,000 nodes including
fallbacks, 56 KiB of root JSON, and JSON depth 26. Effects, wrappers, arrays and
properties contribute to the output independently of XML nesting. A limit failure
shows a drawing error with the exceeded limit instead of sending an invalid tree.

The beta.15 protocol permits a 192 KiB tree but only a 64 KiB update patch and JSON
depth 32. The plugin deliberately reserves space for patch and session envelopes.
If a property diff exceeds 60 KiB, a bounded root replacement is used instead; this
can reset in-progress controls or animations for that unusually large update.
Large rendered trees still cost more to draw, especially during animation.
