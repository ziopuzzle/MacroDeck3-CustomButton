# Known issues and historical observations

Last reviewed: 2026-10-03, Custom Button 0.34.0 with Macro Deck 3.0.0-beta.15.

## Copying event targets

Custom Button currently saves concrete widget IDs in its custom-event conditions
and disables the self-target option for those conditions. An explicit `$self` value
is converted to the current widget ID when the plugin normalizes the configuration.
After copying a button, check its custom-event targets and retarget or recreate them
if they still reference the original button. Renaming an interactive element also
requires updating matching element-ID filters separately.

This implementation was introduced because beta.13 treated `$self` as a literal
string in custom-event matching. That historical finding is not a verification of
the beta.15 host's handling of `$self`; the plugin still uses concrete IDs.

## Earlier intermittent image disappearance — cause unconfirmed

During beta.13-era testing, an installed Icon Pack image sometimes stopped displaying;
restarting Macro Deck restored it. An earlier WebNowPlaying image-loading observation
also recovered after restarting. No reliable reproduction procedure or shared cause
was established.

The current beta.15 image-loading paths have been confirmed working by the user.
That confirmation does not establish the cause or resolution of every older
intermittent failure. If the issue recurs, record the image source, host/plugin
versions, preceding edits or deck switches, and image/session logs before restarting.

## Rapid color-picker edits — reported improvement confirmed

Rapid color-picker input previously ended configuration/preview sessions on beta.13.
The supplied `20260926.log` contained session replacements but did not establish
their cause. After the 0.27.0 changes, the user confirmed that rapid picker input
no longer crashed the client.

The editor currently limits draft messages to four per second and keeps the window
and edited content available after a disconnect, allowing XML to be copied before
closing. A host rate limit or fail-safe was suggested as a possible cause, but was
not proven. This is a historical observation with a confirmed improvement, not a
confirmed ongoing crash in the current build.

## Host artwork and installed Icon Packs — access issue resolved in beta.15

The user confirmed image loading with the current beta.15-based build. Installed
icons use `UiResources.GetIconAsync`; other players' artwork uses
`UiResources.RegisterMusicPlayerArtworkAsync`. These supported SDK APIs replace
the unauthenticated HTTP requests responsible for the beta.14 authorization failures.

Supported sources include `icon-pack:UUID`, `/api/icons/UUID/image`, and
`/api/music-player/artwork/ID?instanceId=PROVIDER%3A%3APLAYER`, optionally prefixed
with `artwork:` for the artwork path. A temporary player is not required and is no
longer registered. See [Images](images.md) for configuration examples.

The referenced icon must still be installed, and the selected player and artwork
must be available. The plugin retains the last successful image while retrying
failed loads; clearing the source removes the image.

### Historical cause on beta.14

The 2026-09-27 log showed repeated HTTP 401 responses before resource registration.
The image endpoints required client/admin authentication, while the plugin session
had plugin scope. Beta.13 could authorize these local requests through loopback
trust without credentials; beta.14 additionally required a desktop secret header
or session cookie. The plugin's plain HTTP requests supplied neither.

The beta.14 SDK could resolve the calling plugin's bundled icons but did not expose
the cross-integration artwork and installed-icon APIs now used by this plugin.
Those old restrictions no longer describe the current beta.15 implementation.
No desktop credentials are copied or added by the plugin.
