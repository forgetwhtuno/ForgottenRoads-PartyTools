using System;
using System.Collections.Generic;
using System.Globalization;
using Lunaris;
using Lunaris.Config;
using HarmonyLib;

namespace ErenshorPartyTools
{
    [LunarisPlugin(PluginGuid, PluginVersion, "forgetwhtuno",
        "Deterministic party utilities: ready checks, cosmetic rolls, and friend availability.")]
    [LunarisPermission(LunarisPermission.Reflection | LunarisPermission.Harmony)]
    public sealed class ErenshorPartyToolsPlugin : LunarisPlugin
    {
        internal const string PluginGuid = "forgetwhtuno.erenshor.partytools";
        internal const string PluginName = "Erenshor Party Tools";
        internal const string PluginVersion = "0.1.8";
        internal const int MaximumRollSides = 1000000;
        private const float UpdateErrorLogIntervalSeconds = 30f;

        internal static ErenshorPartyToolsPlugin Instance;
        internal static bool IsSuiteQuickCloseProviderRegistered { get { return Instance != null && Instance._auraProvider != null && Instance._auraProvider.Registered; } }

        private Harmony _harmony;
        private bool _ownsInstance;
        private bool _pendingExternalOpen;
        private bool _pendingExternalClose;
        private int _pendingControlAction;
        private int _pendingControlSides;
        private PartyToolsSettings _settings;
        private PartyToolsConfigEntry<float> _panelNormalizedX;
        private PartyToolsConfigEntry<float> _panelNormalizedY;
        private PartyToolsConfigEntry<bool> _showLauncher;
        private PartyToolsConfigEntry<float> _launcherNormalizedX;
        private PartyToolsConfigEntry<float> _launcherNormalizedY;
        private PartyToolsConfigEntry<bool> _friendAvailabilityEnabled;
        private PartyToolsConfigEntry<int> _friendAvailabilitySessionHours;
        private PartyToolsConfigEntry<string> _friendAvailabilitySeed;
        private PartyToolsConfigEntry<string> _friendAvailabilityFriends;
        private PartyToolsConfigEntry<bool> _rollChatterEnabled;
        private PartyToolsSuiteAuraProvider _auraProvider;
        private float _nextUpdateErrorLogAt;
        private int _suppressedUpdateErrors;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                try { Logging.LogWarning("Erenshor Party Tools duplicate plugin instance ignored."); } catch { }
                enabled = false;
                return;
            }
            Instance = this;
            _ownsInstance = true;

            CoopCompatibility.Initialize();
            Roller.Initialize();

            _settings = new PartyToolsSettings();
            Config.Register(ref _settings);
            InitializeConfigEntries();

            PartyToolsPanel.ConfigurePosition(_panelNormalizedX.Value, _panelNormalizedY.Value, _launcherNormalizedX.Value, _launcherNormalizedY.Value, PersistPanelPosition, PersistLauncherPosition);
            SuiteUiPolicy.InitializeHubPresence(this);

            _harmony = new Harmony(PluginGuid);
            try
            {
                _harmony.PatchAll();
            }
            catch (Exception ex)
            {
                try { _harmony.UnpatchSelf(); } catch { }
                Logging.LogError("Erenshor Party Tools Harmony hooks unavailable (" + ex.GetType().Name + "). Retained UI/Aura will continue, but slash-command and camera-containment hooks are disabled.");
            }

            try { _auraProvider = new PartyToolsSuiteAuraProvider(this); }
            catch (Exception ex) { Logging.LogError("Erenshor Party Tools Suite Aura provider failed to register (" + ex.GetType().Name + ")."); }

