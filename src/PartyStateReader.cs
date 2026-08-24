using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ErenshorPartyTools
{
    internal static class PartyStateReader
    {
        internal static List<ReadyRow> BuildReadyRows()
        {
            List<ReadyRow> rows = new List<ReadyRow>();
            rows.Add(new ReadyRow("You", ReadPlayerReadyState()));

            SimPlayerTracking[] members = ReadGroupMembers();
            if (members == null || members.Length == 0) return rows;

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < members.Length; i++)
            {
                SimPlayerTracking tracking = members[i];
                string partyName = ReadTrackingName(tracking);
                if (partyName.Length == 0 || !seen.Add(partyName)) continue;

                SimPlayer sim = SafeAvatar(tracking);
                if (sim == null)
                {
                    rows.Add(new ReadyRow(partyName, ReadyState.Unavailable));
                    continue;
                }

                if (CoopCompatibility.IsRemoteCoopHuman(sim))
                {
                    // We can authoritatively identify COOP ownership from its explicit component,
                    // but Party Tools cannot authoritatively answer for that remote human.
                    rows.Add(new ReadyRow(partyName, ReadyState.RemotePlayer));
                    continue;
                }
                if (CoopCompatibility.IsRemoteCoopSim(sim))
                {
                    rows.Add(new ReadyRow(partyName, ReadyState.Unavailable));
                    continue;
                }

                if (!IsCurrentPartySim(sim))
                {
                    rows.Add(new ReadyRow(partyName, ReadyState.Unavailable));
                    continue;
                }

                rows.Add(new ReadyRow(SafeDisplayName(sim, partyName), ReadSimReadyState(sim)));
            }
            return rows;
        }

        internal static List<PanelRow> BuildPartyWhoRows()
        {
            List<PanelRow> rows = new List<PanelRow>();
            rows.Add(new PanelRow(ReadPlayerName(),
                PartySnapshotPolicy.Describe(PartyWhoKind.LocalPlayer, ReadPlayerLevel(), string.Empty), false));

            SimPlayerTracking[] members = ReadGroupMembers();
            if (members == null || members.Length == 0) return rows;

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < members.Length; i++)
            {
                SimPlayerTracking tracking = members[i];
                string partyName = ReadTrackingName(tracking);
                if (partyName.Length == 0 || !seen.Add(partyName)) continue;

                SimPlayer sim = SafeAvatar(tracking);
                bool hasAvatar = sim != null && IsAvailableSim(sim);
                bool remoteHuman = hasAvatar && CoopCompatibility.IsRemoteCoopHuman(sim);
                bool remoteSim = hasAvatar && CoopCompatibility.IsRemoteCoopSim(sim);
                bool currentParty = hasAvatar && !remoteHuman && !remoteSim && IsCurrentPartySim(sim);
                PartyWhoKind kind = PartySnapshotPolicy.Classify(false, hasAvatar, remoteHuman, remoteSim, currentParty);
                string display = hasAvatar ? SafeDisplayName(sim, partyName) : partyName;
                int level = kind == PartyWhoKind.LocalSim ? ReadTrackingLevel(tracking) : 0;
                string className = kind == PartyWhoKind.LocalSim ? ReadTrackingClass(tracking) : string.Empty;
                rows.Add(new PanelRow(display, PartySnapshotPolicy.Describe(kind, level, className),
                    kind != PartyWhoKind.LocalSim));
            }
            return rows;
        }

        // Native Erenshor determines the roster. Party Tools then applies its own deterministic
        // roleplay availability to those Friends only. Native online/grouped state is deliberately
        // not used as the roleplay-online decision.
        internal static List<PanelRow> BuildFriendAvailabilityRows(
            out bool rosterAvailable,
            out int availableCount,
            out int totalCount)
        {
            rosterAvailable = false;
            availableCount = 0;
            totalCount = 0;
            List<PanelRow> rows = new List<PanelRow>();
            try
            {
                if (GameData.CurrentCharacterSlot == null || GameData.SimMngr == null || GameData.SimMngr.Sims == null)
                    return rows;

                int currentSlot = GameData.CurrentCharacterSlot.index;
                string characterKey;
                if (!FriendAvailability.TryComposeCharacterKey(currentSlot, GameData.CurrentCharacterSlot.CharName, out characterKey))
                    return rows;

                rosterAvailable = true;
                long epoch = FriendAvailability.GetEpoch(DateTime.UtcNow);
                HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
                List<SimPlayerTracking> sims = GameData.SimMngr.Sims;
                for (int i = 0; i < sims.Count; i++)
                {
                    SimPlayerTracking tracking = sims[i];
                    if (tracking == null) continue;

                    string name = ReadTrackingName(tracking);
                    if (name.Length == 0) continue;

                    bool isFriend;
                    try
                    {
                        isFriend = NativeFriendRosterPolicy.IsCurrentCharacterFriend(
                            tracking.FriendedBy, currentSlot, tracking.IsGMCharacter);
                    }
                    catch { continue; }
                    if (!isFriend) continue;

                    string friendKey;
                    int simIndex;
                    try { simIndex = tracking.simIndex; }
                    catch { continue; }
                    if (!FriendAvailability.TryComposeFriendKey(simIndex, name, out friendKey) || !seen.Add(friendKey)) continue;

                    FriendAvailabilityState simulated;
                    if (!FriendAvailability.TryGetSimulatedState(characterKey, friendKey, epoch, out simulated)) continue;

                    bool inCurrentParty = IsCurrentPartyTracking(tracking);
                    SimPlayer avatar = SafeAvatar(tracking);
                    if (!inCurrentParty && avatar != null) inCurrentParty = IsCurrentPartySim(avatar);
                    bool physicallyPresent = IsPhysicallyPresentLocalFriend(avatar);
                    FriendAvailabilityState state = FriendAvailability.ApplyObservedPresence(simulated, inCurrentParty, physicallyPresent);

                    totalCount++;
                    if (FriendAvailability.IsAvailable(state)) availableCount++;
                    rows.Add(new PanelRow(name, FriendAvailabilityText(state), !FriendAvailability.IsAvailable(state)));
                }
            }
            catch
            {
                rosterAvailable = false;
                availableCount = 0;
                totalCount = 0;
                rows.Clear();
            }
            return rows;
        }

        internal static bool TryGetFriendAvailability(string friendName, out FriendAvailabilityState state)
        {
            state = FriendAvailabilityState.Unknown;
            if (string.IsNullOrWhiteSpace(friendName)) return false;
            try
            {
                if (GameData.CurrentCharacterSlot == null || GameData.SimMngr == null || GameData.SimMngr.Sims == null)
                    return false;

                int currentSlot = GameData.CurrentCharacterSlot.index;
                string characterKey;
                if (!FriendAvailability.TryComposeCharacterKey(currentSlot, GameData.CurrentCharacterSlot.CharName, out characterKey))
                    return false;

                long epoch = FriendAvailability.GetEpoch(DateTime.UtcNow);
                string requested = friendName.Trim();
                List<SimPlayerTracking> sims = GameData.SimMngr.Sims;
                for (int i = 0; i < sims.Count; i++)
                {
                    SimPlayerTracking tracking = sims[i];
                    string name = ReadTrackingName(tracking);
                    if (!string.Equals(name, requested, StringComparison.OrdinalIgnoreCase)) continue;

                    bool isFriend;
                    try
                    {
                        isFriend = tracking != null && NativeFriendRosterPolicy.IsCurrentCharacterFriend(
                            tracking.FriendedBy, currentSlot, tracking.IsGMCharacter);
                    }
                    catch { continue; }
                    if (!isFriend) continue;

                    int simIndex;
                    try { simIndex = tracking.simIndex; }
                    catch { return false; }
                    string friendKey;
                    if (!FriendAvailability.TryComposeFriendKey(simIndex, name, out friendKey)) return false;

                    FriendAvailabilityState simulated;
                    if (!FriendAvailability.TryGetSimulatedState(characterKey, friendKey, epoch, out simulated)) return false;
                    bool inCurrentParty = IsCurrentPartyTracking(tracking);
                    SimPlayer avatar = SafeAvatar(tracking);
                    if (!inCurrentParty && avatar != null) inCurrentParty = IsCurrentPartySim(avatar);
                    state = FriendAvailability.ApplyObservedPresence(simulated, inCurrentParty, IsPhysicallyPresentLocalFriend(avatar));
                    return true;
                }
            }
            catch { }
            return false;
        }

        // Historical contract: native Friends membership is authoritative, while the returned
        // state is the deterministic base state at the requested UTC instant. Current party and
        // scene-presence observations intentionally do not rewrite history.
        internal static bool TryGetBaseFriendAvailabilityAtUtc(
            string friendName,
            long utcTicks,
            out FriendAvailabilityState state)
        {
            state = FriendAvailabilityState.Unknown;
            if (string.IsNullOrWhiteSpace(friendName)) return false;
            List<Dictionary<string, string>> snapshot = BuildBaseFriendAvailabilitySnapshotAtUtc(utcTicks);
            string requested = friendName.Trim();
            for (int i = 0; i < snapshot.Count; i++)
            {
                string name;
                string availability;
                if (!snapshot[i].TryGetValue("name", out name) ||
                    !string.Equals(name, requested, StringComparison.OrdinalIgnoreCase) ||
                    !snapshot[i].TryGetValue("availability", out availability)) continue;
                state = string.Equals(availability, "Online", StringComparison.Ordinal)
                    ? FriendAvailabilityState.Online
                    : FriendAvailabilityState.Offline;
                return true;
            }
            return false;
        }

        internal static List<Dictionary<string, string>> BuildBaseFriendAvailabilitySnapshotAtUtc(long utcTicks)
        {
            List<Dictionary<string, string>> result = new List<Dictionary<string, string>>();
            try
            {
                if (utcTicks < DateTime.MinValue.Ticks || utcTicks > DateTime.MaxValue.Ticks ||
                    GameData.CurrentCharacterSlot == null || GameData.SimMngr == null || GameData.SimMngr.Sims == null)
                    return result;

                int currentSlot = GameData.CurrentCharacterSlot.index;
                string characterKey;
                if (!FriendAvailability.TryComposeCharacterKey(currentSlot, GameData.CurrentCharacterSlot.CharName, out characterKey))
                    return result;

                HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
                List<SimPlayerTracking> sims = GameData.SimMngr.Sims;
                for (int i = 0; i < sims.Count; i++)
                {
                    SimPlayerTracking tracking = sims[i];
                    if (tracking == null) continue;
                    string name = ReadTrackingName(tracking);
                    if (name.Length == 0) continue;

                    bool isFriend;
                    try
                    {
                        isFriend = NativeFriendRosterPolicy.IsCurrentCharacterFriend(
                            tracking.FriendedBy, currentSlot, tracking.IsGMCharacter);
                    }
                    catch { continue; }
                    if (!isFriend) continue;

                    int simIndex;
                    try { simIndex = tracking.simIndex; }
                    catch { continue; }
                    string friendKey;
                    if (!FriendAvailability.TryComposeFriendKey(simIndex, name, out friendKey) || !seen.Add(friendKey)) continue;

                    FriendAvailabilityState state;
                    if (!FriendAvailability.TryGetBaseStateAtUtc(characterKey, friendKey, utcTicks, out state)) continue;
                    result.Add(new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        { "name", name },
                        { "stableId", friendKey },
                        { "availability", state == FriendAvailabilityState.Online ? "Online" : "Offline" }
                    });
                }
            }
            catch { result.Clear(); }
            return result;
        }

        internal static bool IsRaidActive()
        {
            try { return GameData.RaidActive; }
            catch { return false; }
        }

        internal static List<PartyRollParticipant> GetLocalRollParticipants()
        {
            List<PartyRollParticipant> result = new List<PartyRollParticipant>();
            result.Add(new PartyRollParticipant(ReadPlayerName(), true));

            SimPlayerTracking[] members = ReadGroupMembers();
            if (members == null || members.Length == 0) return result;

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < members.Length; i++)
            {
                SimPlayerTracking tracking = members[i];
                string partyName = ReadTrackingName(tracking);
                if (partyName.Length == 0 || !seen.Add(partyName)) continue;
                SimPlayer sim = SafeAvatar(tracking);
                if (!IsLocallyRollEligible(sim)) continue;
                result.Add(new PartyRollParticipant(SafeDisplayName(sim, partyName), false));
            }
            return result;
        }

        internal static string ReadPlayerName()
        {
            try
            {
                Character player = GameData.PlayerControl == null ? null : GameData.PlayerControl.Myself;
                if (player != null && player.MyStats != null && !string.IsNullOrWhiteSpace(player.MyStats.MyName))
                    return CleanDisplayName(player.MyStats.MyName, "You");
            }
            catch { }
            return "You";
        }

        internal static int ReadPlayerLevel()
        {
            try
            {
                Character player = GameData.PlayerControl == null ? null : GameData.PlayerControl.Myself;
                if (player != null && player.MyStats != null && player.MyStats.Level > 0) return player.MyStats.Level;
            }
            catch { }
            return 0;
        }

        private static ReadyState ReadPlayerReadyState()
        {
            Character player = null;
            try { player = GameData.PlayerControl == null ? null : GameData.PlayerControl.Myself; }
            catch { }
            bool available = IsAvailableCharacter(player);
            bool aliveKnown = false;
            bool alive = false;
            if (available)
            {
                try { alive = player.Alive; aliveKnown = true; }
                catch { }
            }
            bool combatKnown = false;
            bool inCombat = false;
            try { inCombat = GameData.InCombat; combatKnown = true; }
            catch { }
            return ReadyStatePolicy.Classify(available, false, aliveKnown, alive, combatKnown, inCombat);
        }

        private static ReadyState ReadSimReadyState(SimPlayer sim)
        {
            Character character = null;
            try { character = sim == null || sim.MyStats == null ? null : sim.MyStats.Myself; }
            catch { }
            bool available = IsAvailableSim(sim) && IsAvailableCharacter(character);
            bool aliveKnown = false;
            bool alive = false;
            if (available)
            {
                try { alive = character.Alive; aliveKnown = true; }
                catch { }
            }

            bool combatKnown = false;
            bool inCombat = false;
            if (available)
            {
                try { inCombat = sim.IsSimGroupInCombat(); combatKnown = true; }
                catch { }
                try
                {
                    NPC npc = character.MyNPC;
                    if (npc != null)
                    {
                        combatKnown = true;
                        if (npc.CurrentAggroTarget != null) inCombat = true;
                    }
                }
                catch { }
            }
            return ReadyStatePolicy.Classify(available, false, aliveKnown, alive, combatKnown, inCombat);
        }

        private static bool IsLocallyRollEligible(SimPlayer sim)
        {
            if (!IsAvailableSim(sim)) return false;
            if (CoopCompatibility.IsRemoteCoopHuman(sim) || CoopCompatibility.IsRemoteCoopSim(sim)) return false;
            return IsCurrentPartySim(sim);
        }

        private static SimPlayerTracking[] ReadGroupMembers()
        {
            try { return GameData.GroupMembers; }
            catch { return null; }
        }

        private static SimPlayer SafeAvatar(SimPlayerTracking tracking)
        {
            try { return tracking == null ? null : tracking.MyAvatar; }
            catch { return null; }
        }

        private static string ReadTrackingName(SimPlayerTracking tracking)
        {
            try { return tracking == null ? string.Empty : CleanDisplayName(tracking.SimName, string.Empty); }
            catch { return string.Empty; }
        }

        private static int ReadTrackingLevel(SimPlayerTracking tracking)
        {
            try { return tracking != null && tracking.Level > 0 ? tracking.Level : 0; }
            catch { return 0; }
        }

        private static string ReadTrackingClass(SimPlayerTracking tracking)
        {
            try { return tracking == null ? string.Empty : PartySnapshotPolicy.CleanClassName(tracking.ClassName); }
            catch { return string.Empty; }
        }

        internal static string FriendAvailabilityText(FriendAvailabilityState state)
        {
            switch (state)
            {
                case FriendAvailabilityState.InParty: return "IN PARTY";
                case FriendAvailabilityState.Online: return "ONLINE";
                case FriendAvailabilityState.Offline: return "OFFLINE";
                default: return "UNKNOWN";
            }
        }

        private static bool IsCurrentPartyTracking(SimPlayerTracking tracking)
        {
            if (tracking == null) return false;
            SimPlayerTracking[] members = ReadGroupMembers();
            if (members == null || members.Length == 0) return false;
            for (int i = 0; i < members.Length; i++)
            {
                SimPlayerTracking member = members[i];
                if (member == null) continue;
                if (object.ReferenceEquals(member, tracking)) return true;
                try
                {
                    if (FriendAvailability.SameNativeFriendIdentity(
                        tracking.simIndex, tracking.SimName, member.simIndex, member.SimName)) return true;
                }
                catch { }
            }
            return false;
        }

        private static bool IsPhysicallyPresentLocalFriend(SimPlayer sim)
        {
            if (!IsAvailableSim(sim)) return false;
            if (CoopCompatibility.IsRemoteCoopHuman(sim) || CoopCompatibility.IsRemoteCoopSim(sim)) return false;
            try
            {
                // A live avatar is only a real presence override when the Sim itself belongs
                // to the active Erenshor zone. Do not compare against the player's persistent
                // GameObject scene (which can live in DontDestroyOnLoad).
                Scene activeScene = SceneManager.GetActiveScene();
                return activeScene.IsValid() && sim.gameObject.scene.IsValid() &&
                       sim.gameObject.scene.handle == activeScene.handle;
            }
            catch { return false; }
        }

        private static bool IsCurrentPartySim(SimPlayer sim)
        {
            try
            {
                return sim != null && sim.InGroup && GameData.SimPlayerGrouping != null &&
                       GameData.SimPlayerGrouping.IsSimInPlayerGroup(sim);
            }
            catch { return false; }
        }

        private static bool IsAvailableSim(SimPlayer sim)
        {
            try
            {
                return sim != null && sim.gameObject != null && sim.gameObject.activeInHierarchy &&
                       sim.MyStats != null && sim.MyStats.Myself != null;
            }
            catch { return false; }
        }

        private static bool IsAvailableCharacter(Character character)
        {
            try { return character != null && character.gameObject != null && character.gameObject.activeInHierarchy; }
            catch { return false; }
        }

        private static string SafeDisplayName(SimPlayer sim, string fallback)
        {
            string name = ReadSimName(sim);
            return string.IsNullOrWhiteSpace(name) ? CleanDisplayName(fallback, "Party member") : name;
        }

        private static string ReadSimName(SimPlayer sim)
        {
            if (sim == null) return string.Empty;
            try
            {
                Character character = sim.MyStats == null ? null : sim.MyStats.Myself;
                NPC npc = character == null ? null : character.MyNPC;
                if (npc != null && !string.IsNullOrWhiteSpace(npc.NPCName)) return CleanDisplayName(npc.NPCName, string.Empty);
            }
            catch { }
            try
            {
                if (sim.MyStats != null && !string.IsNullOrWhiteSpace(sim.MyStats.MyName))
                    return CleanDisplayName(sim.MyStats.MyName, string.Empty);
            }
            catch { }
            return string.Empty;
        }

        private static string CleanDisplayName(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            string clean = value.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Replace('\0', ' ').Trim();
            if (clean.Length == 0) return fallback;
            return clean.Length <= 48 ? clean : clean.Substring(0, 48);
        }
    }
}
