# Changelog

## 0.39.4 — previous control values

- Add numeric previousValue to Value changing/changed events for Slider, Dial, Toggle and Segmented. Existing event IDs are unchanged.
- Read the stored value atomically before applying each input, including external updates. Initial values or component defaults apply before the first stored value. Toggle uses 0/1; Segmented uses its selection index (-1 when unset).
- previousValue refers to the immediately preceding value, not the start of a drag. A final slider/dial change may therefore have the same value and previousValue.

## 0.39.3 — clear input event names

- Use Trackpad start/changing/end with dedicated trackpad-start/changing/end IDs in both registration and shortcuts. Recreate unreleased trackpad bindings after updating.
- Rename the shared slider/dial adjusting event to Value changing and the shared value-change event to Value changed. Rename element press labels to Button press/long press/touch start/touch end. Published element-* IDs remain unchanged.
- Verify the SDK events.describe response includes all trackpad events and their coordinate payload fields. Restart Macro Deck after updating if its event catalogue still shows old definitions.

## 0.39.2 — trackpad visibility and session continuity

- Rename trackpad event labels to Element position start, Element position changing and Element position end. Rename the IDs to element-position-start, element-position-changing and element-position-end. Recreate previously configured trackpad adjusting/changed events after updating.

- Add `showCursorWhileTouching` (default false) to show the trackpad crosshair only during contact. Release and cancellation hide it even when coordinates are unchanged.
- Preserve pointer identity, bounds and gesture history across identical session replacements within two seconds, using isolated, single-use checkpoints. Changed configurations and different widgets do not inherit contact state. Client-side loss of pointer capture or a plugin restart still requires a new touch.

## 0.39.1 — trackpad gesture history and axis direction

- Add a position started event. Pointer down now emits started instead of adjusting; movement emits adjusting and release emits changed.
- Include startX/startY and previousX/previousY in all position events. Previous coordinates refer to the preceding emitted position event; start events initialize both pairs to the current position.
- Replace trackpad minX/maxX/minY/maxY with leftValue/rightValue/topValue/bottomValue. Descending ranges are supported with positive snapping increments anchored to the left/top edge. Update existing trackpad XML attribute names when upgrading.

## 0.39.0 — two-axis trackpad

- Add a Trackpad component with independent X/Y data keys, ranges and snapping, a position crosshair and alpha colors.
- Update both coordinates together and expose position adjusting/changed events. Track drags outside the surface with clamped values; cancelled contacts do not fire a final change.
- Add GUI properties, event shortcuts and a BASIC Trackpad template. External data changes update the displayed position.

## 0.38.0 — dial, toggle and segmented controls

- Add Dial, Toggle and Segmented components with data-key bindings, conditional styles, alpha colors, GUI editor entries and BASIC templates.
- Dial shares slider range, step and adjusting/changed events. Toggle stores boolean data and reports numeric 0/1 in changed events; Segmented stores and reports a zero-based selection index.
- Center Segmented choice content by default without requiring an extra XML stack. Explicit text alignment remains supported.
- Include all three controls in event shortcuts. Segmented children are display content only; hidden choices retain their index.

## 0.37.0 — action setup, editor history and diagnostics

- Publish a follow-up configuration revision after opening copied/imported or previously untracked widgets so the host can expose Save without a manual edit.

- Record the configuration's widget ID on save. Opening a copied or imported widget's configuration retargets only event widget fields that match the recorded source ID; explicit targets for other widgets remain unchanged. Save the destination configuration to apply the correction. Older data without a recorded ID is not guessed.
- Changing the sidebar Channel updates matching channel parameters in this widget's Set display value and Set display data from JSON actions, including nested loops and branches. Other channels and other providers' actions are preserved.

- Group continuous edits to the same property into one undo step, separating different fields and pauses of 800 ms or more. Keep up to 100 steps and disable unavailable Undo/Redo controls.
- Allow Undo to discard incomplete property input without losing the last valid layout. Keep validation errors visible while earlier valid changes are sent to the host.
- Report component IDs and XML line/column locations for layout validation and rendering errors; report both definitions of a duplicate ID. Select the offending XML line when applying invalid XML.

- Initialize the default channel of newly added Set display value and Set display data from JSON actions from the Custom Button widget's current channel, including actions nested in loops and branches. Preserve existing actions and explicitly configured channels on pasted actions.
- Add Target this widget in all events to replace event widget targets with the current widget ID in one click while preserving actions and other event conditions.

## 0.36.4 — repeated self-target selection

