using System;
using ErenshorPartyTools;

internal static class FriendAvailabilityTests
{
    internal static void Run()
    {
        NativeRosterUsesCurrentCharacterBinding();
        NonFriendAndGmFailClosed();
        FriendStableIdentityUsesNativeTrackingIndex();
        SameEpochIsDeterministic();
        HistoricalUtcUsesExactEpoch();
        PanelReopenDoesNotReroll();
        NextEpochCanChange();
        DifferentFriendsVary();
        DifferentPlayerCharactersAreIsolated();
        CurrentPartyForcesAvailable();
        PresentNonPartyFriendForcesOnline();
        NativeRosterChangeIsReflected();
        MissingNativeAuthorityFailsClosed();
        EpochShapeIsBoundedAndModerate();
        Console.WriteLine("FriendAvailabilityTests: PASS");
    }

    private static void NativeRosterUsesCurrentCharacterBinding()
    {
        Assert(NativeFriendRosterPolicy.IsCurrentCharacterFriend(2, 2, false),
            "matching FriendedBy and current slot should be a native friend");
        Assert(!NativeFriendRosterPolicy.IsCurrentCharacterFriend(1, 2, false),
            "another character's friend must be excluded");
    }

    private static void NonFriendAndGmFailClosed()
    {
        Assert(!NativeFriendRosterPolicy.IsCurrentCharacterFriend(-1, 2, false),
            "nonfriend must never enter the availability roster");
        Assert(!NativeFriendRosterPolicy.IsCurrentCharacterFriend(2, 2, true),
            "GM characters must match the native Friends-filter exclusion");
    }

    private static void FriendStableIdentityUsesNativeTrackingIndex()
    {
        string first;
        string second;
        Assert(FriendAvailability.TryComposeFriendKey(17, "Phanty", out first), "native friend identity should resolve");
        Assert(FriendAvailability.TryComposeFriendKey(17, "PHANTY RENAMED", out second), "same native tracking index should resolve after display-name change");
        Assert(first == second && first == "SIM:17", "simIndex is the stable primary native Friend identity");
        Assert(FriendAvailability.SameNativeFriendIdentity(17, "Phanty", 17, "Other"), "same native simIndex should match across object recreation");
    }

    private static void SameEpochIsDeterministic()
    {
        string character = CharacterKey(2, "Hero");
        string friend = FriendKey(2, "Fiora");
        FriendAvailabilityState first;
        FriendAvailabilityState second;
        Assert(FriendAvailability.TryGetSimulatedState(character, friend, 100L, out first), "first deterministic state should resolve");
        Assert(FriendAvailability.TryGetSimulatedState(character, friend, 100L, out second), "repeat deterministic state should resolve");
        Assert(first == second, "same character/friend/epoch must remain stable");
    }

    private static void HistoricalUtcUsesExactEpoch()
    {
        string character = CharacterKey(2, "Hero");
        string friend = FriendKey(2, "Fiora");
        DateTime instant = new DateTime(2026, 8, 21, 7, 15, 0, DateTimeKind.Utc);
        FriendAvailabilityState historical;
        FriendAvailabilityState direct;
        Assert(FriendAvailability.TryGetBaseStateAtUtc(character, friend, instant.Ticks, out historical),
            "valid UTC ticks should resolve historical base availability");
        Assert(FriendAvailability.TryGetSimulatedState(character, friend, FriendAvailability.GetEpoch(instant), out direct),
            "direct epoch lookup should resolve");
        Assert(historical == direct, "historical UTC lookup must use the exact deterministic epoch");
        Assert(!FriendAvailability.TryGetBaseStateAtUtc(character, friend, -1L, out historical),
            "invalid UTC ticks must fail closed");
        Assert(historical == FriendAvailabilityState.Unknown, "invalid history must remain Unknown");
    }

    private static void PanelReopenDoesNotReroll()
    {
        string character = CharacterKey(2, "Hero");
        string friend = FriendKey(4, "Cyndara");
        FriendAvailabilityState opened;
        FriendAvailabilityState reopened;
        FriendAvailability.TryGetSimulatedState(character, friend, 100L, out opened);
        FriendAvailability.TryGetSimulatedState(character, friend, 100L, out reopened);
        Assert(opened == reopened, "reopening the panel inside one epoch must not reroll a Friend");
    }

