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

## Scrolling to a repeated item

Checked against the host source tag `v3.0.0-beta.17` and the plugin's beta.17 SDK reference. Scroll cannot currently be directed to an XML element ID or Repeat item key. The host's `UiList` exposes direction and start/end anchoring, but no target item, offset or alignment request. Its `reveal` event reports which child became visible; it does not move the viewport. UI patch operations and the public UI interaction API offer no scroll command either.

Since 0.48.1, Custom Button exposes optional vertical `anchor="start|end"`. End-following alone cannot target an arbitrary playing track. The separate [scroll anchor experiment](../experiments/scroll-anchor/README.md) stages row creation, follows the end, disables following, then restores remaining rows. This workaround needs client verification and is sensitive to timing and layout; it is not a guaranteed arbitrary-scroll API. Replacing the list can discard the user's position.

The manual three-stage workaround was confirmed on the user's client. Experimental `followKey`, `followOffset` and `followDelayMs` now automate it for one direct Repeat child. Offset counts array items, not pixels; variable row heights cannot guarantee centering. Intermediate rows are removed and recreated, and transport delivery does not acknowledge client paint. Existing start/end anchoring remains available when followKey is omitted.

A reliable direct-target API needs host/reader support. A suitable future API would identify a descendant node, choose start/center/end/nearest alignment, and carry a request revision so unrelated data updates do not pull the viewport back. Repeat's stable item keys can then identify the desired rendered instance. No direct target-item XML attribute is implemented.

Evidence: host `UiElements.cs` (`UiList`), `ui-list.component.ts`, `UiPatchOperations.cs`, `IUiInteractions.cs`, and the [official List documentation](https://docs.macro-deck.app/ui/components/list/).

## Artwork color refresh

The host caches average artwork colors by player and artwork ID. Replacing bytes without changing that internal ID can leave the old color cached; repeated color requests do not force recalculation. The host may also return its last-known color when artwork is unavailable. See [Actions](actions.md#automatic-artwork-color-updates) for the host album-color variable and its refresh behavior.
