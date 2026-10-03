# Shapes and alpha (0.14.0)

## Local coordinate mode (0.27.0)

`coordinates="local"` is supported by rectangles, circles, capsules, paths, lines, polygons and sectors. The default remains `widget` for compatibility. Local shapes use their entire allocated box; `fill` defaults to true and `mainSize` is omitted unless explicitly provided. Allocate a region with a parent layer/stack or an explicit main size.

For rectangles/circles/capsules/paths and legacy length-based lines, `x/y/width/height/length/thickness` geometry is converted to a normalized native path. `x + width` and `y + height` must fit within 100%. Changing the parent box scales the geometry on each axis. A local circle becomes an ellipse in a nonsquare box; use a square region to keep it circular. Path coordinates are transformed including absolute curves and arc endpoints. Rotated arcs require equal width and height; axis-aligned arcs support independent scaling.

Endpoint lines, polygons and sectors already have normalized geometry; local mode additionally makes their allocation fill the parent rather than reserve one widget-basis unit. A zero-size parent still cannot display anything.

**Limits:** rectangle, circle and capsule gradient fills remain available only with widget coordinates. Line gradients support both coordinate modes. Stroke width and endpoint-line thickness still use the widget basis because the host's path stroke uses that basis even for normalized paths. Set these explicitly for small regions. This mode does not change text/font sizes, stack padding/gaps, images or other components.

## Two-point lines and gradients (0.26.0)

Two-point lines use `x1`, `y1`, `x2`, `y2`, all required, between 0–1 or 0–100%. Coordinates refer to the available path box, like polygons, rather than widget-basis lengths. Default main-axis size is one basis unit; the cross axis follows the parent. Use a square canvas for identical X/Y scales. Thickness uses the widget basis. Lines have flat ends and no fill. Identical endpoints or zero thickness draw nothing.

```xml
<line id="diagonal" x1="10%" y1="80%" x2="90%" y2="{{level}}%" thickness="2%" color="#54dfcc" />
```

Old `x/y/length/direction` lines are still supported, but cannot be combined with endpoints. New lines added through the editor start with endpoints. All four coordinates support bindings, expressions, styles and value-change transitions.

`rect`, `circle` and `capsule` support `gradient="none|linear|radial"` (default `none`). The first color is `color`; `endColor` defaults to the same color, including alpha. Linear `gradientAngle` defaults to 90 degrees: 0 up, 90 right, 180 down. Range: -360 to 360. Radial `gradientX` and `gradientY` specify the center in 0–1 or 0–100%, default 50%. Colors and gradient geometry support data bindings, styles and transitions; switching gradient type is immediate.

```xml
<rect id="gradient" width="80%" height="30%" corner="rounded" color="#2196f3a0" endColor="#54dfcca0" gradient="linear" gradientAngle="135" strokeColor="#ffffff" />
<circle id="disc" width="40%" height="40%" color="#ffffff" endColor="#2196f3" gradient="radial" gradientX="30%" gradientY="30%" />
```

This uses the host's [gradient background and clipping API](https://docs.macro-deck.app/ui/components/modifier/), without uploaded images or bands of shapes. Gradient colors must have equal alpha; use component opacity to fade the entire result. Strokes keep their independent color/alpha. Gradient circles require equal width and height. Path, polygon and sector gradient fills are not implemented because this API cannot clip a background to arbitrary path geometry. Multi-stop gradients and different alpha per stop remain future work.

See [the combined example](../examples/basic_gradation.xml). In the editor, Add component has subcategories; the component menu offers both Wrap in stack and Wrap in layer. Wrapping preserves the selected subtree and IDs, and supports undo/redo.

