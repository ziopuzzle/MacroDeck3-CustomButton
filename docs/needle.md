# Angular lines and gauge needles

Use `cx`, `cy`, `length` and `angle` on a line. The angle is evaluated and interpolated before its endpoint is calculated, so the needle retains its length during a turn. Endpoint interpolation instead travels along the chord and can shorten the needle.

Initial data: `{"speed":0}`. Update `speed` with a display-data action.

```xml
<line id="needle" coordinates="local" cx="50%" cy="50%"
      length="36%" thickness="2%" color="#ff5555"
      angle="{{= -225 + clamp(speed, 0, 240) / 240 * 270}}"
      transitionMs="400" transitionProperties="angle" easing="ease-out" />
```

The example maps 0–240 to a 270-degree sweep. Zero degrees points right; positive angles turn clockwise. Angles accept -3600 through 3600 degrees. Interpolation follows the specified numeric range, not the shortest circular path: use 350 to 370 to cross zero clockwise, rather than 350 to 10. First display uses the current angle immediately.

Positions and length use the normalized drawing box. Use a square box for a circular sweep; a rectangular box stretches the sweep. Geometry extending beyond the box can be clipped. Do not combine angle mode with `x`, `y`, `direction`, or endpoint attributes (`x1`, `y1`, `x2`, `y2`). In the GUI, choose **Start + length + angle** under **Line geometry**; competing base attributes are removed automatically. **Two endpoints** and the legacy **Horizontal / vertical** mode are also available. Switching mode does not geometrically convert the previous line; inspect its position after switching. Conditional styles remain independently editable. `cx` and `cy` default to 50%, and `length` defaults to 40%.

# Renaming elements in the GUI

Edit **Element ID** just like any other field; valid input applies automatically. IDs must be unique, start with an ASCII letter, and contain at most 32 letters, digits, underscores or hyphens. Invalid input keeps the last valid layout. Selection follows the renamed element without rebuilding its input field; Undo restores the previous editing state. If an action event filters on that ID, update its filter separately in Macro Deck.

Sector bounds use the visible arc and center rather than the full parent circle. For example, `cx="50%" cy="100%" radius="50%" startAngle="180" sweepAngle="180"` draws the upper semicircle without requiring empty space below its center. This fixes geometry validation; it does not automatically crop or resize the allocated box, and stroke thickness can still extend beyond its edge.