- Send normalized Custom Button event targets back to the action editor even when the saved value is unchanged. Repeatedly choosing Use this widget now restores the concrete widget ID instead of leaving the editor's optimistic `$self` value visible.

## 0.36.3 — conditional-style XML formatting

- Normalize indentation inside images and other non-text components so conditional styles and closing tags stay on separate lines after GUI edits. Preserve text whitespace, SVG CDATA and condition order.

## 0.36.2 — image and SVG tint

- Add optional `color` to images and SVGs to recolour the entire artwork while preserving its alpha mask. Support data bindings, conditional styles and colour interpolation; tint alpha multiplies component opacity.
- Use the host's native tint rendering for Icon Packs, local/remote images and SVG resources. Leaving color blank restores original colours. On beta.15, tint requires client mask support and disables artwork crossfade.

## 0.36.1 — image alignment and transparency

- Give images and SVGs a natural square footprint based on `size`, so horizontal and vertical stacks can align them without extra centered space. Ordinary contain images still support explicit expanded allocation through `fill` or `mainSize`.
- Use the host's supported transparent background for Icon Pack and artwork cover/zoom rendering, preventing the button accent color from covering transparent pixels or lower layers.

## 0.36.0 — variable-driven SVG

- Add an SVG component with XML-safe data bindings and expressions for text, colors and geometry; render self-contained shapes, paths, text, gradients and clipping paths as transparent PNG resources.
- Add native editor markup editing and a BASIC Variable SVG template. Preserve image fitting, positioning, opacity and crossfade controls.
- Cache unchanged SVG and coalesce changed content to at most five renders per second per element. Limit raster size to 64–1024 pixels and reject scripts, external references, embedded images and stylesheets.

## 0.35.1 — line gradients

- Add linear and radial gradients to all line geometry modes, including local coordinates, using native opacity masks rather than segmented paths. Support bindings, conditional styles and transitions with the existing gradient parameters.

## 0.35.0 — expanded layout limits

- Raise XML limits to 65,536 characters, 512 elements and 32 levels; validate generated node count, JSON depth and size before sending, and bound oversized live-update diffs.
- Keep the Now playing template's disconnected message outside the connected-only player so it remains visible and tappable while disconnected.

## 0.34.0 — range markers, client clocks and adaptive layouts

- Add the BASIC Image sources template with JSON fields for an installed icon, a local file, and music-player artwork.
- Refresh the clock and icon examples; use beta.15 SDK APIs for installed Icon Packs and artwork from other music players, accepting existing host paths and the artwork: prefix.
- Remove the temporary artwork test player and its image after verification.
- Extend `bar` with a data-bound start position and optional marker, using the same min/max scale as its value.
- Add `dynamic-text` for client-updated time and date displays.
- Add `modifier` for padding, clipping, frame constraints, group opacity and disabled descendants.
- Add `responsive` with ordered `variant` layouts selected by client dimensions.
- Add native editor entries, property suggestions and BASIC examples; require the beta.15 SDK and host.
- Use purple for all layout blocks and refine the Modifier and Responsive examples.
- Give icons a natural square footprint (20% default size); keep native centering only when an expanded box is explicitly requested. Refresh the Modifier and Transform examples.

## 0.33.0 — gauges, built-in icons and transforms

- Add `gauge`, `icon` and `transform` XML components and native editor palette entries.
- Bind gauge values, icon names and transformations to display data and conditional styles.
- Animate gauge values and group rotation, scaling and translation; support alpha colors on gauges and icons.
- Include BASIC examples for all three components. Requires the existing beta.14 SDK; no SDK upgrade.

## 0.32.0 — framework-dependent deployment

- Declare framework-dependent DLL entrypoints for all five platforms.
- Omit Microsoft runtime binaries and launch the editor through the plugin's selected dotnet host.
- Require .NET 10 and ASP.NET Core 10 shared frameworks; see the [distribution guide](docs/distribution.md).

## 0.31.3 — smaller multi-platform packages

- Place the plugin and editor together so identical runtime and dependency files are packaged once per platform.
- Reject mismatched shared files during packaging instead of overwriting them.
- Keep self-contained deployment: a separate .NET installation is still unnecessary.

## 0.31.2 — image refresh and recovery

- Recheck visible image sources every 30 seconds, including unchanged artwork URLs, Icon Pack references and local files. Upload only changed bytes.
- Preserve the last available image during failures and retry with a 1–30 second backoff. Log the failing stage without exposing URLs.
- Upgrade SDK, test helpers and CLI to beta.14; require Macro Deck beta.14 or later.
- Artwork still uses the host HTTP endpoint. Cross-plugin access to an `IMusicPlayer` instance has not been established.
