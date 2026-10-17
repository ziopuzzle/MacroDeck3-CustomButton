# Development notes

Internal test observations and deferred proposals. These are not a list of current Store-release defects. Historical host versions and uncertainty are retained for investigation.

## Image color extraction follow-up (2026-10-17)

The initial XML implementation is documented in docs/images.md. Live widgets and modals now publish generated colors to Channel data; settings previews remain local. Artwork colors use a shared one-second demand-driven cache, separate from image loading. SDK beta.17 provides no artwork/color-change subscription. Installed Icon Pack analysis still needs a supported pixel or palette API; the unreleased artwork-only action was removed because XML artwork extraction and the host album-color variable cover its intended use. Icon Pack support is a separate limitation.

## Earlier intermittent image disappearance — cause unconfirmed

During beta.13-era testing, an installed Icon Pack image sometimes stopped displaying;
restarting Macro Deck restored it. An earlier WebNowPlaying image-loading observation
also recovered after restarting. No reliable reproduction procedure or shared cause
was established.

The beta.15 image-loading paths were confirmed working by the user and remain the supported API paths.
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

The user confirmed image loading after the beta.15 upgrade. Installed
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

## Deferred ideas (lowest priority)

- Text Field input constraints: reconsider only alongside useful correction/submission behavior. The experimental validation implementation was discarded; current Text Fields have no required, pattern, length or numeric-range validation settings.

## Image color follow-ups

- Extract colors from arbitrary supported image sources (local files, URLs and installed icons), beyond the host's music-player average-color API.
- Color expressions for mixing with another color, adjusting hue/saturation and alpha. These are proposals, not currently supported functions.
- The legacy player named `Artwork probe (no audio)` is no longer registered by Custom Button and is not required for images or average colors. A beta.17 host snapshot was confirmed to retain this player's catalog even though `acceptedKinds` no longer included the music-player capability. The host refreshes accepted capabilities without clearing the old catalog, and its player registry can still enumerate that catalog. The current SDK provides no removal API. With the host fully stopped and the original file backed up, setting only `net.ziopuzzle.custombutton.musicPlayer` to `null` in `config/plugin-capability-snapshots.json` clears this obsolete catalog. Do not delete the entire snapshot file or other plugins' catalogs.
