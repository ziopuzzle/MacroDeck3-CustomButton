# Included runtimes and dependencies

Custom Button is distributed as a self-contained application. Its package includes:

- The Microsoft .NET runtime and ASP.NET Core runtime needed by the plugin.
- The native block editor and its Avalonia, Skia and related platform dependencies.
- The Macro Deck SDK, logging libraries and other dependencies restored from public NuGet packages.

The plugin and editor executables share one directory per platform. Their individual
dependency/runtime configuration files are retained, while byte-identical runtime and
library files are included only once. Packaging rejects conflicting files rather than
overwriting them. This avoids shipping two copies of the .NET runtime per platform
and keeps the combined five-platform archive below the Store's expanded-size limit.

Users do not need to install .NET separately to run the packaged plugin. Other platform requirements still apply; see the platform guide in the source repository. The included runtimes and third-party libraries are precompiled upstream distribution files copied by the build/publish process. The plugin build compiles Custom Button's own source; it does not rebuild .NET or all dependency libraries from source.

Dependency versions are recorded in the committed lock files, and package notices are included under ThirdParty with the plugin's LICENSE. Runtime packs used in each artifact are also listed in ThirdParty/INDEX.md. The included .NET runtime does not automatically become the machine's newest installed runtime: runtime/security updates require a rebuilt plugin package. The publisher should review upstream security updates and rebuild affected releases.

The publisher has chosen to submit this disclosed packaging for Store review. This statement does not claim prior Store approval. Packaging will be revised if review requires a different deployment approach.
