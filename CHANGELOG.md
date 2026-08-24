# Changelog

## 0.1.8 - historical Friend availability contract

- Added an optional v2 read-only API for deterministic native-Friend base availability at an exact UTC instant.
- Added a bounded native-Friends snapshot for optional consumers; historical answers never use current party or scene-presence overrides.
- Preserved the v1 current-availability methods and kept the feature independent of Deep Sims, network access, and AI services.

## 0.1.6 - first public release

**Added**
- Standalone retained-uGUI launcher with programmatic grip marks and the shared Forgotten Roads hover/pressed colors.
- Collapsible panel header: collapsed Party Tools keeps only its draggable header plus Reset and Close.

**Changed**
- Standardized the compact title and close-button dimensions without changing any party action or panel content.
- Explicit Ready Check, Roll, Party Roll, and Friends Online actions remain the panel's full surface, with Suite Hub launcher fallback unchanged.

**Fixed**
- A Harmony install failure is now fail-open. If the chat-command and camera-containment hooks cannot be applied, Party Tools unpatches itself and keeps the retained UI and Suite bridge working instead of aborting startup.
- A persistent per-frame update failure now reports one bounded summary at most every 30 seconds instead of one log line per frame.
- The project file now references `UnityEngine.InputLegacyModule`, so `ErenshorPartyTools.csproj` builds from a clean checkout.

**Compatibility**
- Built against the currently installed Erenshor `Assembly-CSharp.dll` and Lunaris 0.1.0.
- Fully standalone. Suite Hub and every other Forgotten Roads mod are optional; none are required to load or to use any feature.
- Supports Lunaris runtime enable/disable/reload.

**Known limitations**
- Lunaris lists the plugin under its stable identifier `forgetwhtuno.erenshor.partytools` rather than a friendly display name. That identifier also names the config file, so it is deliberately left unchanged.

## 0.1.5 - RC camera and gesture ownership

- Claims only left-button gestures at pointer-down, reasserts while held, and releases on physical button loss, focus/pause loss, disable, destroy, readiness loss, close, zone, and unload.
- Coordinates through the suite process owner registry and restores the captured native baseline instead of blindly clearing `GameData.DraggingUIElement`.
- Adds a fail-closed monotonic postfix for the verified current `CameraController.UsingUI()` boundary and release source guards.
- Startup output now includes the exact version.

## 0.1.4 - Escape authority release correctness

- Split Hub **presence** from Hub launcher usability/native quick-close capability. A well-formed Hub endpoint now always suppresses Party Tools' local Escape poll, even when `quickClose=0` or this module's quick-close provider is unavailable.
- Standalone Escape fallback is retained only when Hub is genuinely unavailable/malformed; healthy-but-unverified Suite operation uses explicit X/close controls and does not compete for Escape.
- Added deterministic tests for all three authority states and documented that Erenshor's tall Attack/Assist/Pull/Auto Pull/Guard stack is native UI outside Party Tools ownership.

## Pre-release development

- Restored `/ptwho` and the panel's **Friends Online** action to Erenshor's native current-character Friends roster. It again shows `AVAILABLE`, `BUSY - GROUPED`, and `OFFLINE` for friends who may be available to group.

## 0.1.3 - deep playable-state pass

- Replaced `System.Random` rolls with rejection-sampled `RandomNumberGenerator` output for unbiased inclusive ranges through 1,000,000.
- Removed synthetic Sim/personality roll chatter. A party roll can now emit at most one concise local summary line, while the retained panel remains the complete result surface.
- Removed the second-overload chat fallback: one Party Tools action now makes exactly one native chat append attempt, avoiding a late-exception path that could duplicate a result line.
- Ready checks now distinguish an explicitly identified remote COOP human as `REMOTE` without inventing readiness; unknown combat/alive state fails closed to `UNAVAILABLE`.
- `/ptwho` now includes persistent Sim level/class only for verified local Sims; remote humans are never described using the backing Sim tracking record.
- The normal panel opens directly on a refreshing current-party summary and returns there when a ready check expires.
- Added small-screen result-list geometry so optional footer content yields before result rows overlap the header.
- Added duplicate-plugin initialization protection and privacy-safer exception-type logging for hot reload/recovery.
- Optional COOP AssemblyLoad detection now reinitializes idempotently on plugin load and fully detaches on unload, avoiding stale type caches across Lunaris reload cycles.
- The cryptographic RNG provider now follows plugin initialize/shutdown lifecycle so repeated Lunaris reloads do not retain native RNG resources.
- Added/expanded pure tests for roll bounds, parsing edge cases, readiness/remote policy, party snapshot formatting, panel geometry, and Suite launcher fallback.
- Suite basic labels are exactly `Show Party Tools Launcher` and `Party roll chat summary`.

## Pre-release development - full playable-state reliability pass

- `/ptwho` now reports only the current player/current party, with explicit local-Sim, observable remote-COOP, and unavailable states; legacy friend-availability config remains readable but is no longer product behavior.
- Refresh an open `/ptwho` view on the same bounded cadence used by ready checks so party membership changes do not leave stale rows.
- Added a bounded 10-second ready-check session timeout; repeated `/ready` restarts the current check and current-party rows continue to refresh while it is active.
- Added explicit retained-panel `Launcher [ON/OFF]` and `Roll Chatter [ON/OFF]` buttons while keeping the panel compact.
- Added shared `ui.state`/`closePanel` support and defer local Escape only when the Hub advertises verified quick-close and this module provider registered successfully.
- Added pure release-policy coverage for command bounds, ready timeout, current-party/COOP classification, Hub presence, quick-close fallback, and `ui.state`.
- No AI, raid tooling, network messages, gameplay rolls, or inferred readiness were added.

