# Known issues and limitations

Reviewed: 2026-10-17 against the current implementation and SDK beta.17. This is a source review, not a new full device test.

## Copying event targets

Custom Button currently saves concrete widget IDs in its custom-event conditions
and disables the self-target option for those conditions. An explicit `$self` value
is converted to the current widget ID when the plugin normalizes the configuration.
Opening copied/imported widget settings remaps targets matching the stored source
widget ID; save to apply. Other-widget targets remain unchanged.
Since 0.41.3, ID-only edits through the GUI or XML also update this widget's matching
element-ID filters. Structural edits, simultaneous content/property changes, reused
IDs and template replacement are not treated as renames. Review the reference
warnings in Drawing/Actions and update unresolved filters manually in those cases.

This implementation was introduced because beta.13 treated `$self` as a literal
string in custom-event matching. That historical finding is not a verification of
the beta.15 host's handling of `$self`; the plugin still uses concrete IDs.

## Image availability

Installed icons must still be installed, and the selected music player and artwork must be available. Failed image loads retain the last successful image while retrying; clearing the source removes it. See [Images](images.md) for supported sources and configuration.

## Artwork color refresh

The host caches average artwork colors by player and artwork ID. Replacing bytes without changing that internal ID can leave the old color cached; repeated color requests do not force recalculation. The host may also return its last-known color when artwork is unavailable. See [Actions](actions.md#automatic-artwork-color-updates) for the host album-color variable and its refresh behavior.
