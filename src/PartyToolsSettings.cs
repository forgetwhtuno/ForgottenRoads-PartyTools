using System;
using Lunaris.Config;

namespace ErenshorPartyTools
{
    // Loader-neutral ConfigEntry-style shim. Keeping the Value surface makes the Lunaris
    // migration mechanical and lets the existing call sites keep their proven access pattern.
    internal sealed class PartyToolsConfigEntry<T>
    {
        private readonly Func<T> _get;
        private readonly Action<T> _set;

        internal PartyToolsConfigEntry(Func<T> get, Action<T> set)
        {
            _get = get;
            _set = set;
        }

        internal T Value
        {
            get { return _get(); }
            set { _set(value); }
        }
    }

    internal sealed class PartyToolsSettings
    {
        public PartyToolsSettings() { }

        [Config("PanelOffsetX", "UI.Legacy", "Legacy top-origin panel offset retained for config compatibility but ignored by retained uGUI.")]
        public float PanelOffsetX = 0f;

        [Config("PanelOffsetY", "UI.Legacy", "Legacy top-origin panel offset retained for config compatibility but ignored by retained uGUI.")]
        public float PanelOffsetY = 0f;

        [Config("PanelNormalizedX", "UI", "Retained-uGUI panel horizontal position normalized 0..1 from bottom-left. -1 uses the safe default.")]
        public float PanelNormalizedX = -1f;

        [Config("PanelNormalizedY", "UI", "Retained-uGUI panel vertical position normalized 0..1 from bottom-left. -1 uses the safe default.")]
        public float PanelNormalizedY = -1f;

        [Config("ShowLauncher", "UI", "Show the Party Tools launcher when Suite Hub is usable. If Hub or this module bridge is unavailable, fallback visibility is forced on.")]
        public bool ShowLauncher = true;

        [Config("LauncherNormalizedX", "UI", "Retained-uGUI launcher horizontal position normalized 0..1 from bottom-left. -1 uses the safe default.")]
        public float LauncherNormalizedX = -1f;

        [Config("LauncherNormalizedY", "UI", "Retained-uGUI launcher vertical position normalized 0..1 from bottom-left. -1 uses the safe default.")]
        public float LauncherNormalizedY = -1f;

        [Config("OpenMenuKey", "UI.Legacy",
            "Deprecated compatibility value. Retained uGUI uses its launcher/Hub Open Panel action; no global hotkey is required or polled for normal access.")]
        public UnityEngine.KeyCode OpenMenuKey = UnityEngine.KeyCode.F7;

        [Config("Enabled", "Roll Chatter",
            "Show one concise local chat summary for a party roll. Party Tools does not impersonate Sim dialogue and this never controls loot or gameplay.")]
        public bool RollChatterEnabled = true;

        [Config("Enabled", "FriendAvailability",
            "Legacy compatibility value; ignored. Friends Online always uses Party Tools deterministic roleplay availability over the native Friends roster.")]
        public bool FriendAvailabilityEnabled = true;

        [Config("SessionHours", "FriendAvailability",
            "Legacy compatibility value; ignored. Friends Online uses a fixed four-hour deterministic epoch.")]
        public int FriendAvailabilitySessionHours = 3;

        [Config("Seed", "FriendAvailability",
            "Legacy compatibility value; ignored. Friends Online uses a fixed versioned deterministic salt.")]
        public string FriendAvailabilitySeed = string.Empty;

        [Config("Friends", "FriendAvailability",
            "Legacy compatibility value; ignored. Only Erenshor's native Friends roster supplies candidates.")]
        public string FriendAvailabilityFriends = string.Empty;
    }
}
