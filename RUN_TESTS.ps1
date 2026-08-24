$ErrorActionPreference = "Stop"
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

function Find-Csc {
    foreach ($path in @(
        "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
        "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
    )) {
        if (Test-Path $path) { return $path }
    }
    throw "csc.exe not found. Install the .NET Framework Developer Pack or Visual Studio Build Tools."
}

$csc = Find-Csc
$out = Join-Path $env:TEMP "ErenshorPartyTools.PanelPositioningTests.exe"

& $csc /nologo /target:exe ("/out:{0}" -f $out) `
    (Join-Path $ScriptRoot "src\PanelPositioning.cs") `
    (Join-Path $ScriptRoot "src\FriendAvailability.cs") `
    (Join-Path $ScriptRoot "src\NativeFriendRosterPolicy.cs") `
    (Join-Path $ScriptRoot "src\PartyModels.cs") `
    (Join-Path $ScriptRoot "src\PartyRollSocial.cs") `
    (Join-Path $ScriptRoot "src\PartyToolsCommandPolicy.cs") `
    (Join-Path $ScriptRoot "src\PartyToolsReleasePolicies.cs") `
    (Join-Path $ScriptRoot "src\Roller.cs") `
    (Join-Path $ScriptRoot "src\PartyToolsUiGeometry.cs") `
    (Join-Path $ScriptRoot "src\SuiteLauncherPolicy.cs") `
    (Join-Path $ScriptRoot "tests\FriendAvailabilityTests.cs") `
    (Join-Path $ScriptRoot "tests\PartyRollSocialTests.cs") `
    (Join-Path $ScriptRoot "tests\RollerTests.cs") `
    (Join-Path $ScriptRoot "tests\UiAndCommandPolicyTests.cs") `
    (Join-Path $ScriptRoot "tests\PanelPositioningTests.cs")
if ($LASTEXITCODE -ne 0) {
    throw "Panel positioning test compilation failed."
}

try {
    & $out
    if ($LASTEXITCODE -ne 0) {
        throw "Panel positioning tests failed with exit code $LASTEXITCODE."
    }
}
finally {
    Remove-Item $out -Force -ErrorAction SilentlyContinue
}

# Deep playable-state source guards: cryptographic rejection-sampling RNG, one summary line only,
# conservative remote ownership, exact Suite launcher label, and hot-reload singleton safety.
$partyPluginSource = Get-Content (Join-Path $ScriptRoot "src\ErenshorPartyToolsPlugin.cs") -Raw
$partyStateSource = Get-Content (Join-Path $ScriptRoot "src\PartyStateReader.cs") -Raw
$partyRollSource = Get-Content (Join-Path $ScriptRoot "src\PartyRollSocial.cs") -Raw
$partyRngSource = Get-Content (Join-Path $ScriptRoot "src\Roller.cs") -Raw
$partyAuraSource = Get-Content (Join-Path $ScriptRoot "src\PartyToolsSuiteAuraProvider.cs") -Raw
if ($partyRngSource -match 'new\s+Random\s*\(') { throw "Party Tools RNG guard failed: System.Random reintroduced." }
if ($partyRngSource -notmatch 'RandomNumberGenerator') { throw "Party Tools RNG guard failed: cryptographic RNG missing." }
if ($partyRngSource -notmatch 'limit\s*=') { throw "Party Tools RNG guard failed: rejection-sampling limit missing." }
if ($partyPluginSource -notmatch 'Roller\.Initialize\(\)' -or $partyPluginSource -notmatch 'Roller\.Shutdown\(\)') { throw "Party Tools RNG lifecycle guard failed: RNG is not initialized/disposed with the plugin." }
if ($partyStateSource -match 'gameObject\.name') { throw "Party Tools identity guard failed: scene object name fallback reintroduced." }
if ($partyRollSource -match 'PersonalizeString|PartyRollTone|acknowledge|personality') { throw "Party Tools roll guard failed: synthetic Sim chatter/personality path reintroduced." }
if ($partyAuraSource -notmatch 'Show Party Tools Launcher') { throw "Party Tools Suite guard failed: exact launcher label missing." }
if ($partyPluginSource -notmatch 'Instance\s*!=\s*null\s*&&\s*Instance\s*!=\s*this') { throw "Party Tools lifecycle guard failed: duplicate plugin initialization is not rejected." }
$chatMethod = [regex]::Match($partyPluginSource, 'internal\s+void\s+Chat\([^)]*\)\s*\{[\s\S]*?\n\s*\}')
if (-not $chatMethod.Success -or ([regex]::Matches($chatMethod.Value, 'UpdateSocialLog\.LogAdd\(').Count -ne 1)) { throw "Party Tools chat guard failed: each action must make exactly one native log append attempt." }
$coopSource = Get-Content (Join-Path $ScriptRoot "src\CoopCompatibility.cs") -Raw
if ($partyPluginSource -notmatch 'CoopCompatibility\.Initialize\(\)') { throw "Party Tools lifecycle guard failed: optional COOP detection is not reinitialized on plugin load." }
if ($coopSource -notmatch 'AssemblyLoad\s*-=\s*OnAssemblyLoad' -or $coopSource -notmatch 'AssemblyLoad\s*\+=\s*OnAssemblyLoad') { throw "Party Tools lifecycle guard failed: optional COOP AssemblyLoad subscription is not idempotent." }
if ($partyPluginSource -match 'Logging\.Log\w+\([^\r\n]*\+\s*ex\s*\)') { throw "Party Tools privacy guard failed: full exception detail may expose local paths." }
Write-Host "PASS: Party Tools deep authority/RNG/lifecycle source guard"

# Release-correctness source guards: a live Hub endpoint must never cause Party Tools to compete for Escape.
$releasePolicySource = Get-Content (Join-Path $ScriptRoot "src\PartyToolsReleasePolicies.cs") -Raw
$suiteUiSource = Get-Content (Join-Path $ScriptRoot "src\SuiteUiPolicy.cs") -Raw
$panelSource = Get-Content (Join-Path $ScriptRoot "src\PartyToolsPanel.cs") -Raw
if ($releasePolicySource -notmatch 'SuiteEscapeAuthority\.ExplicitCloseControls') { throw "Party Tools Escape guard failed: explicit-controls Hub state missing." }
if ($releasePolicySource -notmatch 'if\s*\(!hubPresent\)\s*return\s+SuiteEscapeAuthority\.StandaloneFallback') { throw "Party Tools Escape guard failed: standalone fallback is not restricted to Hub absence." }
if ($suiteUiSource -notmatch 'SuiteHubPresencePolicy\.FromEndpoint\(true, payload\)') { throw "Party Tools Escape guard failed: live malformed/faulting Hub endpoint could be mistaken for absence." }
if ($panelSource -notmatch 'SuiteUiPolicy\.IsHubPresent\(\)') { throw "Party Tools Escape guard failed: panel does not gate local polling on Hub presence." }
Write-Host "Party Tools release Escape/source guards: PASS" -ForegroundColor Green

$dragSource = Get-Content (Join-Path $ScriptRoot "src\PartyToolsDragGuard.cs") -Raw
$cameraSource = Get-Content (Join-Path $ScriptRoot "src\PartyToolsCameraUiPatch.cs") -Raw
if ($dragSource -notmatch 'IPointerDownHandler' -or $dragSource -notmatch 'InputButton\.Left' -or
    $dragSource -notmatch 'Input\.GetMouseButton\(0\)' -or $dragSource -notmatch 'OnApplicationFocus' -or
    $dragSource -notmatch 'OnApplicationPause' -or $dragSource -match 'DraggingUIElement\s*=\s*false') {
    throw "Party Tools RC drag guard failed: safe left-only ownership lifecycle regressed."
}
if ($dragSource -notmatch 'ProcessOwnersKey' -or $dragSource -notmatch 'RestoreBaseline') {
    throw "Party Tools RC drag guard failed: cross-mod baseline restoration missing."
}
if ($cameraSource -notmatch '\[HarmonyPatch\(typeof\(CameraController\),\s*"UsingUI"\)\]' -or
    $cameraSource -notmatch '\[HarmonyPrepare\]' -or $cameraSource -notmatch 'if\s*\(!__result\s*&&\s*PartyToolsDragGuard\.OwnsPointerGesture\)') {
    throw "Party Tools camera guard failed: fail-closed monotonic UsingUI postfix missing."
}
foreach ($token in @('UIWindows','activeSelf','ModernControls','releaseMouse','GetAxis','DraggingUIElement')) {
    if ($cameraSource -notmatch [regex]::Escape($token)) { throw "Party Tools camera guard failed: native proof token missing: $token" }
}
if ($partyPluginSource -notmatch 'PluginVersion\s*=\s*"0\.1\.8"' -or
    $partyPluginSource -notmatch 'Party Tools " \+ PluginVersion \+ " loaded') {
    throw "Party Tools RC version guard failed."
}
Write-Host "Party Tools RC camera/gesture source guards: PASS" -ForegroundColor Green
$launcherVisual = Get-Content (Join-Path $ScriptRoot "src\StandaloneLauncherVisual.cs") -Raw
if ($launcherVisual -notmatch 'Width\s*=\s*154f' -or $launcherVisual -notmatch 'Height\s*=\s*32f' -or
    $launcherVisual -notmatch 'GripWidth\s*=\s*20f' -or $launcherVisual -notmatch '"GripDot"' -or
    $panelSource -notmatch 'StyleGrip\(grip\)' -or $panelSource -notmatch '"PARTY TOOLS"') {
    throw "Party Tools Forgotten Roads launcher visual contract failed."
}
Write-Host "Party Tools Forgotten Roads launcher visual contract: PASS" -ForegroundColor Green

# Canonical Forgotten Roads collapse/header chrome guards. The collapse icon must remain a drawn
# retained-uGUI chevron and the existing robust drag/camera ownership must not be replaced.
$panelSource = Get-Content (Join-Path $ScriptRoot "src\PartyToolsPanel.cs") -Raw
$geometrySource = Get-Content (Join-Path $ScriptRoot "src\PartyToolsUiGeometry.cs") -Raw
if ($panelSource.Contains([char]0x25B2) -or $panelSource.Contains([char]0x25BC) -or $panelSource.Contains([char]0x25BE) -or $panelSource.Contains([char]0x25B8)) {
    throw "Party Tools collapse guard failed: Unicode arrow glyph dependency introduced."
}
if ($panelSource -notmatch 'AddVerticalChevron\(_collapseChevron,\s*true\)' -or
    $panelSource -notmatch 'AddVerticalChevron\(_collapseChevron,\s*!_collapsed\)') {
    throw "Party Tools collapse guard failed: retained graphic chevron is missing."
}
if ($panelSource -notmatch 'MakeRect\("Collapse",\s*_header,\s*28f,\s*24f,\s*4f,\s*4f\)' -or
    $panelSource -notmatch 'MakeRect\("Header Drag Surface",\s*_header,\s*Width\s*-\s*72f,\s*PartyToolsUiGeometry\.HeaderHeight,\s*36f') {
    throw "Party Tools header guard failed: arrow-left-of-title geometry regressed."
}
if ($panelSource -notmatch 'SetBodyVisible\(false\)' -or
    $panelSource -notmatch 'CollapseFromExpanded' -or
    $panelSource -notmatch 'ExpandFromCollapsed') {
    throw "Party Tools collapse guard failed: header-only body/geometry transition missing."
}
if ($geometrySource -notmatch 'CollapsedHeight\s*=\s*HeaderHeight' -or
    $geometrySource -notmatch 'HeaderHeight\s*=\s*32f') {
    throw "Party Tools collapse guard failed: canonical collapsed/header height missing."
}
if ($panelSource -notmatch 'PartyToolsDragGuard' -or
    $panelSource -notmatch 'drag\.Target\s*=\s*_panel') {
    throw "Party Tools input guard failed: proven drag owner is no longer attached to the header."
}
Write-Host "Party Tools canonical collapse/header chrome: PASS" -ForegroundColor Green

# Friends Online semantic guards: native membership is authority; Party Tools owns deterministic
# roleplay availability and must not regress to native online/grouped state or an AI dependency.
$friendAvailabilitySource = Get-Content (Join-Path $ScriptRoot "src\FriendAvailability.cs") -Raw
$friendRosterSource = Get-Content (Join-Path $ScriptRoot "src\PartyStateReader.cs") -Raw
$friendApiSource = Get-Content (Join-Path $ScriptRoot "src\PartyToolsFriendAvailabilityApi.cs") -Raw
if ($friendRosterSource -notmatch 'tracking\.FriendedBy' -or $friendRosterSource -notmatch 'tracking\.IsGMCharacter' -or
    $friendRosterSource -notmatch 'if\s*\(!isFriend\)\s*continue') {
    throw "Party Tools Friends Online guard failed: native Friends roster is not the candidate authority."
}
if ($friendRosterSource -match 'tracking\.online' -or $friendRosterSource -match 'tracking\.Grouped') {
    throw "Party Tools Friends Online guard failed: native observable availability leaked back into roleplay availability."
}
if ($friendRosterSource -notmatch 'SceneManager\.GetActiveScene\(\)' -or
    $friendRosterSource -notmatch 'sim\.gameObject\.scene\.handle\s*==\s*activeScene\.handle') {
    throw "Party Tools Friends Online presence guard failed: active-zone proof is missing."
}
foreach ($token in @('EpochHours = 4','OnlinePercent = 65','DeterministicSalt','TryComposeCharacterKey','TryComposeFriendKey','ApplyObservedPresence')) {
    if ($friendAvailabilitySource -notmatch [regex]::Escape($token)) { throw "Party Tools Friends Online deterministic model guard failed: missing $token" }
}
foreach ($forbidden in @('DeepSims','Ollama','HttpClient','UnityWebRequest','System.Random','new Random')) {
    if (($friendAvailabilitySource + $friendRosterSource + $friendApiSource) -match [regex]::Escape($forbidden)) {
        throw "Party Tools Friends Online dependency guard failed: forbidden token $forbidden"
    }
}
if ($friendApiSource -notmatch 'ContractVersion\s*=\s*2' -or $friendApiSource -notmatch 'IsAvailable' -or
    $friendApiSource -notmatch 'GetFriendAvailability' -or $friendApiSource -notmatch 'GetBaseAvailabilityAtUtc' -or
    $friendApiSource -notmatch 'GetBaseAvailabilitySnapshotAtUtc') {
    throw "Party Tools Friends Online API guard failed."
}
if ($friendRosterSource -notmatch 'BuildBaseFriendAvailabilitySnapshotAtUtc' -or
    $friendRosterSource -notmatch 'TryGetBaseStateAtUtc' -or
    $friendRosterSource -notmatch 'Current party and' -or
    $friendRosterSource -notmatch 'do not rewrite history') {
    throw "Party Tools historical availability guard failed."
}
Write-Host "Party Tools Friends Online roleplay-availability source guards: PASS" -ForegroundColor Green

# Retained-UI source contract (tests/verify_retained_ui_source.py). Run it whenever Python is
# available so the contract cannot silently rot; skip with a warning otherwise rather than making
# Python a hard prerequisite for the C# suites above.
$retainedUiVerifier = Join-Path $ScriptRoot "tests\verify_retained_ui_source.py"
$python = (Get-Command python -ErrorAction SilentlyContinue)
if (-not $python) { $python = (Get-Command py -ErrorAction SilentlyContinue) }
if ($python) {
    & $python.Source $retainedUiVerifier
    if ($LASTEXITCODE -ne 0) { throw "Retained UI source contract failed." }
    Write-Host "Party Tools retained UI source contract: PASS" -ForegroundColor Green
} else {
    Write-Warning "Python not found; skipped tests\verify_retained_ui_source.py."
}
