# Trackpad

`trackpad` is an absolute two-axis input surface, not an operating-system mouse or a relative touchpad. Choose the BASIC Trackpad template to try it.

- `keyX` / `keyY`: distinct display data keys (default `x` / `y`). Both values update together.
- `leftValue`, `rightValue`, `topValue`, `bottomValue`: values at the four edges (defaults 0, 100, 0, 100). Opposite values must differ; either can be larger. For Y increasing upwards use topValue="100" bottomValue="0". These replace the former minX/maxX/minY/maxY attributes.
- `stepX` / `stepY`: positive snapping increments anchored to the left/top value, including descending axes; 0 means continuous (default).
- `interactive="true"`: enable pointer input. Without it, the surface only displays the data position.
- `showCursorWhileTouching="true"`: show the crosshair only during contact. Default false keeps it visible. Release and cancellation hide it, including unchanged coordinates.
- `color` / `background`: crosshair and surface colors, including alpha.
- Standard layout, visibility and opacity attributes also apply.

Updating the two data keys through actions moves the crosshair. Out-of-range values are clamped for display without changing stored data.

Select the trackpad ID in Actions and add **Trackpad start**, **Trackpad changing** or **Trackpad end**. All expose `x`, `y`, `levelX`, `levelY`, `keyX`, `keyY`, `elementId` and `widgetId`. Levels describe physical position: 0 at left/top and 1 at right/bottom, even for descending ranges. All events also expose `startX`, `startY`, `previousX`, `previousY`. Start coordinates stay fixed throughout the gesture. Previous coordinates are from the preceding emitted position event, not from an external data update or each raw pointer sample. On start, both pairs equal the current coordinates. Subtract start for gesture displacement and previous for incremental movement. Start runs on contact, changing runs on movement; end runs on release, even if the position is unchanged. A cancelled contact keeps the last value without firing end.

Input uses the host pointer API and follows a drag outside the surface, clamping coordinates at its edges. It consumes that gesture instead of pressing the whole widget. One pointer controls a surface at a time; a new contact may take over after two seconds without samples to recover from a disconnected client. Multiple clients share widget state. Each move batch uses only its newest matching sample; drawing follows the existing render update schedule. The external editor preview is not an interactive input client.

Identical sessions replaced within two seconds restore contact ID, dimensions, start coordinates and previous coordinates. Checkpoints are single-use and isolated by widget, surface and configuration. If the client loses pointer capture or the plugin process restarts, touch again to begin a new gesture.
