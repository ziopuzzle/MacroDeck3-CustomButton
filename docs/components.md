# Additional drawing components

Every component requires a unique `id`. Common attributes include `fill`, `mainSize`, `visible`, `visibleWhen`, and conditional styles. `{{name}}` reads initial or received display data. Evaluate Macro Deck expressions such as `{{ vars.name }}` in an update action first.

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

The XML supports stack/layer, text, images, built-in icons, shapes, range bars,
charts, sliders, gauges, transforms, modifiers, responsive layouts, dynamic time
text, clocks and playback progress bars. Pressable stacks/layers provide button
behavior. Grid, list, toggle, segmented, dial, text-field and progress text are not yet
exposed as XML components. Internal use of a host component does not imply XML
or editor support for all of its properties.

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

## SDK mapping

| SDK component | XML support |
| --- | --- |
| UiStack / UiLayer | stack / layer |
| UiTextRun | text |
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
