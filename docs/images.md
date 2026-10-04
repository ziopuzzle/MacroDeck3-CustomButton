# Images

The BASIC **Image sources** template (`basic_image`) displays three sources side by side.
Set `iconPackId` to an installed icon UUID (without the `icon-pack:` prefix),
`localFilePath` to an absolute path on the host computer, and `artworkUrl` to the
music-player artwork path. In JSON, escape Windows backslashes, for example
`"localFilePath": "C:\\Pictures\\cover.png"`. Empty values show a hint instead of
loading an image. Update `artworkUrl` with a display-data action to follow track changes.

Requires Macro Deck **3.0.0-beta.15 or later**. Installed icons and music-player
artwork now use supported SDK resource APIs instead of unauthenticated HTTP.

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
WebNowPlaying paths such as `/api/music-player/artwork/408e2083d7e2cca5?instanceId=app.macro-deck.webnowplaying%3A%3Abrowser` work unchanged through `coverUrl`. The plugin extracts the artwork ID and qualified player instance ID and calls `UiResources.RegisterMusicPlayerArtworkAsync`. No temporary player or desktop credentials are needed. Other paths starting with `/api/` still resolve against the configured host URL.

For a fixed image, use `source="C:\Pictures\cover.png"`, `/home/me/Pictures/cover.png`,
or an HTTP/HTTPS URL. Escape XML special characters such as `&` as `&amp;` in literal URLs.

| Attribute | Meaning |
| --- | --- |
| `source` | Absolute local path, HTTP/HTTPS URL, host `/api/...` URL, `icon-pack:UUID`, or `artwork:/api/music-player/artwork/...`; supports display-data bindings and conditional styles. Empty or missing data draws no image. |
| `size` | Edge of the square image box, default `100%`. |
| `fit` | `contain` (default) shows the complete image. `cover` fills the square box and clips the overflow at its center. Both preserve aspect ratio. |
| `zoom` | 0.1–4, default 1. Multiplier applied after contain/cover fitting. |
| `offsetX`, `offsetY` | −1–1, default 0. Shift right/down by a fraction of the image box, after scaling. Use `0.2` for 20%. |
| `mainSize`, `fill` | Images default to a natural `size` × `size` footprint (`fill=false`) so the parent stack controls alignment. Ordinary contain images can explicitly request an expanded allocation with `fill=true` or `mainSize`; artwork is centered within it. Cover/zoom framing retains the square crop box. |
| `opacity` | 0–1; supports the existing opacity animation. |
| `color` | Optional single-colour tint, including alpha. Replaces all RGB colours while retaining the image's alpha mask. Blank preserves original colours. Supports bindings, conditional styles and colour transitions; colour alpha multiplies `opacity`. |
| `brightness`, `saturation` | 0–2, default 1. Saturation 0 is grayscale. |
| `transition` | `none` (default) or `crossfade`, the host's fixed 220 ms artwork transition. |
| `visible`, `visibleWhen` | Normal visibility rules. |

Place an image inside an interactive stack to make it clickable, or inside a layer
to put text and controls over it. Image itself does not emit input events.

For a monochrome Icon Pack image, add `color="{{iconColor}}"` (for example `#54dfcc80`). Tint uses the host's native image mask and does not download/re-upload the image. It also works on the `svg` component, recolouring all SVG parts together. On beta.15, clients without CSS mask support show the original colours, and tinted artwork does not crossfade. Brightness and saturation are applied to the tint too. Tint uses the square image frame even when the source dimensions are known.

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
The image must be installed in the current Macro Deck instance. Both this shorthand
and `/api/icons/UUID/image` call `UiResources.GetIconAsync` and use the returned
host-owned resource directly. Removing the widget does not remove the installed icon.

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

## Player artwork

Use `artwork:` followed by a host artwork path to request artwork through the
SDK player API rather than HTTP:

```xml
<image id="cover" source="artwork:{{artworkUrl}}" fit="contain" />
```

`artworkUrl` must contain `/api/music-player/artwork/ARTWORK_ID?instanceId=PROVIDER%3A%3APLAYER`.
If a player is available, its `GetArtworkAsync` result is registered as a UI resource.
There is no HTTP fallback. The former experimental `sdk-artwork:` prefix is no longer used.

The host resolves the other integration's player through the beta.15 SDK API.
The player must be enabled and the artwork ID must still be available. An unavailable
image retains the previous image and retries. No music player is registered by Custom Button.
Host resources have no pixel dimensions in their handles, so Cover, zoom and offsets
use client-side image framing with no press events.

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

