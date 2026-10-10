# Text fonts

## Weight and requested decorations

Use `weight="bold"` for bold text; `regular`, `medium` and `semibold` are also available in the editor. Bindings and conditional styles work for weight. Actual glyph weight depends on the host's selected font.

Since 0.41.7 (host beta.16), Text supports `shadow`, `strokeColor` and `strokeWidth` in XML and the GUI Appearance section. Since 0.41.8, shadow defaults to false (also for blank or unavailable data). Set `shadow="true"` to enable the host shadow, including inside nested transparent stacks: every widget now has a native button root. Shadow colour, offset and blur are fixed by the host. The root clips to the tile, while internal layout containers are unchanged. An outline requires both a colour and a positive width; width defaults to zero and accepts fractions or percentages of the widget basis, up to 10%. Fill and outline colours have independent alpha. Use `opacity` to fade both.

```xml
<text id="title" size="20%" shadow="false" strokeColor="#000000c0" strokeWidth="0.6%">
  {{title}}
  <style when="warning == 1" strokeColor="#ff0000" />
</text>
```

All three settings support bindings and conditional styles. Empty strokeColor removes the outline. Stroke width and RGB stroke colour can be animated through transitionProperties; alpha changes are immediate. Outlines do not enlarge the text's layout box: leave padding around text, especially multiline clipped text. Rendering depends on client SVG-filter support. Italic remains unavailable as a dedicated property; select an appropriate font face instead.

## Font selection

Since 0.21.0, each `text` supports `fontFace`. Enter a Macro Deck font catalogue ID in XML or **Font (Macro Deck catalogue ID)** in the external editor. This is not a Windows font name, CSS font-family list or local font-file path. The catalogue picker is not integrated into the external editor yet; obtain an ID from the saved configuration of a standard button with the desired font selected.

```xml
<text id="title" fontFace="{{titleFont}}" size="18%" maxLines="2">
  {{title}}
  <style when="compact == 1" fontFace="{{compactFont}}" />
</text>
```

Set `titleFont` and `compactFont` in initial data or through display-data actions. Font changes apply immediately, without interpolation. Different text elements can use different IDs. Leaving the field blank, using an empty conditional override or referencing missing/null data omits the font property and restores the host default. An unavailable ID is sent to the host, whose text renderer falls back to its default face; the plugin cannot check catalogue membership offline.

Font size, weight, color/alpha, wrapping and multiline clipping remain independent. Font metrics differ: verify long titles and non-Latin text on the actual clients. Text waits for the requested face to load according to the [host text contract](https://docs.macro-deck.app/ui/components/text/).

The underlying `UiTextRun.FontFace` already exists in SDK beta.11, so this release does not change the minimum host version. Automated tests verify serialized font values, live patches, default restoration and editor persistence, not font availability or actual host font rendering.

