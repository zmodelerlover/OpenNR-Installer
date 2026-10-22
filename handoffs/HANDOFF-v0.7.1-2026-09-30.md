# Handoff v0.7.1: the pirate language crashed the app

2026-09-30. Read after [HANDOFF-v0.7.0-2026-09-30.md](HANDOFF-v0.7.0-2026-09-30.md).

With the pirate language, "Last session" formatted numbers with `CultureInfo.GetCultureInfo("x-pirate", predefinedOnly: true)`.
.NET accepts the private tag without throwing, but the culture has no data, and `NumberFormat` gives `NullReferenceException`. In list
mode a game's page is always open, so the app crashed on every launch, before the updater ran.

- `GameSheet.Culture()`: `x-` tags use `en-US`.
- `ShowSession` (async void) goes through `ShowSessionAsync` inside a try: an error there hides the block and goes to the log.
- `SessionFlow` also renders the session in `x-pirate`; without the fix the flow reproduces the crash.

Anyone stuck on v0.7.0: download the v0.7.1 exe by hand, or delete `"Language"` from `%APPDATA%\AmdNrInstaller\settings.json`.
Payload unchanged.
