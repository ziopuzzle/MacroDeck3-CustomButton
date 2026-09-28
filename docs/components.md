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
Defaults are 0..100, -135..135 degrees and 4% thickness. `color` supports alpha,
which fades the entire gauge including its native background track. Track color
is host-controlled. This is a display component, not an input control.

## Built-in icon

```xml
<icon id="symbol" name="{{symbol}}" size="25%" color="#54dfcc80" />
```

`name` selects a glyph from the beta.14 SDK's built-in catalogue, such as `play`,
`pause`, `music-note`, `arrow-up` or `star`. The editor lists the supported names.
Omitting `name` uses `star`; an unknown name shows a validation error.
Omitting `size` fits the glyph to the smaller side of its box. Omit `color` to
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
charts, sliders, gauges, transforms, clocks and playback progress bars. Pressable
stacks/layers provide button behavior. Grid, list, toggle, segmented, dial and
text-field components, general-purpose modifiers and progress text are not yet
exposed as XML components. Internal use of a host component does not imply XML
or editor support for all of its properties.

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

Dynamic/progress text can be composed with text and values. Text input, lists, toggles, dials, segmented controls and grids are not exposed in this XML schema. Images use registered host resources. See [shapes](shapes.md) and [images](images.md).

