# Additional drawing components

Every component requires a unique `id`. Common attributes include `fill`, `mainSize`, `visible`, `visibleWhen`, and conditional styles. `{{name}}` reads initial or received display data. Evaluate Macro Deck expressions such as `{{ vars.name }}` in an update action first.

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

