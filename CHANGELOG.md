# Changelog

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
