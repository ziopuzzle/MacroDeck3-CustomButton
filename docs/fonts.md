# Text fonts

## Weight and requested decorations

Use `weight="bold"` for bold text; `regular`, `medium` and `semibold` are also available in the editor. Bindings and conditional styles work for weight. Actual glyph weight depends on the host's selected font.

The inspected **3.0.0-beta.13** `UiTextRun` exposes weight and font catalogue selection, but no italic or text-outline property. Version 0.26.0 does not add unsupported XML attributes or emulate these decorations with offset copies. Native support or a separate text rendering backend would be needed for explicit outline/italic styling. Selecting another font catalogue face remains possible.

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

