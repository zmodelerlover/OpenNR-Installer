# Handoff v0.7.2: Thai, auto-update on launch and the wiki name in Automatic

2026-09-30. Read after [HANDOFF-v0.7.1-2026-09-30.md](HANDOFF-v0.7.1-2026-09-30.md). Payload unchanged.

## Auto-update broke the launch (since v0.7.0)

Auto-update opens the game page before it has been drawn. The auto-update switch, in the left column
(`SheetSide`), found the `GameSheet` by walking up the visual tree, which does not exist yet at that point: `NullReferenceException`
inside the handler, `TargetInvocationException` in `Run("startup")`, the game did not update and the rest of the launch stopped.
Now the `GameSheet` introduces itself to the `SheetSide` in the constructor (`Side.Sheet = this`). `AutoUpdateFlow` in uishot opens a
new window with an outdated game and the switch on; without the fix it fails with the same error as the player's log.

## Automatic: the first name from the OptiScaler wiki wins

`Work.OptiProxyFor`: chosen by hand, then the first name from the wiki, then the name already installed, then `dxgi.dll`. Before, the
installed one came before the wiki, so an install made before the app read the wiki stayed on `dxgi.dll` forever
(Neverness to Everness, which the wiki says to use via `version.dll`). The name switch already existed: the old name goes back to what it was.
`ApiDatabase.OptiScalerNames` also searches by the name given in the app and by the folder name. The name's ⓘ changed in all 14 languages.
Tested by a friend of the user in Neverness to Everness.

## Thai

`th`, 415 keys, Leelawadee UI font. The app only breaks lines at spaces, and Thai does not separate words: the phrases have
spaces between segments.
