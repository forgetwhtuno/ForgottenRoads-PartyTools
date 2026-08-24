# Erenshor Party Tools 0.1.8

Part of the **Forgotten Roads for Erenshor** mod collection.

Party Tools is a lightweight social/diagnostic utility for local Erenshor parties. It does not control combat, movement, healing, loot, rolls in the game engine, or rewards.

## Commands and panel

```text
/tools                    open the retained Party Tools panel
/ready                    show a local-party ready check
/roll [maximum]           roll for the player, 1..1,000,000
/rollparty [maximum]      display cosmetic rolls for the current local party
/ptwho                    show the current character's friend availability
```

Normal mouse access is through the retained-uGUI **Party Tools** launcher or Suite Hub's **Open Panel** action. There is no polled global hotkey for normal access; the old F7 config value is retained only as legacy config compatibility. The panel has a visible X, a dedicated non-button drag surface, normalized position persistence, resolution reclamping, and a scrollable result area.

The same four deterministic tools are available as panel buttons: **Ready Check**, **Roll 1-100**, **Party Roll 1-100**, and **Friends Online**. The panel also shows explicit **Launcher [ON/OFF]** and **Roll Summary [ON/OFF]** state buttons rather than ambiguous tiny toggles. Commands retain their exact syntax/bounds and remain useful as compatibility/debug recovery paths.

## Ready checks

`/ready` reads the currently observable local party and displays readiness rows. It is unavailable during raids. The check refreshes only verified local state and times out after a bounded 10 seconds, returning the panel to its neutral tools view. It is a readiness display, not a command that changes party state or forces a player/Sim response. No AI readiness or speculative "recovering" state is inferred.

## Cosmetic rolls

`/roll` uses rejection-sampled cryptographic randomness and makes exactly one native chat append attempt for its local result. `/rollparty` generates one result for the player plus each verified eligible local Sim, displays those results in the retained panel, and can make one native append attempt for one concise local summary line. It never impersonates Sim responses. The winner is not awarded an item, currency, XP, loot rights, or any other gameplay state. The summary line can be disabled in configuration.

## Friend availability

`/ptwho` reads the current character's native friend roster using the same character binding as Erenshor's Friends filter. It shows each friend's native state: **AVAILABLE**, **BUSY - GROUPED**, or **OFFLINE**. While the view remains open it refreshes on a bounded cadence so the list does not go stale. The tool is read-only: it never sends invites or changes the friend list.

## Compatibility and safety

Party Tools uses narrow Harmony command interception, read-only state inspection, and a fail-closed monotonic `CameraController.UsingUI()` postfix only while Party Tools owns a left-button retained-UI drag. It does not patch player clicks, poll camera axes, or force `EditUIMode`. It preserves commands it does not own, stays deliberately limited in raids, keeps panel refresh bounded, and fails closed when native current-party state is unavailable. It has no Deep Sims, Ollama, network, or gameplay dependency; COOP compatibility is runtime-detected so remote-human readiness is never invented.

## UI ownership boundary

Party Tools owns only its retained **PARTY TOOLS** launcher/panel and the Ready Check, Roll, Party Roll, Friends Online, launcher preference, and roll-summary controls described above. The tall in-game party command stack containing controls such as **Attack, Assist, Pull Target, Auto Pull, Guard** is Erenshor's native party/group UI, not a Party Tools panel. This mod does not patch or restyle that stack in the current source, so changes to it are outside Party Tools' safe surface.

## Installation

Party Tools requires **Lunaris**. BepInEx is not required, and nothing else needs to be installed —
Lunaris already ships Harmony.

**Lunaris (recommended)**

1. Install Lunaris for Erenshor and launch the game once so the loader sets itself up.
2. Open the Lunaris plugin installer in game.
3. Search for **Erenshor Party Tools** and install it.
4. Enable the plugin.

**Manual**

Download the release archive and copy **only** `ErenshorPartyTools.dll` into the `plugins` folder
next to `Erenshor.exe`. The `README.md`, `CHANGELOG.md`, `LICENSE`, and `NOTICE` files in the
archive are documentation — do not copy them into `plugins`.