            Logging.LogInfo("Erenshor Party Tools " + PluginVersion + " loaded. Use the retained launcher/Hub panel; compatibility commands: /tools, /ready, /roll [max], /rollparty [max], /ptwho.");
        }

        private void InitializeConfigEntries()
        {
            _panelNormalizedX = new PartyToolsConfigEntry<float>(delegate { return _settings.PanelNormalizedX; }, delegate(float v) { _settings.PanelNormalizedX = v; });
            _panelNormalizedY = new PartyToolsConfigEntry<float>(delegate { return _settings.PanelNormalizedY; }, delegate(float v) { _settings.PanelNormalizedY = v; });
            _showLauncher = new PartyToolsConfigEntry<bool>(delegate { return _settings.ShowLauncher; }, delegate(bool v) { _settings.ShowLauncher = v; });
            _launcherNormalizedX = new PartyToolsConfigEntry<float>(delegate { return _settings.LauncherNormalizedX; }, delegate(float v) { _settings.LauncherNormalizedX = v; });
            _launcherNormalizedY = new PartyToolsConfigEntry<float>(delegate { return _settings.LauncherNormalizedY; }, delegate(float v) { _settings.LauncherNormalizedY = v; });
            _rollChatterEnabled = new PartyToolsConfigEntry<bool>(delegate { return _settings.RollChatterEnabled; }, delegate(bool v) { _settings.RollChatterEnabled = v; });
            _friendAvailabilityEnabled = new PartyToolsConfigEntry<bool>(delegate { return _settings.FriendAvailabilityEnabled; }, delegate(bool v) { _settings.FriendAvailabilityEnabled = v; });
            _friendAvailabilitySessionHours = new PartyToolsConfigEntry<int>(delegate { return _settings.FriendAvailabilitySessionHours; }, delegate(int v) { _settings.FriendAvailabilitySessionHours = v; });
            _friendAvailabilitySeed = new PartyToolsConfigEntry<string>(delegate { return _settings.FriendAvailabilitySeed; }, delegate(string v) { _settings.FriendAvailabilitySeed = v; });
            _friendAvailabilityFriends = new PartyToolsConfigEntry<string>(delegate { return _settings.FriendAvailabilityFriends; }, delegate(string v) { _settings.FriendAvailabilityFriends = v; });
        }

        private void Update()
        {
            try
            {
                bool ready = SuiteUiPolicy.IsGameplayReady();
                if (_pendingExternalClose) { _pendingExternalClose = false; PartyToolsPanel.Close(); }
                if (_pendingExternalOpen) { _pendingExternalOpen = false; if (ready) PartyToolsPanel.ShowCommandMenu(HandleMenuAction); }
                if (_pendingControlAction != 0 && ready)
                {
                    int action = _pendingControlAction; int sides = _pendingControlSides;
                    _pendingControlAction = 0; _pendingControlSides = 0;
                    if (action == 1) ShowReadyCheck();
                    else if (action == 2) ShowLocalRoll(sides, true);
                    else if (action == 3) ShowPartyRoll(sides);
                    else if (action == 4) ShowPartyWho();
                }
                if (!ready) { _pendingControlAction = 0; _pendingControlSides = 0; PartyToolsPanel.Close(); }
                bool bridge = _auraProvider != null && _auraProvider.Registered;
                bool launcher = SuiteLauncherPolicy.ShouldShow(ready, SuiteUiPolicy.IsHubAvailable(), bridge, ShowLauncherPreference);
                PartyToolsPanel.Tick(launcher);
            }
            catch (Exception ex)
            {
                LogUpdateFailure(ex);
                PartyToolsPanel.Close(); PartyToolsPanel.ReleaseDrag();
            }
        }

        // A persistent Update failure would otherwise write one line per frame. Report the first
        // one immediately, then at most one bounded summary per interval so a broken frame can
        // never flood the shared Lunaris log.
        private void LogUpdateFailure(Exception ex)
        {
            if (UnityEngine.Time.unscaledTime < _nextUpdateErrorLogAt) { _suppressedUpdateErrors++; return; }
            string suffix = _suppressedUpdateErrors > 0
                ? " (" + _suppressedUpdateErrors.ToString(CultureInfo.InvariantCulture) + " similar failures suppressed since the last report)"
                : string.Empty;
            _suppressedUpdateErrors = 0;
            _nextUpdateErrorLogAt = UnityEngine.Time.unscaledTime + UpdateErrorLogIntervalSeconds;
            try { Logging.LogError("Party Tools update/UI failed (" + ex.GetType().Name + ")." + suffix); } catch { }
        }

        private void OnDestroy()
        {
            if (!_ownsInstance) return;
            _ownsInstance = false;
            // Lunaris can unload/reload plugins while Erenshor is running. Release everything
            // this plugin owns before clearing the singleton.
            try { if (_auraProvider != null) _auraProvider.Unregister(); } catch { }
            _auraProvider = null;
            try { PartyToolsPanel.Dispose(); } catch { }
            try { CoopCompatibility.Shutdown(); } catch { }
            try { Roller.Shutdown(); } catch { }
            try { if (_harmony != null) _harmony.UnpatchSelf(); } catch { }
            _harmony = null;
            _pendingExternalOpen = _pendingExternalClose = false; _pendingControlAction = _pendingControlSides = 0;
            _nextUpdateErrorLogAt = 0f; _suppressedUpdateErrors = 0;
            SuiteUiPolicy.Reset();
            if (Instance == this) Instance = null;
        }

        internal void RequestOpenToolsPanel() { _pendingExternalOpen = true; }
        internal void RequestCloseToolsPanel() { _pendingExternalClose = true; }
        internal void ControlReadyCheck() { _pendingControlAction = 1; _pendingControlSides = 0; }
        internal void ControlRoll(int sides) { _pendingControlAction = 2; _pendingControlSides = sides; }
        internal void ControlPartyRoll(int sides) { _pendingControlAction = 3; _pendingControlSides = sides; }
        internal void ControlPartyWho() { _pendingControlAction = 4; _pendingControlSides = 0; }

        private void PersistPanelPosition(float x, float y)
        {
            if (_panelNormalizedX == null || _panelNormalizedY == null) return;
            _panelNormalizedX.Value = x; _panelNormalizedY.Value = y; Config.Save();
        }

        private void PersistLauncherPosition(float x, float y)
        {
            if (_launcherNormalizedX == null || _launcherNormalizedY == null) return;
            _launcherNormalizedX.Value = x; _launcherNormalizedY.Value = y; Config.Save();
        }

        internal bool ShowLauncherPreference { get { return _showLauncher == null || _showLauncher.Value; } }
        internal bool RollChatterPreference { get { return _rollChatterEnabled != null && _rollChatterEnabled.Value; } }
        internal bool FriendFallbackPreference { get { return _friendAvailabilityEnabled != null && _friendAvailabilityEnabled.Value; } }
        internal void SetShowLauncherPreference(bool value) { if (_showLauncher != null) { _showLauncher.Value = value; Config.Save(); } }
        internal void SetRollChatterPreference(bool value) { if (_rollChatterEnabled != null) { _rollChatterEnabled.Value = value; Config.Save(); } }
        internal void SetFriendFallbackPreference(bool value) { if (_friendAvailabilityEnabled != null) { _friendAvailabilityEnabled.Value = value; Config.Save(); } }
        internal void ResetLauncherPosition() { if (_launcherNormalizedX != null) _launcherNormalizedX.Value = PartyToolsUiGeometry.Unset; if (_launcherNormalizedY != null) _launcherNormalizedY.Value = PartyToolsUiGeometry.Unset; PartyToolsPanel.ResetLauncherPosition(); Config.Save(); }

        internal void Chat(string message, string color)
        {
            // Use exactly one native append attempt per Party Tools action. Falling back to a
            // second overload after an exception can duplicate a line if the first overload
            // appended successfully and then failed later in the native presentation path.
            try { UpdateSocialLog.LogAdd(message, color); }
            catch { }
        }

        internal void LogPatchError(Exception ex)
        {
            try { Logging.LogError("Party Tools command patch failed (" + ex.GetType().Name + ")."); }
            catch { }
        }

        internal bool TryHandle(TypeText typeText, string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return false;

            string command = raw.Trim();
            string argument;

            if (TryMatchCommand(command, "/tools", out argument))
            {
                ClearInput(typeText);
                if (!string.IsNullOrWhiteSpace(argument))
                {
                    Chat("[Party Tools] Usage: /tools", "yellow");
                    return true;
                }
                PartyToolsPanel.ShowCommandMenu(HandleMenuAction);
                return true;
            }

            // Check /rollparty before /roll so the longer command can never be
            // misinterpreted as the shorter one.
            if (TryMatchCommand(command, "/rollparty", out argument))
            {
                ClearInput(typeText);
                int sides;
                if (!TryParseSides(argument, out sides))
                {
                    Chat("[Party Tools] Usage: /rollparty [1-" + MaximumRollSides.ToString(CultureInfo.InvariantCulture) + "]", "yellow");
                    return true;
                }

                ShowPartyRoll(sides);
                return true;
            }

            if (TryMatchCommand(command, "/ptwho", out argument))
            {
                ClearInput(typeText);
                if (!string.IsNullOrWhiteSpace(argument))
                {
                    Chat("[Party Tools] Usage: /ptwho", "yellow");
                    return true;
                }

                ShowPartyWho();
                return true;
            }

            if (TryMatchCommand(command, "/ready", out argument))
            {
                ClearInput(typeText);
                if (!string.IsNullOrWhiteSpace(argument))
                {
                    Chat("[Party Tools] Usage: /ready", "yellow");
                    return true;
                }
                ShowReadyCheck();
                return true;
            }

            if (TryMatchCommand(command, "/roll", out argument))
            {
                ClearInput(typeText);
                int sides;
                if (!TryParseSides(argument, out sides))
                {
                    Chat("[Party Tools] Usage: /roll [1-" + MaximumRollSides.ToString(CultureInfo.InvariantCulture) + "]", "yellow");
                    return true;
                }

                ShowLocalRoll(sides, false);
                return true;
            }

            return false;
        }

        private void HandleMenuAction(PartyToolsAction action)
        {
            switch (action)
            {
                case PartyToolsAction.ReadyCheck: ShowReadyCheck(); break;
                case PartyToolsAction.Roll: ShowLocalRoll(100, true); break;
                case PartyToolsAction.PartyRoll: ShowPartyRoll(100); break;
                case PartyToolsAction.PartyWho: ShowPartyWho(); break;
            }
        }

        private void ShowReadyCheck()
        {
            if (PartyStateReader.IsRaidActive())
            {
                Chat("[Party Tools] /ready is unavailable during a raid.", "yellow");
                return;
            }
            try { PartyToolsPanel.ShowReadyCheck(); }
            catch (Exception ex) { Logging.LogError("Party Tools ready-check UI failed (" + ex.GetType().Name + ")."); }
        }

        private void ShowLocalRoll(int sides, bool showPanel)
        {
            int value = Roller.Roll(sides);
            string name = PartyStateReader.ReadPlayerName();
            Chat(name + " rolls " + value.ToString(CultureInfo.InvariantCulture) + " (1-" + sides.ToString(CultureInfo.InvariantCulture) + ").", "lightblue");
            if (showPanel) PartyToolsPanel.ShowLocalRoll(name, sides, value);
        }

        private void ShowPartyRoll(int sides)
        {
            if (PartyStateReader.IsRaidActive())
            {
                Chat("[Party Tools] /rollparty is unavailable during a raid.", "yellow");
                return;
            }
            try
            {
                List<PartyRollParticipant> participants = PartyStateReader.GetLocalRollParticipants();
                List<PanelRow> rows = new List<PanelRow>();
                List<PartyRollResult> results = new List<PartyRollResult>();
                for (int i = 0; i < participants.Count; i++)
                {
                    PartyRollParticipant participant = participants[i];
                    int value = Roller.Roll(sides);
                    results.Add(new PartyRollResult(participant, value));
                    rows.Add(new PanelRow(participant.Name, value.ToString(CultureInfo.InvariantCulture), false));
                }
                PartyToolsPanel.ShowPartyRoll(sides, rows);
                if (_rollChatterEnabled != null && _rollChatterEnabled.Value)
                    ShowPartyRollChatter(sides, results);
            }
            catch (Exception ex) { Logging.LogError("Party Tools /rollparty handling failed (" + ex.GetType().Name + ")."); }
        }

        private void ShowPartyRollChatter(int sides, List<PartyRollResult> results)
        {
            string summary = PartyRollSocial.Summary(sides, results);
            if (!string.IsNullOrWhiteSpace(summary)) Chat(summary, "lightblue");
        }

        private void ShowPartyWho()
        {
            try
            {
                bool rosterAvailable;
                int availableCount;
                int totalCount;
                List<PanelRow> rows = PartyStateReader.BuildFriendAvailabilityRows(out rosterAvailable, out availableCount, out totalCount);
                if (!rosterAvailable)
                {
                    Chat("[Party Tools] Friend roster is not ready yet.", "yellow");
                    return;
                }
                if (rows.Count == 0)
                {
                    Chat("[Party Tools] Erenshor's Friends filter is empty for this character.", "yellow");
                    return;
                }
                PartyToolsPanel.ShowPartyWho(rows, availableCount, totalCount);
            }
            catch (Exception ex) { Logging.LogError("Party Tools /ptwho handling failed (" + ex.GetType().Name + ")."); }
        }

        private static bool TryMatchCommand(string raw, string command, out string argument)
        {
            argument = null;
            if (raw == null || command == null) return false;
            if (!raw.StartsWith(command, StringComparison.OrdinalIgnoreCase)) return false;
            if (raw.Length > command.Length && !char.IsWhiteSpace(raw[command.Length])) return false;
            argument = raw.Length == command.Length ? string.Empty : raw.Substring(command.Length).Trim();
            return true;
        }

        private static bool TryParseSides(string argument, out int sides)
        {
            return PartyToolsCommandPolicy.TryParseSides(argument, MaximumRollSides, out sides);
        }

        private static void ClearInput(TypeText typeText)
        {
            try
            {
                if (typeText != null && typeText.typed != null) typeText.typed.text = string.Empty;
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(TypeText), "CheckCommands")]
    internal static class PartyToolsChatPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(TypeText __instance)
        {
            try
            {
                return ErenshorPartyToolsPlugin.Instance == null ||
                       __instance == null ||
                       __instance.typed == null ||
                       !ErenshorPartyToolsPlugin.Instance.TryHandle(__instance, __instance.typed.text);
            }
            catch (Exception ex)
            {
                if (ErenshorPartyToolsPlugin.Instance != null)
                    ErenshorPartyToolsPlugin.Instance.LogPatchError(ex);
                return true;
            }
        }
    }

}
