using System;
using System.Globalization;

namespace ErenshorPartyTools
{
    internal enum FriendAvailabilityState
    {
        Unknown,
        Offline,
        Online,
        InParty
    }

    // Pure deterministic roleplay-availability model. Native Erenshor decides who is a
    // Friend; this class only decides whether that already-authorized Friend is simulated
    // Online for the current character/epoch. It has no Unity, save-file, LLM, or network
    // dependency and persists no per-friend state.
    internal static class FriendAvailability
    {
        internal const int EpochHours = 4;
        internal const int OnlinePercent = 65;
        internal const string DeterministicSalt = "ForgottenRoads.PartyTools.FriendsOnline.v1";

        internal static bool TryComposeCharacterKey(int slotIndex, string characterName, out string key)
        {
            key = null;
            string normalizedName = NormalizeIdentity(characterName, 80);
            if (slotIndex < 0 || normalizedName == null) return false;
            key = "SLOT:" + slotIndex.ToString(CultureInfo.InvariantCulture) + "|CHAR:" + normalizedName;
            return true;
        }

        internal static bool TryComposeFriendKey(int simIndex, string simName, out string key)
        {
            key = null;
            string normalizedName = NormalizeIdentity(simName, 80);
            if (normalizedName == null) return false;

            // simIndex is a native persistent tracking identity. Keep a name fallback for
            // unusual/legacy tracking records whose index is unavailable, but never use a
            // scene object identity.
            key = simIndex >= 0 ? "SIM:" + simIndex.ToString(CultureInfo.InvariantCulture) : "NAME:" + normalizedName;
            return true;
        }

        internal static long GetEpoch(DateTime utcNow)
        {
            DateTime utc = utcNow.Kind == DateTimeKind.Utc ? utcNow : utcNow.ToUniversalTime();
            return utc.Ticks / (TimeSpan.TicksPerHour * EpochHours);
        }

        internal static bool TryGetBaseStateAtUtc(
            string characterKey,
            string friendKey,
            long utcTicks,
            out FriendAvailabilityState state)
        {
            state = FriendAvailabilityState.Unknown;
            if (utcTicks < DateTime.MinValue.Ticks || utcTicks > DateTime.MaxValue.Ticks) return false;
            return TryGetSimulatedState(characterKey, friendKey, GetEpoch(new DateTime(utcTicks, DateTimeKind.Utc)), out state);
        }

        internal static bool TryGetSimulatedState(
            string characterKey,
            string friendKey,
            long epoch,
            out FriendAvailabilityState state)
        {
            state = FriendAvailabilityState.Unknown;
            string character = NormalizeIdentity(characterKey, 200);
            string friend = NormalizeIdentity(friendKey, 200);
            if (character == null || friend == null || epoch < 0) return false;

            uint roll = StableHash(DeterministicSalt + "|" + character + "|" + friend + "|" + epoch.ToString(CultureInfo.InvariantCulture)) % 100u;
            state = roll < OnlinePercent ? FriendAvailabilityState.Online : FriendAvailabilityState.Offline;
            return true;
        }

        internal static FriendAvailabilityState ApplyObservedPresence(
            FriendAvailabilityState simulatedState,
            bool inCurrentParty,
            bool physicallyPresent)
        {
            if (inCurrentParty) return FriendAvailabilityState.InParty;
            if (physicallyPresent) return FriendAvailabilityState.Online;
            return simulatedState;
        }

        internal static bool IsAvailable(FriendAvailabilityState state)
        {
            return state == FriendAvailabilityState.Online || state == FriendAvailabilityState.InParty;
        }

        internal static bool SameNativeFriendIdentity(int leftIndex, string leftName, int rightIndex, string rightName)
        {
            if (leftIndex >= 0 && rightIndex >= 0) return leftIndex == rightIndex;
            string left = NormalizeIdentity(leftName, 80);
            string right = NormalizeIdentity(rightName, 80);
            return left != null && right != null && string.Equals(left, right, StringComparison.Ordinal);
        }

        private static string NormalizeIdentity(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            string normalized = value.Trim().ToUpperInvariant();
            return normalized.Length == 0 || normalized.Length > maxLength ? null : normalized;
        }

        private static uint StableHash(string value)
        {
            // FNV-1a: stable across .NET process restarts unlike string.GetHashCode().
            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 16777619u;
            }
            return hash;
        }
    }
}