    private static void NextEpochCanChange()
    {
        string character = CharacterKey(2, "Hero");
        string friend = FriendKey(1, "Phanty");
        FriendAvailabilityState first;
        FriendAvailability.TryGetSimulatedState(character, friend, 100L, out first);
        bool changed = false;
        for (long epoch = 101L; epoch < 160L; epoch++)
        {
            FriendAvailabilityState next;
            FriendAvailability.TryGetSimulatedState(character, friend, epoch, out next);
            if (next != first) { changed = true; break; }
        }
        Assert(changed, "a later epoch must be capable of changing roleplay availability");
    }

    private static void DifferentFriendsVary()
    {
        string character = CharacterKey(2, "Hero");
        FriendAvailabilityState first;
        FriendAvailabilityState second;
        FriendAvailability.TryGetSimulatedState(character, FriendKey(1, "Phanty"), 100L, out first);
        FriendAvailability.TryGetSimulatedState(character, FriendKey(2, "Fiora"), 100L, out second);
        Assert(first != second, "different native Friend identities should be able to vary in the same epoch");
    }

    private static void DifferentPlayerCharactersAreIsolated()
    {
        FriendAvailabilityState first;
        FriendAvailabilityState second;
        FriendAvailability.TryGetSimulatedState(CharacterKey(1, "Hero"), FriendKey(3, "Dancer"), 100L, out first);
        FriendAvailability.TryGetSimulatedState(CharacterKey(2, "Alt"), FriendKey(3, "Dancer"), 100L, out second);
        Assert(first != second, "character key must participate in availability so characters are isolated");
    }

    private static void CurrentPartyForcesAvailable()
    {
        FriendAvailabilityState state = FriendAvailability.ApplyObservedPresence(FriendAvailabilityState.Offline, true, false);
        Assert(state == FriendAvailabilityState.InParty, "current party must override simulated Offline with In Party");
        Assert(FriendAvailability.IsAvailable(state), "In Party must count as available");
    }

    private static void PresentNonPartyFriendForcesOnline()
    {
        FriendAvailabilityState state = FriendAvailability.ApplyObservedPresence(FriendAvailabilityState.Offline, false, true);
        Assert(state == FriendAvailabilityState.Online, "an active local Friend in the current scene must not display Offline");
    }

    private static void NativeRosterChangeIsReflected()
    {
        Assert(NativeFriendRosterPolicy.IsCurrentCharacterFriend(2, 2, false), "friend should be included before native roster change");
        Assert(!NativeFriendRosterPolicy.IsCurrentCharacterFriend(-1, 2, false), "native unfriend must remove the candidate immediately");
    }

    private static void MissingNativeAuthorityFailsClosed()
    {
        string key;
        FriendAvailabilityState state;
        Assert(!NativeFriendRosterPolicy.IsCurrentCharacterFriend(0, -1, false), "missing current character slot must fail closed");
        Assert(!FriendAvailability.TryComposeCharacterKey(-1, "Hero", out key), "missing native character slot must not create a scope key");
        Assert(!FriendAvailability.TryComposeCharacterKey(2, "", out key), "missing native character name must not create a scope key");
        Assert(!FriendAvailability.TryGetSimulatedState(null, "SIM:1", 1L, out state), "missing character authority must not invent availability");
        Assert(!FriendAvailability.TryGetSimulatedState("SLOT:2|CHAR:HERO", null, 1L, out state), "missing Friend identity must not invent availability");
    }

    private static void EpochShapeIsBoundedAndModerate()
    {
        Assert(FriendAvailability.EpochHours == 4, "availability epoch should remain a useful multi-hour session span");
        Assert(FriendAvailability.OnlinePercent == 65, "default roleplay-online rate should remain moderate without a tuning surface");
        long start = FriendAvailability.GetEpoch(new DateTime(2026, 8, 21, 0, 0, 0, DateTimeKind.Utc));
        long same = FriendAvailability.GetEpoch(new DateTime(2026, 8, 21, 3, 59, 59, DateTimeKind.Utc));
        long next = FriendAvailability.GetEpoch(new DateTime(2026, 8, 21, 4, 0, 0, DateTimeKind.Utc));
        Assert(start == same, "four-hour epoch must remain stable within its window");
        Assert(next == start + 1L, "next four-hour boundary must advance exactly one epoch");
    }

    private static string CharacterKey(int slot, string name)
    {
        string key;
        if (!FriendAvailability.TryComposeCharacterKey(slot, name, out key)) throw new InvalidOperationException("character key setup failed");
        return key;
    }

    private static string FriendKey(int simIndex, string name)
    {
        string key;
        if (!FriendAvailability.TryComposeFriendKey(simIndex, name, out key)) throw new InvalidOperationException("friend key setup failed");
        return key;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
