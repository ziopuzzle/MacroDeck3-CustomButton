# Images

## Average color in XML

Set `averageColorKey` on an Image or inline SVG to expose its average color as display data. Live widgets and modals also publish this value to their Channel. The source can use the usual data bindings. Use a unique literal output name per element; output names and fallback colors cannot be bound or overridden by styles.

```xml
<stack id="container" background="{{coverColor}}" padding="5%">
  <image id="cover" source="{{imageUrl}}" size="65%"
         averageColorKey="coverColor" averageColorFallback="#1a1a1a" />
  <text id="colorLabel" size="10%">{{coverColor}}</text>
</stack>
```

Set `imageUrl` in Initial JSON or with a display-data action to an absolute local path, ordinary HTTP/HTTPS image URL, or supported artwork path. Inline SVG uses the same two properties. The GUI exposes them under **Content and data**. Leaving the output name blank disables analysis; fallback defaults to `transparent` and accepts alpha colors.

- Local/HTTP images and inline SVG: a sampled, alpha-weighted RGB mean of the original image, before tint, opacity, crop, brightness or saturation. Transparent pixels do not darken the result; a fully transparent image returns `#00000000`. Animated images use their first frame. Analysis is serialized and limited to 16 megapixels per decoded image; analysis failures do not prevent an otherwise supported image from displaying.
- Artwork: uses the selected player's host album-color API. This may return the current or last-known album color rather than the exact historical artwork ID requested by the image. Host cache limitations still apply.
- Icon Packs: **not supported for color analysis in SDK beta.17**. The host returns a resource handle, not image bytes or a palette. Setting `averageColorKey` on such a source produces an explicit layout error; normal image display remains supported without that attribute.

A live widget or modal writes the result to its Channel under the output name. **Write display value to variable**, **Copy display value** and **Copy display data** can read it normally; no direct write to a Macro Deck variable is performed. Missing keys are initialized with the fallback. Existing Channel data is not replaced by an initial fallback while waiting for the first result. Only changed values are written. Settings previews and picker samples do not publish generated colors, so editing an unsaved preview cannot overwrite live data. After closing the live display, the last published Channel value remains until replaced. Use a dedicated output name: multiple live elements sharing a Channel and output name can overwrite one another.

Results appear asynchronously. The fallback is available for the initial frame; failed image loads retain the last successful image/color. Source changes trigger loading; unchanged URLs reuse the existing 30-second image recheck and content hash, avoiding a second download or repeated analysis of unchanged bytes. Artwork color is checked separately approximately once per second while visible. Queries for the same player are shared across widget, modal and preview sessions; color checks do not re-download or re-register images. SDK beta.17 exposes no artwork/color-change subscription, so this is demand-driven polling, not a change hook. Host state/cache refresh may add latency. Hidden/unopened elements do not promise refreshes. Do not make an image's source depend on its own generated color, which can create a feedback loop.

## Image sources

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
| `frame` | `square` (default) retains the existing square frame. `container` uses the rectangle allocated by the parent, ignoring `size`. Supports bindings and conditional styles. Also available on SVG. |
| `size` | Edge of the square image box, default `100%`. Ignored for `frame="container"`. |
| `fit` | `contain` (default) shows the complete image. `cover` fills the chosen frame and clips overflow at its center. Both preserve aspect ratio. |
| `zoom` | 0.1–4, default 1. Multiplier applied after contain/cover fitting. |
| `offsetX`, `offsetY` | −1–1, default 0. Shift right/down by a fraction of the image box, after scaling. Use `0.2` for 20%. |
| `mainSize`, `fill` | Square mode defaults to a natural `size` × `size` footprint (`fill=false`). Ordinary contain images can request expanded allocation, but their artwork still fits a square. Container mode defaults to `fill=true`; the parent's layout and `mainSize` determine the available rectangle. |
| `opacity` | 0–1; supports the existing opacity animation. |
| `color` | Optional single-colour tint, including alpha. Replaces all RGB colours while retaining the image's alpha mask. Blank preserves original colours. Supports bindings, conditional styles and colour transitions; colour alpha multiplies `opacity`. |
| `brightness`, `saturation` | 0–2, default 1. Saturation 0 is grayscale. |
| `transition` | `none` (default) or `crossfade`, the host's fixed 220 ms artwork transition. |
| `visible`, `visibleWhen` | Normal visibility rules. |

Place an image inside an interactive stack to make it clickable, or inside a layer
to put text and controls over it. Image itself does not emit input events.

For a monochrome Icon Pack image, add `color="{{iconColor}}"` (for example `#54dfcc80`). Tint uses the host's native image mask and does not download/re-upload the image. It also works on the `svg` component, recolouring all SVG parts together. On beta.15, clients without CSS mask support show the original colours, and tinted artwork does not crossfade. Brightness and saturation are applied to the tint too. Tint uses the selected image frame.

## Fit a rectangular widget (0.47.3)

Choose **Image frame → container** in the GUI, or set `frame="container"`. In a layer, the image uses the layer's available rectangle. This lets a landscape cover use a wide widget instead of being reduced to fit a square.

```xml
<layer id="player">
  <image id="artwork" source="{{artwork}}" frame="container" fit="contain"
         brightness="0.5" transition="crossfade" />
  <stack id="overlay" padding="4%" justify="end">
    <text id="title" size="10%" align="center">{{title}}</text>
  </stack>
</layer>
```

The image surface has a transparent background; no background color needs to be set. Letterboxing and transparent pixels show the underlying content. Set a background on a parent stack/layer if desired. The image has no press handlers and does not add its own rounded corners; ancestor clipping still applies. Existing child controls can remain above it in a layer.

Container mode supports all existing image sources, SVG, tint, opacity, zoom, offsets and transitions. `contain` still leaves margins when source and frame aspect ratios differ; use `cover` to fill the frame with cropping. In a stack, use `mainSize` to reserve space, or let the default `fill=true` share remaining space. Inside a scrolling layout, provide an explicit main-axis size or a bounded modifier; an unbounded scroll axis has no remaining space to fill. Existing XML remains in square mode.

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
This describes the default square mode. Since 0.47.3, container mode instead uses the host's native artwork surface to fit the allocated rectangle without requiring image dimensions.
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
against the selected frame and is independent of zoom. Overflow is clipped. Zooming
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

