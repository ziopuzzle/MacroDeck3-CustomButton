# Runtime requirements and included dependencies

Custom Button 0.32.0 uses framework-dependent deployment. The package does not include
the Microsoft .NET or ASP.NET Core runtime. It requires an accessible .NET 10 runtime
and ASP.NET Core 10 shared framework of the target architecture. Macro Deck selects a
compatible dotnet host from the entrypoint runtime requirements and runtimeconfig.json;
if no suitable runtime is available, it cannot start the plugin. Installing .NET 10 SDK
also supplies these frameworks for development.

The native editor is also framework-dependent. It starts through the same dotnet
executable as the running plugin, rather than requiring a separate executable on PATH.
The editor needs the .NET shared framework plus its platform dependencies; see
[cross-platform requirements](cross-platform.md). Macro Deck's own self-contained
installation is not by itself evidence that an independently usable shared runtime exists.

Packages still include Custom Button's assemblies, the Avalonia editor, native Skia and
other platform libraries, and dependencies restored from public NuGet. The plugin and
editor share byte-identical dependencies in one directory per platform; mismatched
files cause packaging to fail. Dependencies are pinned in the lock files. Their licenses
and notices are included in each platform's ThirdParty directory, with the plugin LICENSE.

Third-party library binaries are upstream compiled distributions. This project builds
Custom Button's code, not the source of every dependency. Runtime updates are managed
through the installed shared runtime; bundled library updates require a new plugin build.
