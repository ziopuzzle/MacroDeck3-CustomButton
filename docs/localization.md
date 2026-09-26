# Product language

Version 0.17.0 uses English for plugin-authored configuration, actions, events, templates, validation messages and the native editor, regardless of host/OS language. The manifest advertises English only. There is no per-widget language setting.

User-authored XML text, data values/keys and saved action captions are not translated. Existing saved host captions can retain their previous language; newly created plugin events use English. OS/library exception details may also use the OS language.

Product strings in source are English. `TextCatalog` preserves literal text and expressions. The English resource catalog remains registered for SDK compatibility; `localization/generate.ps1` regenerates it from the English TSV entries. No Japanese resource assembly is distributed. If localization returns later, use stable catalog keys and an explicit host-language transport for the external process.

