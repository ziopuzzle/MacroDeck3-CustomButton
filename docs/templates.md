# Template gallery

New widgets contain only `<stack id="container"></stack>` and initial data `{}`. Choose a template from Drawing when needed. Selection replaces XML and initial data, while retaining channel, interaction settings and action flows.

The gallery is built directly from `examples/*.xml`. A same-stem `.json` provides initial data; if absent, `{}` is used. There is no second inline copy of the drawing. Add or edit examples and rebuild to change the gallery. Prefix drawing demonstrations with `basic_` for BASIC; other examples, currently prefixed `widget_`, are ADVANCED.

| Category | Template | File stem |
| --- | --- | --- |
| BASIC | Border | basic_border |
| BASIC | Text | basic_text |
| BASIC | Client time and date | basic_dynamic-text |
| BASIC | Built-in icons | basic_icon |
| BASIC | Image sources | basic_image |
| BASIC | Shapes | basic_shape |
| BASIC | Gradients | basic_gradation |
| BASIC | SVG | basic_svg |
| BASIC | Bar | basic_bar |
| BASIC | Range and marker | basic_range |
| BASIC | Arc gauge | basic_gauge |
| BASIC | Playback progress | basic_progress |
| BASIC | Conditional styles | basic_conditional |
| BASIC | Calculated sector | basic_calculation |
| BASIC | Animation | basic_animation |
| BASIC | Scroll | basic_scroll |
| BASIC | Repeat array | basic_repeat |
| BASIC | Modifier | basic_modifier |
| BASIC | Transform a group | basic_transform |
| BASIC | Responsive layouts | basic_responsive |
| BASIC | Toggle | basic_toggle |
| BASIC | Slider | basic_slider |
| BASIC | Dial | basic_dial |
| BASIC | Segmented | basic_segmented |
| BASIC | Text field | basic_text-field |
| BASIC | Trackpad | basic_trackpad |
| ADVANCED | Clock | widget_clock |
| ADVANCED | History Graph | widget_history-chart |
| ADVANCED | Meter | widget_meter |
| ADVANCED | Now playing | widget_nowplaying_player |

Templates supply drawing and initial data, not connected actions. Connect display-data updates and input events for your own variables/player. The updated Now playing template expects `position` and `duration` in seconds and uses an interactive seek slider. Divide millisecond source values by 1000 before passing them in; convert the slider event value back to the units required by the player's seek action. Existing widgets retain their saved XML and units when upgrading; selecting the updated template replaces XML and initial data but does not rewrite actions.

# Event identification and ordering

The host's Events menu displays each saved event's `eventName` and retains flow array order. The plugin refreshes labels for its own events and the standard Variable Changed event. Variable Changed includes the variable name; element events include the XML element ID. Other providers' labels are retained.

In Actions, select an existing event under **Event order**, then use **Move up** or **Move down** in the row below the selector. The numbered selector helps distinguish repeated labels. Moving past either end, or moving a deleted/stale selection, does nothing. This is an adjacent ordering control; it does not add drag handles inside the host-owned Events popup. Saved action blocks, event filters and trigger IDs are not changed by moving a flow. Save the widget to persist the result.