Requires Macro Deck **3.0.0-beta.11 or later**. Shapes use the public `UiShape` component added after [issue #777](https://github.com/Macro-Deck-App/Macro-Deck/issues/777). Sectors and polygons now use paths instead of stacks of horizontal bands. See the [official shape contract](https://docs.macro-deck.app/ui/components/shape/).

Add shapes in the editor, or select **Shapes (BASIC)**. Selecting a template replaces both XML and initial JSON with that example; it preserves the channel and action flows. Templates embed the files in `examples/` directly at build time.

## Placement

Translucent stack backgrounds retain a noninteractive, invisible sizing copy so automatic rows keep their natural height. This works around the beta.11 layer renderer's absolute-positioned children; foreground text keeps its own alpha. It adds rendering nodes and remains subject to the output-size limit. Version 0.17.0 includes a regression case for an auto-sized row of interactive media controls. On-device confirmation is still required.

Use a `layer` to overlap shapes. For `rect`, `circle`, `capsule`, `path` and `line`, positions and dimensions use the widget sizing basis. `10%` and `0.1` mean the same length. They are not percentages of a nested parent's remaining space.

| Element | Geometry |
| --- | --- |
| `rect` | `x`, `y`, `width` (40%), `height` (20%), `corner` (square or rounded), `cornerRadius` (6%) |
| `circle` | `x`, `y`, `width` (40%), `height` (40%); circle centered in the smaller dimension |
| `capsule` | `x`, `y`, `width` (40%), `height` (20%); radius is half the smaller dimension |
| `path` | `x`, `y`, `width` (40%), `height` (40%), absolute path commands in `data` |
| `line` | `x`, `y`, `direction` (horizontal or vertical), `length` (80%), `thickness` (2%) |
| `polygon` | `points="x,y;x,y;…"`, 3–64 points in 0–100%; automatically closed |
| `sector` | `cx`, `cy` (50%), `radius` (40%), `startAngle` (-90), `sweepAngle` (120) |

All shapes accept `color`. All except `line` also accept `strokeColor` and `strokeWidth` (1% when a stroke color is supplied). Use `color="transparent"` for an outline only. Use line's `thickness` for its width. No stroke is drawn when `strokeColor` is absent.

Sector and polygon coordinates cover their own box (0–1 horizontally and vertically). Their default main-axis extent is one widget basis unit; the cross axis follows the parent. A square canvas gives equal X/Y scales. Sectors use 0 degrees at the right and positive angles clockwise. A sweep of 360 or -360 draws a complete circle without a radial seam; zero sweep draws nothing. The sector's full circle must fit within its coordinate box. Self-intersecting polygons follow SVG's default nonzero fill rule.

```xml
<layer id="canvas">
  <sector id="usage" radius="40%" startAngle="-90" sweepAngle="{{= clamp(value / 100, 0, 1) * 360}}" color="#2196f3a0" strokeColor="#ffffff80" strokeWidth="1%" />
  <polygon id="arrow" points="30%,35%;70%,50%;30%,65%" color="#ffffff" />
</layer>
```

`resolution` was removed. Delete that attribute from old XML if it was explicitly set. The two-shape limit and scan-band limits no longer apply; the current [layout limits](components.md#layout-limits) apply to the XML and generated drawing data.

## Path data

`data` accepts absolute `M L H V C Q A Z` commands, SVG numbers and implicit repetition. Coordinates use 0–1 across the path box, not `%` suffixes. Relative/lowercase commands and complete SVG documents are not supported. Invalid commands, argument counts and arc flags are reported before sending to the host. Limit: 4096 expanded path characters.

```xml
<path id="curve" x="10%" y="50%" width="80%" height="30%" data="M0 1 C0.2 0 0.8 0 1 1" color="transparent" strokeColor="#54dfcc" strokeWidth="3%" />
```

This does not add SVG file loading, uploaded images or a JavaScript renderer.

## Alpha

Colors accept **CSS order**: `#RGB`, `#RGBA`, `#RRGGBB`, `#RRGGBBAA`, or `transparent`. Alpha is last: `#ff000080` is red at approximately 50% opacity; `00` is transparent and `ff` is opaque. The picker supports alpha and writes the same format.

The beta.11 reader accepts only RGB strings. The plugin sends six-digit RGB and `UiModifier.Opacity` instead. Backgrounds, text, static borders, shape fills and strokes are separated so one paint's transparency does not fade another. Transparency reveals underlying layers; the outer button still has its base background.

- Text, chart, clock, slider, shapes, stack backgrounds and static borders support alpha.
- Bar and progress-bar require equal alpha at both ends. Omitting `endColor` inherits `color`, including alpha.
- Animated borders require opaque background/border colors. Their component cannot isolate the animated outline from the default background. Static translucent borders use shape layers with a fixed rounded outline.
- Every XML element supports `opacity="0..1"` to fade the whole element, including children and animated borders. It multiplies paint alpha and supports styles and expressions.
- Legacy 0.2 presets retain their RGB-only renderer; current XML/editor settings use the new renderer.

```xml
<stack id="card" background="#20304080" padding="8%">
  <style when="value == 0" opacity="0.4" />
  <text id="label" color="#ffffff">{{value}}%</text>
  <bar id="level" value="{{value}}" color="#54dfcc80" />
</stack>
```

## Expressions and data

`{{value}}`, numeric formats such as `{{value:0.0}}`, `visibleWhen` and conditional `style` attributes work with shapes. Supply Macro Deck variables through the existing **Update display data** action.

```xml
<line id="usage" length="{{= clamp(used / capacity, 0, 1) * 80}}%" thickness="3%" />
```

With initial JSON `{"used":1500,"capacity":2000}`, this is 60% long. Expressions support arithmetic, remainder, rounding, range functions and more. See [calculations and formats](expressions.md) for the full syntax, including millisecond-to-duration formatting. Evaluation occurs on display updates, not as a separate animation loop.


## Line gradients (0.35.1)

All line modes support `gradient="none|linear|radial"`, `endColor`, `gradientAngle`, `gradientX` and `gradientY`, including `coordinates="local"`. The parameters use the same defaults as shape gradients. The gradient spans the line's allocated box, not the distance between its endpoints; use `gradientAngle` to choose its direction (0 up, 90 right, 180 down). For endpoint/angle lines, the box is the entire path canvas.

```xml
<line id="gradientLine" x1="10%" y1="50%" x2="90%" y2="50%" thickness="4%" color="#2196f3a0" endColor="#54dfcca0" gradient="linear" gradientAngle="90" />
```

Native opacity masks blend two identical line shapes without dividing the stroke into segments. Both colors must have the same alpha, applied once to the combined result. Bindings, conditional styles and transitions work with the same gradient attributes as other shapes. Two shapes and mask/layout wrappers count toward rendering limits.
