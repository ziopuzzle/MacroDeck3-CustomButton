# Changelog

## 0.47.0 — image colors

- Add opt-in Image/SVG averageColorKey and averageColorFallback properties, including GUI fields. Analyze local/HTTP images and rendered SVG; use the host's album-color API for artwork. Live widgets and modals publish results to Channel data for variable-write and copy actions; preview results remain local.
- Refresh artwork colors independently approximately once per second, sharing player queries across visible sessions without reloading images. SDK beta.17 has no artwork-change subscription.
- Support transparent and alpha-channel fallback colors. Reuse existing image refreshes and content hashes for color analysis.
- Document automatic shared-data updates through the host's album-color variables and its artwork-ID cache limitation. Icon Pack color analysis is not supported by the current SDK.
- Remove the unreleased Get artwork color action in favor of XML extraction or the host album-color variable.

## 0.46.0 — display data transfer

- Clarify local/global variable destination selection and how to verify copied live data independently of the drawing.

- Add Copy display value and Copy display data actions with type-preserving snapshots, optional key renaming, and merge/replace modes.
- Add Write display value to variable using the host's existing-variable write API, with owner/global scope selection and host error reporting.
- Automatically initialize source channels inside Custom Button and follow matching transfer references when channels change or widgets are copied/imported.
- Include saved source-widget initial JSON in all three transfer actions, with current Channel values taking priority. Add an optional source widget selector for reading another Custom Button's defaults.
- Document source selection, one-time copying, limits and variable conversions.

## 0.45.5 — searchable configuration choices

- Enable host-native search for templates, input targets and the event reorder picker. Keep short event-type and mode menus unchanged.
- Include BASIC/ADVANCED in searchable template labels so categories remain visible and can be searched.

## 0.45.4 — Macro Deck beta.17

- Update SDK, UI, testing helpers, analyzers and local/GitHub Actions CLI to 3.0.0-beta.17; regenerate all NuGet lock files.
- Require Macro Deck 3.0.0-beta.17 or later. Keep existing widget, image and modal behavior.
- Document relevant upstream changes and future candidates, including notification dialogs, searchable choices, music-player access and Windows conformance fixes.

## 0.45.3 — modal result values

- Add result to closing stacks/layers and the GUI Interaction properties. Support literal strings, data bindings and conditional styles, up to 4096 characters.
- Include result and elementId in Modal closed. Host cancellation yields empty fields; a result named cancel is a normal explicit completion. Existing wait-mode requirements remain unchanged.
- Resolve client answer IDs against bounded per-modal render snapshots, preserving the displayed result across data updates and rejecting unknown or expired answers.
- Update the default Close example and documentation. Correct component coverage, current event labels, implemented animation/input/text features and the historical status of older publication notes.

## 0.45.2 — modal completion events

- Include a Close control below the text in the default Show modal XML. Existing action layouts are unchanged.

- Clarify that Modal closed requires the waiting option in Show modal, including both option labels and the event description. Order lifecycle shortcuts as Display activated, Modal opened, Modal closed, Display update, then Variable changed.

- Add an optional After opening setting to Show modal: wait for completion and publish Modal closed with widgetId, channel and cancelled. The default continues immediately, as before.
- Add Modal closed to event definitions and the Other shortcut category. Use the Show modal event target, or the action owner when empty or $self.
- Suppress completion events when no modal session opened. Cancellation includes dismissal, disconnection and host time limits; waiting holds subsequent actions and a host run slot.

## 0.45.1 — modal close controls

- Add closeModal to stacks and layers, including GUI suggestions and a false default. In a modal, pressing the element closes that modal without requiring an event target widget or publishing press events. Ordinary widgets ignore the close behavior.
- Support data bindings and conditional styles for closeModal, including transparent and bordered controls.

## 0.45.0 — text input

- Add text-field, a native single-line text input for widgets and modals, with data-key/value binding, placeholder, size and explicit interactive mode.
- Add Text changing and Text changed events with string value, previousValue and key payloads. Inputs update Channel data; previousValue reads the latest stored data, including external updates. Existing numeric event IDs and payloads are unchanged.
- Add Text field to the GUI Controls category, property defaults, event shortcuts and a Basic template. Empty strings, Unicode and whitespace are preserved.

## 0.44.0 — modal GUI editing

- Add a native GUI editor launcher to Show modal through the SDK action-configuration UI, independent of the owning widget type.
- Keep XML, JSON, destination device, channel, title and event target settings available. Stage external edits and accept them with Apply edited XML so the host stores a client-originated parameter change.
- Present Apply and Discard as matching buttons, with warnings about pending edits and a reminder to save after applying. Hide empty message components, including their warning indicators. Provide a recovery copy for pending XML. Save the action and reopen the modal to see layout changes; no modal preview is included.

## 0.43.1 — modal initialization

- Simplify the default modal XML to a vertical scroll containing static text. New Show modal actions inside Custom Button use the widget ID as their Channel; other widget types keep demo.

- Add Modal opened with target widget and Channel payloads, including an event shortcut under Other. Initialize data even if missing values prevent the first render; publish once per modal session.
- Default new Show modal actions to empty initial JSON and explain fallback precedence. Shared Channel values are preserved. Existing actions retain their configured initial JSON.
- Document initialization from host variables, live recovery and the option to prepare data before opening a modal.

## 0.43.0 — scroll containers

