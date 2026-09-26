# Privacy and data handling

Custom Button is an independent plugin by ziopuzzle, not an official Macro Deck product.

- The plugin receives data through configured Macro Deck actions and events. Values, histories and animation state are held in memory; the plugin does not send them to an analytics service. Macro Deck stores the widget configuration, including XML, initial JSON and action parameters.
- Widgets sharing a channel share display data. Do not use the same channel for unrelated or private information unless that sharing is intended.
- Image sources can read a user-selected local file or make HTTP/HTTPS requests to the configured URL, including local-network addresses. A data-bound source can change that destination. Only use layouts and data sources you trust. The image server receives the request URL and ordinary network metadata; HTTP does not encrypt requests. Redirects for external images are followed by the HTTP client.
- Host-relative image paths and Icon Pack IDs are resolved through the connected Macro Deck host. Images are registered with Macro Deck for delivery to clients showing the widget. This can make local images visible on those clients. Session image caches and display values are transient; Macro Deck may manage its own resource cache separately.
- The native editor is launched only when requested. Layout and initial data are passed to it over redirected process input/output. It can stay open with the draft after Macro Deck disconnects so the user can recover edits. The plugin does not automatically upload that draft.
- The Macro Deck SDK manages connection credentials and authentication. Diagnostic messages go to Macro Deck's logging system. Image-load diagnostics avoid recording source URLs, which may contain access tokens. Logging and retention managed by Macro Deck are outside this plugin's control.
- No runtime AI service, telemetry endpoint or advertising service is configured by this plugin. Development used AI assistance, including code, documentation and UI text. The manifest declares AI-generated assets/text (`generatedAssets: true`); this does not mean the running plugin generates content with AI.

Do not publish personal configuration exports, logs, access tokens, local file paths or copyrighted cover art with the source repository. The included examples contain placeholder data rather than bundled album artwork.