## Updating

Install the new version the same way and let it replace `ErenshorPartyTools.dll`. Your settings live
in `plugins/config/forgetwhtuno.erenshor.partytools.lpcfg` and are preserved across updates; Party
Tools never deletes them.

## Configuration

Settings are edited through Lunaris (or Suite Hub, if installed). The defaults are safe for normal
play:

| Setting | Default | What it does |
| --- | --- | --- |
| `ShowLauncher` | on | Show the on-screen Party Tools launcher. Forced visible whenever Suite Hub is absent or unusable, so the mod is never unreachable. |
| `Roll Chatter / Enabled` | on | Emit one concise chat summary line for a party roll. Purely cosmetic. |
| `PanelNormalizedX/Y`, `LauncherNormalizedX/Y` | auto | Remembered on-screen positions. `-1` means "use the safe default". |

Entries under `UI.Legacy` and `FriendAvailability` are retained only so older config files keep
loading. They no longer affect behavior.

## Optional Friend availability API

Contract v2 preserves the original current-state calls and adds read-only historical base availability
for native Friends at exact UTC ticks: `GetBaseAvailabilityAtUtc(name, utcTicks)` and
`GetBaseAvailabilitySnapshotAtUtc(utcTicks)`. Historical answers are deterministic `Online`/`Offline`
base state only; current party membership and scene presence never rewrite history. Missing native
roster authority fails closed, and the API has no Deep Sims, network, or AI dependency.

## Build from source

`BUILD_AND_INSTALL.ps1` compiles the plugin against your installed Erenshor/Lunaris assemblies and
installs it to `<Erenshor>\plugins\ErenshorPartyTools.dll`. This repository intentionally does not
redistribute Erenshor, Unity, or Lunaris assemblies. The plugin identifier is
`forgetwhtuno.erenshor.partytools`, version `0.1.8`. A legacy BepInEx release remains available in
this repository's Git history.

Run the deterministic test suite with `RUN_TESTS.ps1`.

## Support / issues

Please report bugs and feature requests on the
[GitHub issue tracker](https://github.com/forgetwhtuno/ForgottenRoads-PartyTools/issues).

## License

See [LICENSE](LICENSE) and [NOTICE](NOTICE).

## Credits and Inspiration

### Compatibility / related projects

- **[Erenshor COOP](https://github.com/MizukiBelhi/ErenshorCoop) by MizukiBelhi** — important technical reference and compatibility target for remote-human/networked-Sim detection. I have also tested against a locally updated copy for recent Erenshor and Deep Sims compatibility.

## Development note

The goal is to build features for Erenshor, with development guided through design, testing, playtesting, audits, and iteration against the game. Bug reports, code review, corrections, and contributions from experienced Erenshor modders are welcome.

This is an unofficial, community-made mod for Erenshor and is not affiliated with or endorsed by the game's developer.


## Optional Suite Hub integration

Forgotten Roads Hub is **optional**. Party Tools exposes its versioned `PartyToolsControlApi` through Aura without referencing Hub types or assuming Hub load order. The Hub can show concise state, `Show Party Tools Launcher`, `Party roll chat summary`, and the conventional panel/actions. The module advertises `ui.state` + `closePanel`. Escape ownership is deterministic: if a well-formed Hub endpoint is present, Party Tools does **not** poll Escape—even when Hub reports `quickClose=0`; the player uses explicit X/close controls until Hub has a verified native consume path. A local Escape fallback exists only when the Hub endpoint is genuinely unavailable, preserving standalone usability without competing with a healthy Hub.

The retained panel and commands stay independently usable. Launcher fallback is mandatory: with Hub absent/unusable or this module's bridge unregistered, the on-screen Party Tools launcher is forced visible even when the saved Hub-era preference is off. With Hub and the bridge usable, the preference is obeyed.

Ready-check inference, roll bounds/output behavior, raid limitations, friend availability, and COOP remote-human handling remain deterministic and authoritative in Party Tools.