- Add the scroll XML container for widgets and modals, using the host's native UiList. Support vertical (default) and horizontal directions, nested content, live bindings and child input events.
- Add Scroll to the GUI Layout category in purple, with child editing and a Basic Scroll template.
- Define scrolling entirely in XML; Show modal has no scrolling checkbox. See docs/modal.md for sizing and interaction guidance.

## 0.42.0 — modal views

- Add Show modal, an action that opens literal XML with initial JSON and live Channel data in a native dialog on the originating or explicitly selected client.
- Reuse the existing renderer and image resources, with a transparent stack root instead of tile rounding. Include IDs in the sample XML.
- Route interactive dialog elements through existing element events, using Event target widget (the opening button by default).
- Add Destination device, resolving the selected device to its current client via the public Deck API. This permits opening dialogs from Custom Button events without an originating client. Offline explicit targets fail without falling back to another device. See docs/modal.md for setup and limitations.

## 0.41.8 — consistent text-shadow context

- Use a stable native UiButton at the widget root so Text shadow=true works through nested transparent stacks, without requiring a border. Keep the existing whole-widget interaction switch and child events.
- Default Text shadow to false, including blank or unavailable bindings. Preserve explicit root transparency instead of falling back to the host accent. The native root applies tile clipping and corners; internal layout containers remain unchanged.

## 0.41.7 — text outlines and gauge track colours

- Add Text shadow, strokeColor and strokeWidth, and Gauge trackColor to XML and the GUI editor, including display-data bindings and conditional styles.
- Split Text and bar into separate Text and Bar templates. Demonstrate shadow and outline settings in Text and an explicit track colour in Arc gauge.
- Support RGB transitions for strokeColor/trackColor and length transitions for strokeWidth. Text fill/outline and gauge level/track now use independent native colour alpha; use opacity to fade the whole element.

## 0.41.6 — Macro Deck beta.16 toolchain

- Update the plugin, editor and test SDK dependencies, local CLI and release workflow to Macro Deck 3.0.0-beta.16. Require host beta.16 or later.
- Preserve existing XML, drawing behavior and event handling; new beta.16 drawing options are not enabled by this dependency update.

## 0.41.5 — clear recovered preview errors

- Successful preview rendering clears retained preview failures for the same widget, including errors from replaced editing sessions and previous channels. Errors from actual widget displays and other widgets remain independent. Refresh Diagnostics to read the updated report.

## 0.41.4 — inspect data and handle unavailable numbers

- List effective display-data keys, JSON types, values and initial/live sources in Diagnostics. Refresh manually with the diagnostic report; long values are truncated for display.
- Add number(key, fallback), isNumber(key) and isNumeric(key) to calculations and conditions, and type(key) to conditions. Numeric strings can be converted; N/A, missing/null and other nonnumeric values use the explicit fallback.
- Add fallback to Slider and Dial for key-based and direct value bindings, defaulting to the minimum. Rendering a fallback never overwrites source data. Literal configuration and calculation errors remain errors.

## 0.41.3 — follow element ID edits

- Follow unambiguous ID-only changes from both the GUI editor and direct XML edits in this widget's Custom Button event filters. Preserve other-widget filters and action data. Keep the last valid XML while typing incomplete XML.
- Do not infer renames from structural changes, reused IDs or template replacement. Show unresolved element-event references beneath the XML as well as in Actions. Save applies event changes together with the layout.

## 0.41.2 — automatic widget channels

- Assign new widgets their own widget ID as the channel. On opening copied/imported widget settings, replace a channel matching the stored source widget ID with the new widget ID, including matching Set display value/data actions in nested event flows. Save applies the correction using the existing configuration transaction.
- Preserve manually named channels, including existing demo channels, and actions targeting other channels or integrations. No additional ownership metadata is stored.

## 0.41.1 — organized templates and component menu

- Order templates from basic drawing examples to more involved drawing, input and complete widgets. Order Shapes and Controls from simple to complex components. Separate text/time, images, data displays and controls; rename the SVG template to SVG and refresh the edited examples.

## 0.41.0 — copyable drawing diagnostics

- Show a compact draft status, a deduplicated list of error messages, and an always-visible native copy field. Keep timestamps and source details in the copyable report. Show identical draft, preview and widget errors only once per list/report, retaining each runtime source, channel, latest timestamp and session count in the report.
- Add a Diagnostics tab to widget settings with a manual refresh and a native copyable report. Opening settings checks the current XML/initial data merged with live channel data.
- Capture runtime render errors by widget/session with timestamps and channel information. Successful rendering clears that session's error; closed-session failures remain labeled as last observed. Reports are bounded and kept only in memory.
- Clipboard access is initiated only by the user in the settings UI. Widget touch gestures and actions are unchanged. Draft checks do not download images or validate client rendering; action execution logs remain in Macro Deck.

## 0.40.1 — property default placeholders

- Show safe literal defaults as property placeholders without writing them into XML. Leave inherited conditional-style values, theme colors, automatic sizing and attribute-presence switches blank.

## 0.40.0 — grouped GUI properties

- Separate property presentation order from XML validation. Place ID first and group data, layout, appearance, interaction, visibility, animation and advanced settings.
- Group Trackpad settings by axis; put opacity with appearance, image transitions with animation and SVG raster resolution under Advanced.
- Order line geometry as coordinate basis, mode, then the selected mode's coordinates. Keep condition fields in the same categories, initially opening every property category.
- Place Text and SVG content inside Content and data. Open all property sections initially.
- Remember normal property-section expansion per component type while the editor remains open. Preserve XML attributes and automatic editing/undo behavior.
- Insert bare data keys for Trackpad keyX/keyY fields, matching other data-key fields.

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
