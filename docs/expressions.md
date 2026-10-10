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

### Types and unavailable data

`number(key, fallback)` converts a finite number or numeric string, otherwise using the fallback.
`isNumber(key)` tests the JSON number type; `isNumeric(key)` tests whether a finite number can be obtained.
These predicates return 1/0 in calculations and true/false in conditions.
In conditions, `type(key)` returns `number`, `string`, `boolean`, `null`, or `missing`.
The key is a data name, not an expression. Conditions also accept quoted keys such as `type('my-key')`.
Calculation keys follow the existing identifier grammar (letters, digits, underscores and dots).
Calculation fallbacks can be arithmetic expressions; condition fallbacks are values or nested function calls.
This does not add array/object support to display data.

```xml
<text id="time">{{= floor(number(position, 0) / 1000):duration}}</text>
<text id="unavailable" visibleWhen="not isNumeric(position)">Unavailable</text>
<text id="textValue" visibleWhen="type(position) == 'string'">{{position}}</text>
<text id="ready" visibleWhen="number(duration, 0) &gt; 0">Ready</text>
<slider id="volume" key="volume" min="0" max="100" fallback="25" interactive="true" />
```

For Slider and Dial, `fallback` defaults to `min`. It applies to unavailable key data and direct
bindings such as `value="{{volume}}"`; the displayed value is clamped/snapped normally.
Source data is unchanged until an actual user interaction writes it. Other numeric attributes
such as `max` can explicitly use `{{= number(duration, 100)}}`.
Fallbacks do not suppress malformed expressions or invalid literal configuration.


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

