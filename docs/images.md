# Images

**beta.14 limitation:** Host `/api/music-player/artwork/...` and `/api/icons/...`
requests can return HTTP 401. These endpoints require client/admin authentication,
which the plugin session does not supply. Installed `icon-pack:UUID` references use
the latter endpoint and have the same limitation. Earlier successful local tests do
not establish supported access. Use local files or accessible HTTP/HTTPS image URLs
until a supported host image-resolution API is available. See [known issues](known-issues.md).

Version 0.23.0 uses the beta.13 SDK and requires Macro Deck 3.0.0-beta.13 or later.
Add **Image** in the external editor, then enter **Image source (file / URL / Icon Pack)**.
**Browse…** selects a local file. Paths refer to the computer running Macro Deck,
not to the phone. Macro Deck distributes the registered bytes to its clients.

```xml
<layer id="artwork">
  <image id="cover" source="{{coverUrl}}" size="100%" transition="crossfade">
    <style when="paused == 1" brightness="0.6" saturation="0.3" />
  </image>
  <text id="caption" size="12%" align="center">{{title}}</text>
</layer>
```

Set initial data to `{"coverUrl":"","title":"","paused":0}`.
Use **Set display value** to put the cover URL into `coverUrl`; the action can read
the appropriate Macro Deck variable, just like track titles. Add **Display activated**
to fetch its current value when the widget appears, and **Variable changed** for later changes.
Since 0.23.1, WebNowPlaying paths such as `/api/music-player/artwork/408e2083d7e2cca5?instanceId=app.macro-deck.webnowplaying%3A%3Abrowser` also work. Pass the value unchanged through `coverUrl`; the plugin resolves it against the host URL supplied by Macro Deck. No port setting or manual prefix is needed. Paths starting with `/api/` are reserved for host HTTP endpoints on every platform.

For a fixed image, use `source="C:\Pictures\cover.png"`, `/home/me/Pictures/cover.png`,
or an HTTP/HTTPS URL. Escape XML special characters such as `&` as `&amp;` in literal URLs.

| Attribute | Meaning |
| --- | --- |
| `source` | Absolute local path, HTTP/HTTPS URL, host `/api/...` URL, or `icon-pack:UUID`; supports display-data bindings and conditional styles. Empty or missing data draws no image. |
| `size` | Edge of the square image box, default `100%`. |
| `fit` | `contain` (default) shows the complete image. `cover` fills the square box and clips the overflow at its center. Both preserve aspect ratio. |
| `zoom` | 0.1–4, default 1. Multiplier applied after contain/cover fitting. |
| `offsetX`, `offsetY` | −1–1, default 0. Shift right/down by a fraction of the image box, after scaling. Use `0.2` for 20%. |
| `mainSize`, `fill` | Normal stack layout sizing. |
| `opacity` | 0–1; supports the existing opacity animation. |
| `brightness`, `saturation` | 0–2, default 1. Saturation 0 is grayscale. |
| `transition` | `none` (default) or `crossfade`, the host's fixed 220 ms artwork transition. |
| `visible`, `visibleWhen` | Normal visibility rules. |

Place an image inside an interactive stack to make it clickable, or inside a layer
to put text and controls over it. Image itself does not emit input events.

PNG, JPEG, WebP and GIF are accepted. SVG is not supported by the host resource API.
The plugin limits each file/download to 2 MiB; the host additionally enforces its
resource count and total-byte quotas (documented defaults: 256 resources, 16 MiB per plugin).
Large images should be resized first. No local file bytes or URL credentials are embedded
in drawing patches: only the handle returned by the host is sent.

Loading is asynchronous with a 15-second timeout. The first load reserves its layout space
but draws nothing until ready. Source changes keep the last successful artwork while loading;
blanking the source clears it. Failures retain the last artwork and log a diagnostic without
the source URL; retries back off from 1 to 30 seconds. Each active display session has one
resource slot per image element, reused across URL changes and removed when the session closes.
The display checks completed loads at its normal 100 ms maintenance interval.

Successful sources are checked every 30 seconds for changed content. Images are also
loaded again after session replacement.
There is no Icon Pack browser in the external editor yet. SVG conversion is not part of this release.

## Installed Icon Packs (0.24.0)

```xml
<image id="packIcon" source="icon-pack:01a08476-71b1-7f7f-b698-ece4d0db180e" size="80%" fit="contain" />
```

Use the **icon's** UUID, not the pack's UUID or name. Copy `icon.reference` from a standard
button's saved JSON after choosing the icon there. `source="icon-pack:{{iconId}}"` also works.
The image must be installed in the current Macro Deck instance. This shorthand resolves to
`/api/icons/UUID/image` on the configured host and uses the same bounded resource registration
as other image sources. The beta.13 endpoint has ClientAccess authorization; retrieval was
confirmed on the user's local installation. It is not a guarantee of access to a different
host's protected endpoints. No credentials are copied or added by the plugin.

## Cover (0.24.0)

```xml
<image id="cover" source="{{coverUrl}}" size="100%" fit="cover" opacity="0.8" />
```

The editor's **Image fit (contain / cover)** suggestions select the mode. The same attribute
can use a data binding or conditional style. The image canvas ratio is read from PNG, JPEG,
GIF or WebP headers. The host's transform and clipping components implement the crop; image
bytes are unchanged, transparency is retained, and the image does not become a button.
This release covers the existing square image box rather than an arbitrary rectangular area.
Unknown/malformed dimensions fall back to a ratio of 1; host decoding still validates the image.
Crossfade remains available, but a change in canvas ratio also changes the crop geometry immediately.

## Image framing (0.25.0)

```xml
<stack id="panel" background="transparent" align="center" justify="center">
  <image id="art" source="{{coverUrl}}" size="80%" fit="cover"
    zoom="{{zoom}}" offsetX="{{panX}}" offsetY="{{panY}}"
    transitionMs="250" transitionProperties="zoom offsetX offsetY" />
</stack>
```

Initial framing data: `{"coverUrl":"","zoom":1,"panX":0,"panY":0}`.
The editor exposes all three fields, value suggestions and display-data insertion.
Changing these values reuses the loaded image. Zoom is centered; translation is measured
against the original square box and is independent of zoom. Overflow is clipped. Zooming
out or moving too far can expose empty areas of the box, including with Cover.
These properties support the existing value-change transitions, bindings and conditional styles.

API references: [resources](https://docs.macro-deck.app/ui/reference/resources/),
[image component](https://docs.macro-deck.app/ui/components/image/).

## Refresh and recovery (0.31.2)

Visible images are checked again every 30 seconds, including files and unchanged URLs.
HTTP requests ask caches to revalidate. Identical bytes reuse the registered resource,
so checks do not unnecessarily restart GIFs or crossfades. A source change starts a
new load immediately unless the previous load is still finishing.

Failures retain the last available image and retry after 1, 2, 4, 8, 16, then 30 seconds
(capped at 30 seconds). Success or a source change resets the backoff. Empty sources
clear the image. Hidden elements do not request periodic refreshes.
Logs identify download, registration or removal failures without exposing source URLs.
This handles transient acquisition failures; it does not detect a resource silently
discarded by the host after successful registration.

