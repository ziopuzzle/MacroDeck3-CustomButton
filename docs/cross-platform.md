# Platform builds

The plugin and Avalonia editor target Windows x64, macOS ARM64/x64 and Linux ARM64/x64. Packages contain framework-dependent plugin/editor assemblies and platform-specific native libraries. .NET 10 and ASP.NET Core 10 shared frameworks are required; see [runtime requirements](distribution.md). Use the package matching the OS and CPU.

Windows is the current on-device verification environment. Cross-building does not establish that installation, clipboard, dialogs, fonts and input work on macOS/Linux; these remain release checks on those systems. Linux requires the native desktop libraries used by Avalonia. A graphical desktop session is required for the external editor.

## Build

Requires .NET 10 SDK and PowerShell 7:

```powershell
./build.ps1
./build.ps1 -AllPlatforms
./build.ps1 -Runtime osx-arm64,linux-x64
```

Normal development builds use Windows only; all-platform builds are produced for releases. The script narrows the package manifest to the selected runtime and writes to `artifacts/`. Use it to include the external editor, rather than publishing only the plugin project.

```powershell
dotnet test editor.tests/Ziopuzzle.CustomButton.Editor.Tests.csproj -c Release
```

Headless tests exercise drag/drop, scrolling, insertion markers, selection, undo and property updates without controlling a running Macro Deck instance. Set `EDITOR_SNAPSHOT` to write a test screenshot. Command+Z is supported on macOS; Ctrl+Z is used elsewhere. Text fields retain their normal text undo behavior.

