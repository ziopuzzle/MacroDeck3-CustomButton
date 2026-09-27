# Public repository contents

The repository contains reproducible source and user documentation, not development
session records or build output. `.gitignore` explicitly lists the public guides;
add a new guide to that list when it is ready for publication.

| Include | Purpose |
| --- | --- |
| `src/`, `editor/` | Plugin and native editor source, project files, manifest, resources and assets |
| `tests/`, `editor.tests/` | Regression tests and project files |
| All four `packages.lock.json` files | Reproducible dependency restore |
| `examples/` | XML/JSON templates embedded into the plugin at build time |
| `localization/` | Editable text catalogue and resource generator |
| `scripts/`, `build.ps1`, `build.bat` | Build, packaging and third-party notice generation |
| `.config/dotnet-tools.json`, `NuGet.Config`, `Ziopuzzle.CustomButton.slnx` | Tool versions, package source and solution |
| `.github/workflows/release.yml` | Release packaging workflow |
| `licenses/`, `LICENSE` | Shared dependency license texts and the project's own license |
| `README.md`, `CHANGELOG.md`, the explicitly allowed `docs/` guides | Installation, release history, usage, limits, privacy and distribution disclosures |
| `screenshot_buttons.png` | Public product screenshot; review its contents before publishing |

Keep `work/`, `artifacts/`, `bin/`, `obj/`, logs, crash dumps, local credentials,
the copied Store guideline, investigation notes and submission drafts local.
The generated dependency inventory is packaged as `ThirdParty/INDEX.md`; the local
`docs/dependencies.md` snapshot is not needed in source control. Do not exclude
`licenses/`: `Collect-Notices.ps1` copies these texts into every distribution.

`UserSecretsId` in the plugin project is a public lookup identifier for .NET's local
development secret store. It is not an API key and does not include the stored values.
Keep the identifier in source; never commit `secrets.json` or exported credentials.

Ignore rules do not remove already staged/tracked files. Before the first commit,
inspect both `git status --short` and `git diff --cached --name-status` and re-stage
updated public files. Remove any ignored, previously staged notes from the index
with `git rm --cached -- <path>`; this keeps the local file.