## Pre-release development - retained uGUI / Suite control migration

- Aligned the retained launcher/panel with Follow's proven Sim Actions dark/translucent/cyan visual language while preserving the existing retained hierarchy, commands, drag ownership, launcher fallback, and deterministic party behavior.
- Updated `TESTING.md` for the retained launcher/Hub/X lifecycle; removed stale F7/OpenMenuKey and transient-timeout acceptance steps from the current test plan. Historical changelog entries remain historical.
- Replaced the transient IMGUI/F7 menu with one persistent retained-uGUI panel and small launcher. Removed Party Tools UI click/camera Harmony workarounds and normal-access global hotkey polling; `/tools`, `/ready`, `/roll`, `/rollparty`, and `/ptwho` command semantics remain unchanged.
- Added EventSystem drag guards, normalized bottom-left position persistence, resolution reclamping, visible X/reset controls, and scrollable in-place result rows. Ready rows refresh values without rebuilding the whole panel.
- Added `showLauncher` Aura setting with mandatory fallback visibility whenever Hub is absent/unusable or this module's Aura bridge is not registered. That migration initially exposed roll-chatter and friend-fallback settings plus `openPanel`; the current playable-state pass above supersedes friend-fallback product behavior because `/ptwho` is now current-party-only.
- Added deterministic tests for launcher policy, normalized geometry, roll bounds/bad/overflow input, repeated parsing, ready-state presentation, and zone/not-ready panel cleanup. Ready checks remain deterministic and remote COOP humans are never inferred ready.
- Current-assembly compile/live Lunaris validation remains required before release.

## Pre-release development (native Lunaris migration)

- Converted the plugin host from BepInEx (`BaseUnityPlugin`/`[BepInPlugin]`/`[BepInProcess]`) to
  native Lunaris (`LunarisPlugin`/`[LunarisPlugin]`/`[LunarisPermission(Reflection | Harmony)]`).
  No Network or FileAccess permission requested — this mod uses neither.
  All commands (`/tools`, `/ready`, `/roll`, `/rollparty`, `/ptwho`) and their exact syntax are
  unchanged; the existing Harmony `TypeText.CheckCommands` prefix hook is retained rather than
  converting to `[LunarisCommand]`, so vanilla commands continue to pass through untouched.
- Config replaced `ConfigEntry<T>`/`Config.Bind` with native typed Lunaris config
  (`PartyToolsSettings`); all 8 existing settings (section/key/default/description) preserved
  unchanged, plus a loader-neutral `PartyToolsConfigEntry<T>` shim so call sites kept their
  existing `.Value` access pattern.
- Logging replaced `BepInEx.Logging`/`ManualLogSource` with native Lunaris `Logging`.
- Fixed a hot-unload leak in `CoopCompatibility`: the `AppDomain.CurrentDomain.AssemblyLoad`
  subscription used an anonymous delegate with no way to ever unsubscribe. Replaced with a named
  handler and a new `Shutdown()` that unsubscribes and clears the cached reflected COOP types;
  called from `OnDestroy()`.
- `BUILD_AND_INSTALL.ps1`/`UNINSTALL.ps1` now target `<Erenshor>\plugins` instead of a BepInEx
  profile and no longer require `BepInEx.dll`.

## Pre-release development

- Aligned the panel interaction contract with PvP: upper-right/below-minimap placement, persisted clamped dragging with hot-control ownership, and suppression of world clicks and camera rotation while the pointer is over or dragging Party Tools.
- `/ptwho` now reads the current character's real Erenshor friend roster using
  the same `FriendedBy == CurrentCharacterSlot.index` predicate as Group
  Builder's native Friends filter. Availability uses native `online` and
  `Grouped` tracking; manual names remain compatibility fallback only.
- Added the required `UnityEngine.InputLegacyModule` build reference for the
  configurable menu key and Escape-to-close handling.
- Added configurable `/rollparty` social chatter: player announcement, one
  acknowledgment per eligible local Sim, chat-visible results, and an untied
  winner reaction. Sim wording uses verified personality/rival signals and
  Erenshor's native `PersonalizeString` typing quirks, following Deep Sims'
  established reader pattern without creating a hard Deep Sims dependency.
- Added a temporary Party Tools command menu. Press configurable `F7`, type
  `/tools`, then choose Ready Check, Roll 1-100, Party Roll
  1-100, or Friends Online. Menu actions share the existing command handlers.

- Added `/ptwho`, an advisory-only friend availability panel.
- Retained the earlier persistent-seed availability simulation only as a
  compatibility fallback; no game save, AI, grouping, invitation, or COOP sync
  is changed.
- Fail `/ready` and `/rollparty` closed while the game reports an active raid;
  the mod does not interpret raid rosters or subgroups.
- Treat inactive player and Sim objects as `UNAVAILABLE` before reading their
  liveness, preventing stale readiness rows during scene transitions.

## 0.1.0 - Initial development build

- Added `/ready` temporary party readiness panel.
- Added verified `READY`, `DEAD`, `IN COMBAT`, and `UNAVAILABLE` states.
- Added `/roll` with a default 1-100 range and optional maximum.
- Added `/rollparty` temporary local-party roll panel.
- Added reflection-only COOP exclusion for remote humans and remote-owned Sims.
- Added automatic panel timeout and zone-change cleanup.
- Added build/install and uninstall helpers.


## Pre-release development - Suite UI/API coherence handoff

- Added optional, versioned `PartyToolsControlApi` discovery/control surface for Suite Hub without a hard Hub dependency.
- Kept standalone commands and core gameplay authority intact.
- Documented the retained panel/launcher policy and Lunaris live-test requirement.
- Gated F7/Tools UI until the native world state has remained stable; no raid automation or AI added.
