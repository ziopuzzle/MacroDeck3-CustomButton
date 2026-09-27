# MacroDeck3 Custom Button

Build custom Macro Deck widgets with XML, templates and a native block editor. Display live data, draw shapes and graphs, and put several interactive buttons or sliders in one widget.

An independent plugin by ziopuzzle, not an official Macro Deck product. See [privacy and AI disclosure](docs/privacy.md) and [known limitations](docs/known-issues.md).

## Requirements and installation

- Macro Deck **3.0.0-beta.14 or later**. The SDK and CLI are pinned to beta.14.
- Development requires **.NET 10 SDK** and **PowerShell 7**.
- Packaged builds include the .NET and ASP.NET Core runtimes, the native editor, and dependencies restored from public NuGet packages. These are published binaries; users do not need a separate .NET installation. See [distribution details](docs/distribution.md).

In Macro Deck, use **Integrations > Install from file** and select `artifacts/net.ziopuzzle.custombutton-0.31.3-win-x64.macroDeckPlugin`.

Plugin ID: `net.ziopuzzle.custombutton`. Widget type: `net.ziopuzzle.custombutton::custom-button`.

Windows x64 is the current verification target. macOS and Linux builds are supported, but their on-device behavior still needs testing. See [platforms](docs/cross-platform.md).

## Get started

1. Add a Custom Button. Set a **Channel** in the sidebar. Widgets with the same channel share received data.
2. Open **Drawing**. Use **Open editor** for block editing, or the separate full-width **Choose a template** dropdown. Selecting a template replaces the drawing XML and initial JSON; it keeps the channel and action flows.
3. Open **Actions**. Select the whole button, an interactive component, or **Other (updates and variables)**, then choose an event to create it.
4. Add actions inside that event and save in Macro Deck. Test gestures on the actual client; the configuration preview does not execute press events.

For live values, add Display update (1 second) or the standard Variable changed event, then a **Set display value** action. Match its channel and data key to the drawing. Macro Deck evaluates `{{ vars.name }}` in the action; use `{{name}}` in XML to read the resulting display data.

```xml
<stack id="panel" padding="8%" gap="4%">
  <text id="title" size="12%">{{title}}</text>
  <text id="reading" size="25%" align="center">{{value:0}}%</text>
  <bar id="level" value="{{value}}" min="0" max="100" color="#54dfcc" />
</stack>
```

Initial data: `{"title":"CPU","value":42}`. Initial data supplies values until updates arrive. The sidebar keeps these keys visible while editing actions.

## Drawing and interaction

- Layout: `stack`, `layer`, text, conditional visibility/styles, expressions and numeric formatting.
- Data: range bars, History Graph, clocks and time-based progress bars.
- Images: local files and HTTP/HTTPS URLs with data bindings, opacity and crossfade. See [images](docs/images.md).
- Geometry: rectangles, circles, capsules, lines, paths, sectors and polygons.
- Input: independent press targets and sliders, with target IDs filled in by the event shortcuts.
- Color: `#RGB`, `#RGBA`, `#RRGGBB`, `#RRGGBBAA` (alpha last), plus component opacity.

Lengths are fractions of the widget sizing basis: `0.2` and `20%` are equivalent. IDs start with a letter and contain at most 32 letters, digits, underscores or hyphens. IDs must be unique. Data text is not reinterpreted as XML. Escape literal XML characters such as `&` and `<`.

Numeric history is sampled approximately once per second while displayed, including repeated values. Display data is transient and shared by channel. Updating the source variable alone does not populate plugin data: connect a Set display value/JSON action. History holds up to 120 samples per key and is cleared when the plugin restarts.

Limits include 16,000 XML characters, 64 elements (including styles), eight levels, 64 data keys per channel and 256 channels. Large rendered trees are rejected. HTML/CSS/JavaScript execution and arbitrary image uploads are not supported.

## Guides

- [Actions and updates](docs/actions.md)
- [Event-driven display updates](docs/realtime.md)
- [Calculations and duration formats](docs/expressions.md)
- [Value-change animation](docs/animation.md)
- [Interactive components and shortcuts](docs/controls.md)
- [Native block editor](docs/block-editor.md)
- [Additional components](docs/components.md)
- [Shapes, expressions and alpha](docs/shapes.md)
- [Images](docs/images.md)
- [English-only text policy](docs/localization.md)
- [Templates](docs/templates.md)

## Build and verify

```powershell
./build.ps1                       # Windows x64 verification build
./build.ps1 -AllPlatforms         # Individual packages for all five runtime targets
dotnet test tests/Ziopuzzle.CustomButton.Tests.csproj -c Release
dotnet test editor.tests/Ziopuzzle.CustomButton.Editor.Tests.csproj -c Release
```

Use the packaging script for distribution: publishing only `src/` does not include the editor. Artifacts are written to `artifacts/`. See [public repository contents](docs/repository-content.md) for source and documentation policy.

## 0.31.3 — smaller multi-platform packages

- Place the plugin and editor together so identical runtime and dependency files are packaged once per platform.
- Reject mismatched shared files during packaging instead of overwriting them.
- Keep self-contained deployment: a separate .NET installation is still unnecessary.

## 0.31.2 — image refresh and recovery

- Recheck visible image sources every 30 seconds, including unchanged artwork URLs, Icon Pack references and local files. Upload only changed bytes.
- Preserve the last available image during failures and retry with a 1–30 second backoff. Log the failing stage without exposing URLs.
- Upgrade SDK, test helpers and CLI to beta.14; require Macro Deck beta.14 or later.
- Artwork still uses the host HTTP endpoint. Cross-plugin access to an `IMusicPlayer` instance has not been established.
