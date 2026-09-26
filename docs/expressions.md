# Calculations and display formats

Calculations read the button's display data. Macro Deck variables still enter through **Set display value** or **Set display data from JSON**; this syntax does not query host variables directly. The native editor uses the same evaluator as the plugin.

## Duration formatting uses seconds

`{{seconds:duration}}` formats a number of seconds. Since version 0.20.0, milliseconds must be explicitly divided by 1000. Use `floor` to discard the fractional part for nonnegative playback times:

```xml
<text id="time">{{= floor(position / 1000):duration}} / {{= floor(duration / 1000):duration}}</text>
<text id="remaining">Remaining {{= floor(max(duration - position, 0) / 1000):duration}}</text>
```

With `{"position":62000,"duration":185000}`, these display `1:02 / 3:05` and `Remaining 2:03`. The stored values can remain in milliseconds; the expressions perform the conversion before formatting. See [basic_progress.xml](../examples/basic_progress.xml) and its [initial JSON](../examples/basic_progress.json). The existing `progress-bar` still consumes milliseconds.

| Format | 185 seconds | 3723 seconds | Behavior |
|---|---|---|---|
| `duration` | `3:05` | `1:02:03` | Show hours only when needed |
| `duration:mm:ss` | `3:05` | `62:03` | Total minutes, not minutes modulo 60 |
| `duration:hh:mm:ss` | `0:03:05` | `1:02:03` | Always show total hours |

The first field is not zero-padded; following fields have two digits. Durations do not wrap at 24 hours. Fractional seconds are discarded toward zero. Negative durations have one leading minus sign; a magnitude below one second displays `0:00`. Use `trunc(milliseconds / 1000)` instead of `floor` when negative values should round toward zero. Use `max(value, 0)` to suppress negative values. Non-numeric duration data produces an error; missing data displays `—`. The supported magnitude is at most 9,007,199,254,740,991 seconds.

## Arithmetic

Use `{{= expression}}` in text or numeric attributes. `* / %` take precedence over `+ -`; use parentheses to group operations. `%` is remainder (the sign follows the left operand). A percent character outside the braces remains a unit suffix. Decimal and scientific literals such as `0.25`, `.5`, `1e3` and `2.5e-2` are supported.

| Function | Meaning |
|---|---|
| `floor(x)`, `ceil(x)`, `trunc(x)` | Round down, up, or toward zero |
| `round(x)`, `round(x, digits)` | Round to 0–6 decimal places; halfway values round away from zero |
| `abs(x)` | Absolute value |
| `min(a,b)`, `max(a,b)` | Smaller/larger value |
| `clamp(x,low,high)` | Limit a value to a range |
| `sqrt(x)`, `pow(x,y)` | Square root and power |
| `sin(x)`, `cos(x)` | Trigonometry in radians |
| `lerp(a,b,t)` | `a + (b-a)*t`; does not clamp `t` |

Examples:

```xml
<text id="percent">{{= used / total * 100:F1}}%</text>
<text id="seconds">{{= floor(position / 1000) % 60:00}}</text>
<bar id="level" value="{{= clamp(used / total, 0, 1)}}" min="0" max="1" />
```

Data names in expressions use letters, digits, underscores and dots; a hyphen is subtraction. Direct bindings such as `{{my-key}}` can still reference hyphenated names. Unknown/null data displays `—`. Non-numeric operands, invalid arguments and non-finite final results produce an error. Expressions have a 256-character limit and a 32-level nesting limit. These are bounded calculations, not JavaScript.

## Numeric formatting

Both direct bindings and calculated results accept `:format`, for example `{{value:F2}}` and `{{= value / 100:F2}}`. Supported formats are `0`, up to eight zero-padding digits (`00`, `000`), optionally followed by 1–6 decimal places (`000.00`), and `F`, `N`, `P` with optional precision 0–6. Formatting uses invariant culture. `P0` multiplies a ratio by 100 and adds a percent sign; do not multiply by 100 again. Non-numeric text with a numeric format remains unchanged.

Formatting and calculations happen when display data changes. They do not start an animation loop or advance playback automatically. The existing native progress bar can animate from its anchor/rate; text time displays follow incoming position updates.

