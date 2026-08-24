namespace ErenshorPartyTools
{
    // Mirrors GroupBuilder.FilterBy's native Friends predicate. Native Erenshor owns
    // roster membership; Party Tools must never promote a nonfriend into its availability
    // surface merely because that Sim is loaded or happens to be online.
    internal static class NativeFriendRosterPolicy
    {
        internal static bool IsCurrentCharacterFriend(int friendedBy, int currentCharacterSlot, bool isGmCharacter)
        {
            return !isGmCharacter && currentCharacterSlot >= 0 && friendedBy == currentCharacterSlot;
        }
    }
}