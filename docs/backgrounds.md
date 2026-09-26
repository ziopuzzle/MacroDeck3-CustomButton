# Widget backgrounds (0.25.0)

With no background on the XML root, the widget uses `#1a1a1a`.
An explicit root background replaces this default; an active conditional style that sets
the root background also replaces it. No opaque default is painted behind that background.

```xml
<stack id="panel" background="transparent" justify="center" align="center">
  <text id="label" size="20%">Visible text</text>
</stack>
```

`#00000000` is another spelling for a fully transparent color. Text and images retain their
own opacity. To fade the content as well, use the root's `opacity` attribute.
RGBA colors such as `#ff000080` show a translucent tint over the client's underlying surface.
This controls the widget's drawing, not the Macro Deck window or deck background itself.

Both `stack` and `layer` accept `background`. A root image or text can be wrapped in a
transparent stack/layer when the whole widget should have no default fill.
Child backgrounds do not remove the root's default: make the XML root transparent as well.

```xml
<layer id="panel" background="transparent">
  <style when="warning == 1" background="#ff000080" />
  <text id="label">{{title}}</text>
</layer>
```

Root background bindings use ordinary display data, such as `background="{{background}}"`.
Existing explicitly colored templates keep their colors. Static borders can be used with
transparent backgrounds. The existing restriction on translucent backgrounds with animated
native button borders remains unchanged.

