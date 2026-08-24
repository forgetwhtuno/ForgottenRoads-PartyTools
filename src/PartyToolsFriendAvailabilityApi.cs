namespace ErenshorPartyTools
{
    // Optional, read-only cross-mod contract. Consumers can ask about a native Friend by name;
    // Unknown includes nonfriends and any case where Party Tools cannot prove native roster authority.
    public static class PartyToolsFriendAvailabilityApi
    {
        public const int ContractVersion = 2;

        public static bool IsAvailable(string friendName)
        {
            FriendAvailabilityState state;
            return PartyStateReader.TryGetFriendAvailability(friendName, out state) && FriendAvailability.IsAvailable(state);
        }

        public static string GetFriendAvailability(string friendName)
        {
            FriendAvailabilityState state;
            if (!PartyStateReader.TryGetFriendAvailability(friendName, out state)) return "Unknown";
            switch (state)
            {
                case FriendAvailabilityState.InParty: return "InParty";
                case FriendAvailabilityState.Online: return "Online";
                case FriendAvailabilityState.Offline: return "Offline";
                default: return "Unknown";
            }
        }

        public static string GetBaseAvailabilityAtUtc(string friendName, long utcTicks)
        {
            FriendAvailabilityState state;
            if (!PartyStateReader.TryGetBaseFriendAvailabilityAtUtc(friendName, utcTicks, out state)) return "Unknown";
            return state == FriendAvailabilityState.Online ? "Online" : "Offline";
        }

        public static System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, string>>
            GetBaseAvailabilitySnapshotAtUtc(long utcTicks)
        {
            return PartyStateReader.BuildBaseFriendAvailabilitySnapshotAtUtc(utcTicks);
        }
    }
}
