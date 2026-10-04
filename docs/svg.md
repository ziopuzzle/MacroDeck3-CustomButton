# Variable-driven SVG

The `svg` component renders self-contained SVG markup to a transparent PNG on the plugin machine. It uses the existing host image-resource API; clients do not execute SVG. Requires Macro Deck beta.15 or later.

Choose **BASIC → Variable SVG**, or add **Text and images → SVG** in the editor. The SVG markup field is multiline and applies valid edits automatically. In layout XML, put the SVG document inside CDATA:

```xml
<svg id="drawing" rasterSize="512" size="100%" fit="contain"><![CDATA[
<svg xmlns="http://www.w3.org/2000/svg" width="200" height="100" viewBox="0 0 200 100">
  <rect x="0" y="0" width="{{= clamp(value, 0, 100) * 2}}" height="100" fill="{{color}}" />
  <text x="100" y="60" text-anchor="middle" font-size="24" fill="#ffffff">{{title}} {{value:0}}%</text>
</svg>
]]></svg>
```

Initial data:

```json
{"title":"Usage","value":65,"color":"#2196f3"}
```

Use the existing display-data update actions/events to change these values. SVG bindings use the same display data and formatting/expression syntax as text. Values are assigned to XML attributes and text nodes, so `&`, quotes and `<` in titles remain literal text. Variables cannot insert elements or attribute names.

## Sizing and updates

- `rasterSize`: longest raster edge, 64–1024 pixels, default 512. Aspect ratio is preserved. Set explicit SVG width/height and a viewBox for predictable sizing.
- `size`, `fit`, `zoom`, `offsetX`, `offsetY`, `opacity`, `brightness`, `saturation`, and `transition` behave as on `image`. Image transforms/opacity can use existing value-change transitions.
- Optional `color` recolours all SVG parts together, preserving transparency. Leave it blank to use the SVG's own colours. It supports bindings, conditional styles and colour interpolation without re-rasterizing the SVG. Tint alpha multiplies `opacity`. The host requires client mask support and disables artwork crossfade while tinted.
- Changed SVG is rendered asynchronously, at most five times per second per element. Intermediate changes are coalesced to the latest value. The last successful image remains during rendering or a failed update.
- Unchanged expanded SVG is cached for the session; it is not periodically rasterized or uploaded. Reopening a session regenerates the resource.
- SVG attribute changes are immediate on the next image update; they do not use per-attribute interpolation. Use `transition="crossfade"` for an image crossfade. SVG/SMIL/CSS animations are not supported.

## Supported subset

Supported elements: `svg`, `g`, `defs`, `title`, `desc`, `rect`, `circle`, `ellipse`, `line`, `polyline`, `polygon`, `path`, `text`, `tspan`, `linearGradient`, `radialGradient`, `stop`, and `clipPath`. Use presentation attributes such as `fill`, `stroke`, `font-size`, and `transform`. Local `url(#id)` gradient/clip references are supported.

Stylesheets and `style` attributes, scripts, event handlers, DTDs, external references, `use`, embedded images, filters, and masks are not supported. Exported SVG may need simplification. Limits: 32,768 characters after expansion, 256 SVG elements, 16 levels, and 2 MiB of output PNG data. The enclosing layout's limits also apply.

Fonts are resolved on the plugin machine, not from the Macro Deck font catalogue or the client. Results can vary across operating systems. Use paths for lettering that must look identical everywhere. Rasterization uses Svg.Skia 2.0.0 with SkiaSharp 2.88.9, aligned with the native editor's Skia libraries; dependency notices are included in packages.

This component accepts inline SVG markup. The existing image file/URL loader still accepts raster images only. Icon Pack images imported from SVG continue to use `image` with `icon-pack:UUID`.
