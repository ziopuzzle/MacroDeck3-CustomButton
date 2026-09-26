# Value-change transitions

Set `transitionMs`, `transitionProperties` and `easing` on an XML element. They are also available as fields in the native block editor. Transitions are opt-in; existing buttons remain immediate. Both the live widget and Macro Deck configuration preview animate.

```xml
<bar id="level" value="{{value}}" min="0" max="100"
     transitionMs="400" transitionProperties="value" easing="ease-out" />
<text id="status" color="#ffffff" opacity="1"
      transitionMs="500" transitionProperties="opacity color" easing="ease-in-out">
  <style when="paused == 1" opacity="0.3" color="#88aaff" />
  Playback
</text>
```

For the example above, change `value` using a display-data action to animate the bar, or change `paused` between 0 and 1 to fade the text. The **Animation (BASIC)** template provides a separate color-transition demonstration: see [basic_animation.xml](../examples/basic_animation.xml) and its [initial data](../examples/basic_animation.json). Add actions that change its `color` value to exercise the transition.

- `transitionMs`: 0–10000 milliseconds; default 0 disables transitions.
- `transitionProperties`: space/comma-separated attribute names; default `opacity`.
- `easing`: `linear`, `ease-in`, `ease-out` (default), or `ease-in-out`.
- `colorSpace`: `linear-rgb` (default, interpolate linear-light channels), `srgb` (the former encoded-RGB interpolation), or `hsv` (shortest hue path, useful for vivid color cycles). For red to green the midpoints are respectively `#bcbc00`, `#808000` and `#ffff00`. HSV is not perceptually uniform and is a choice, not a universal improvement. Grey endpoints retain the colored endpoint's hue.

Supported properties, where the element supports them: `opacity`, `color`, `background`, `x`, `y`, `width`, `height`, `length`, `thickness`, `radius`, `cx`, `cy`, `angle`, `startAngle`, `sweepAngle`. `value` is supported on bars only. Use line `angle` transitions for a [rotating gauge needle](needle.md). Lengths normalize percent/fraction units before interpolation. Declare an explicit base color/background when changing it with a conditional style.

The first display uses its current values immediately. Later changes move from the current visible value to the new target, including when another update arrives mid-transition. Hidden elements are removed immediately and do not keep running transitions. Reappearing elements start at their current values. Use `opacity` for a fade rather than `visible`; zero opacity does not disable input.

Transitions affect presentation only. Stored data, history, text bindings and conditional expressions keep using actual data. Slider input is not delayed by interpolation. Text replacement, visibility, layout direction, slider values, progress timestamps, paths/points and color alpha are not interpolated in this release. RGB colors animate while alpha changes immediately; use `opacity` to animate transparency. Native animated borders/progress retain their existing behavior.

The plugin sends intermediate property patches using its existing bounded rendering worker (a 16 ms pause between background renders). This is not a client-side animation API or a guaranteed frame rate; host/client transport and native component transitions can affect smoothness. Frames stop once all targets are reached, and closing a view cancels its worker. Independent views own independent transition state. A high number of animated components may need shorter/simpler layouts. Repeating/keyframe animations are separate future work.

## Display session replacement

Transition state is retained for up to two seconds when a display session is immediately replaced with the same configuration and surface attributes. This avoids restarting the transition at its target. Continuity is not preserved across plugin restarts or incompatible layouts. Diagnostic logs report when animation state is restored. The previously reported visible jump improved in user testing; the host's reason for session replacement remains unknown.
