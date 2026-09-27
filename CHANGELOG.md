# Changelog

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
