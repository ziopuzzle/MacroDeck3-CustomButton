# Known observations

## Picker edits can end the configuration session

After 0.27.0, the user confirmed rapid picker input no longer crashes the client. This verifies the observed improvement, not the proposed host fail-safe/rate-limit cause.

Reported on beta.13 with rapid color-picker input. The supplied `20260926.log` has preview replacements but no cause-bearing exception. Version 0.27.0 limits draft messages to four per second and preserves the external editor on disconnect, allowing XML recovery. The host reset itself is not proven fixed.

## Copying event targets

beta.13 custom event matching treats `$self` as a literal string. Targets remain concrete widget IDs; retarget or recreate events after copying.

## Icon Pack artwork intermittently disappears

Reported during testing of image support, most recently after validating 0.24.0 Cover and
transparent images. An installed Icon Pack image sometimes stopped displaying; restarting
Macro Deck restored it. The user has confirmed that both Cover and images with transparent
pixels display correctly otherwise.

There is no established reproduction procedure or identified cause. This is recorded for
later investigation, not marked fixed. The user requested deferring investigation until
reproduction steps can be established. On recurrence, record the image source, host/plugin
versions, preceding edits or deck switches, and image/session log entries before restarting.

The earlier WebNowPlaying image-loading observation also recovered after restarting Macro
Deck. It is not yet established that these observations share a cause.


On beta.14 with 0.31.2, WebNowPlaying URL and Icon Pack images were reported as not loading.
The supplied 2026-09-27 log shows repeated `Image download failed (HTTP Unauthorized)`
responses, including after plugin initialization at 12:24:33. The affected requests are
rejected with HTTP 401 before resource registration; retries cannot resolve missing
authentication. Verified against the v3.0.0-beta.14 source: both endpoints require
`ClientAccess` (admin or client scope), whereas plugin sessions carry plugin scope.
The SDK's `GetPluginIconAsync` resolves only the calling plugin's bundled packs, not
arbitrary installed icons. No supported cross-plugin artwork retrieval path was found.
Do not rely on host artwork URLs or installed Icon Pack references for this release;
use local image files or accessible HTTP/HTTPS image URLs instead. This finding does
not establish the cause of every older image issue.
