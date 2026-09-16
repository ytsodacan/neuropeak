# NeuroPeak

A BepInEx plugin that connects [PEAK](https://store.steampowered.com/app/3527290/PEAK/)
to the [Neuro Game SDK](https://github.com/VedalAI/neuro-sdk), so Neuro can climb
the mountain herself: she gets movement and climbing actions, a running
description of what is around her, and — optionally — a seat in the game's voice
chat.

yes this massive ass readme is made by claude

BepInGUID: `com.sillyprootsoda.neuropeak`

---

## Contents

- [How it is put together](#how-it-is-put-together)
- [What has been verified in game](#what-has-been-verified-in-game)
- [Building and running on macOS](#building-and-running-on-macos)
- [Installing BepInEx for PEAK](#installing-bepinex-for-peak)
- [Getting the game assemblies](#getting-the-game-assemblies)
- [Building](#building)
- [Pointing the SDK at a test backend](#pointing-the-sdk-at-a-test-backend)
- [Actions](#actions)
- [Perception](#perception)
- [Voice chat](#voice-chat)
- [Harmony patches and how to re-target them](#harmony-patches-and-how-to-re-target-them)
- [Decompiling Assembly-CSharp.dll yourself](#decompiling-assembly-csharpdll-yourself)
- [Known version sensitivities](#known-version-sensitivities)
- [Configuration](#configuration)

---

## How it is put together

```
src/NeuroPeak/
├── Plugin.cs                      BepInEx entry point, SDK init, Harmony install
├── NeuroPeakConfig.cs             BepInEx config entries
├── Core/
│   ├── NeuroPeakRuntime.cs        Creates the DontDestroyOnLoad runtime object
│   ├── MainThreadCommandQueue.cs  Thread-safe queue drained every frame
│   ├── PeakStateTracker.cs        Publishes an immutable player snapshot per frame
│   ├── PeakPlayerState.cs         That snapshot, plus derived validity rules
│   ├── PeakIntentDriver.cs        Holds Neuro's current input intent
│   ├── PeakSurfaceProbe.cs        Raycasts for climbable geometry and drops
│   ├── RelativePosition.cs        Turns world offsets into "ahead and to your left, 4 m"
│   ├── MoveDirection.cs           The move enum and its input vectors
│   └── NeuroContext.cs            Safe wrapper around Context.Send
├── Actions/                       move, jump, grab, release, sprint + registration
├── Perception/                    Scene description, change detection, urgent events
├── Patches/                       Harmony patches, with every target in one file
└── Voice/                         Voice chat bridge and transmit sinks
```

### The control loop

PEAK samples player input once per frame in `CharacterMovement.Update`, which calls
`CharacterInput.Sample(bool playerMovementActive)` for the local character only.
Rather than simulating key presses or writing to the Unity Input System, the plugin
puts a Harmony postfix on `CharacterInput.Sample` and overwrites the freshly sampled
fields on the local character's `CharacterInput`. Everything downstream — jumping,
climbing, stamina drain, animation, Photon replication — then behaves exactly as it
does for a human player, because it is reading the same fields it always reads.

The chain is:

```
Neuro → websocket → NeuroAction.Validate  (validated against the frame's state snapshot)
                  → action/result sent
                  → NeuroAction.Execute   → MainThreadCommandQueue.Enqueue(...)
                  → drained in Update     → PeakIntentDriver mutates its intent
                  → CharacterInput.Sample postfix → applies the intent to real input
```

`MainThreadCommandQueue` exists because websocket callbacks are documented to run on
other threads. Even though the SDK currently dispatches incoming actions through a
coroutine on the main thread, nothing in the protocol guarantees that, and Unity
throws if you touch the scene graph from a worker thread. Queue first, act on the
main thread.

Validation reads `PeakStateTracker.Current`, an immutable snapshot published once per
frame with `Volatile.Write`. This makes `Validate` lock-free and safe to call from any
thread, and means the answer Neuro gets ("you can't jump, you're mid-air") describes
the same frame for every check in that action.

### Why there are no action forces

`API/README.md` warns that action forces are the main source of race conditions, and
`API/BEST_PRACTICES.md` says to force only when the game is *blocked waiting* on a
decision, and to keep the registered action set stable. PEAK is real time and never
blocks on anyone, so this integration:

- registers all five actions **once** at startup and leaves them registered, so Neuro
  never pays the latency of a register/unregister cycle mid-climb;
- sends **no** `actions/force` at all, which removes the race entirely;
- makes the *effects* one-shot instead: every action resolves to an intent in
  `PeakIntentDriver` that expires on its own (`move` after its duration, `grab` after
  its hold, `jump` and `release` after a single frame). Nothing latches on forever if
  Neuro stops sending actions.

Urgency is communicated with non-silent context messages instead, which lets her react
without the protocol hazard of a force she might already be mid-answer to.

Action results are returned from `Validate`, before `Execute` runs, which is what the
spec asks for: validate, answer immediately, then do the work.

---

## What has been verified in game

Tested against **PEAK 2.4.c** (app 3527290, depot 3527291, manifest
6303598190232800158), running under CrossOver on macOS 26 / Apple Silicon.

Confirmed working:

| Behaviour | Evidence |
| --- | --- |
| BepInEx loads the plugin | `Loading [Neuro PEAK 0.1.0]` in `LogOutput.log` |
| All seven Harmony patches apply | No `Could not find the PEAK method` warnings on any launch |
| Photon Voice transmit sink installs | `Voice transmit sink set to "photon-voice"` |
| Websocket connects and registers | Randy received `startup` then `actions/register` with all five actions |
| Schemas serialize as intended | `move` required `[direction]`, `grab` required `[]`, enums and ranges intact |
| Clean shutdown | `actions/unregister` for all five, then socket close, on quitting the game |
| No managed exceptions | Zero `NullReferenceException` / `Exception:` across repeated launches |

Not yet verified, because it needs working mouse input to start a run:

- Actions actually moving the character (the `CharacterInput.Sample` postfix)
- Perception messages from a live run
- Anything in the voice chat path beyond the sink registering

### PEAK runs on Unity 6, not Unity 2022.3

`Initialize engine version: 6000.3.15f1`. Older PEAK builds were Unity 2022.3, and
much documentation still says so. The plugin works on Unity 6 regardless: it compiles
against the game's own `Assembly-CSharp.dll`, and the `UnityEngine.Modules` package
reference only supplies compile-time API surface that has not changed. If you prefer
an exact match, raise that `PackageReference` to a 6000.x version.

### Wine and CrossOver limitations, none of them plugin bugs

- **Mouse input does not work.** The Unity log shows
  `EnableMouseInPointer failed with the following error: Call not implemented.` — that
  Win32 API is a stub in Wine, so the game renders but does not respond to clicks. A
  gamepad avoids this path entirely. Windowed mode (`-screen-fullscreen 0`) or a Wine
  virtual desktop are the usual workarounds.
- **D3D12 crashes on startup.** `GfxDevice: creating device client` followed by
  `Crash!!!`, with a stack entirely in `unityplayer` and `ntdll` and no managed frames.
  Add `-force-vulkan` to Steam's launch options. `-force-d3d11` is the other candidate.
- **BepInEx logs `Unable to start Unity log writer`.** Harmless. It means SDK messages
  written with `Debug.Log` land in Unity's `Player.log` rather than
  `BepInEx/LogOutput.log`, so check both when debugging. Under CrossOver, `Player.log`
  is at
  `<bottle>/drive_c/users/crossover/AppData/LocalLow/LandCrab/PEAK/Player.log`.

### Setting NEURO_SDK_WS_URL so it survives a GUI launch

Exporting the variable in a shell only reaches the game if the whole chain — bottle,
Steam, then PEAK — was launched from that shell. Launching Steam from CrossOver's own
window silently loses it, and the SDK then logs
`Could not retrieve websocket URL` and gives up without retrying.

Setting it in the bottle's registry instead works however the game is started, because
the SDK falls back to `EnvironmentVariableTarget.User`, which Mono reads from
`HKCU\Environment`:

```bash
/Applications/CrossOver.app/Contents/SharedSupport/CrossOver/bin/cxstart --bottle "Steam" -- reg add 'HKCU\Environment' /v NEURO_SDK_WS_URL /d "ws://localhost:8000" /f
```

This was confirmed to work: the error disappeared and the game connected to Randy on
the next launch. Restart the game after changing it, since the URL is only read at
startup.

---

## Building and running on macOS

**PEAK is a Windows-only title.** Steam reports `windows: true, mac: false,
linux: false`, so there is no native macOS build and Steam on a Mac will not offer to
install it. This affects getting the game assemblies and running the game. It does
**not** affect building the plugin, which compiles natively on Apple Silicon.

### Building

Nothing special is required. The .NET SDK builds `net472` on macOS using the
`Microsoft.NETFramework.ReferenceAssemblies` package, which the project already
references, so no Mono and no Windows machine is involved:

```bash
dotnet restore
```

```bash
dotnet build src/NeuroPeak/NeuroPeak.csproj -c Release
```

You still need the game assemblies in `lib/` first, or the build stops with
`NEUROPEAK001`.

### The CrossOver route (recommended)

Installing PEAK inside a CrossOver bottle solves both problems at once: you get a
playable game *and* the assemblies to build against, from the same install.

1. Install CrossOver, create a Windows 10 bottle, and install **Steam** into it from
   CrossOver's software list.
2. Log in to Steam inside the bottle and install PEAK as normal.
3. Copy the assemblies out of the bottle into `lib/`, adjusting the bottle name:

   ```bash
   export PEAK_MANAGED="$HOME/Library/Application Support/CrossOver/Bottles/YOUR_BOTTLE/drive_c/Program Files (x86)/Steam/steamapps/common/PEAK/PEAK_Data/Managed"
   ```

   ```bash
   cp "$PEAK_MANAGED"/{Assembly-CSharp,PhotonUnityNetworking,PhotonRealtime,Photon3Unity3D,Zorro.Core.Runtime,PhotonVoice.API,PhotonVoice}.dll lib/
   ```

4. Build, and copy the result into the bottle's plugin folder:

   ```bash
   dotnet build src/NeuroPeak/NeuroPeak.csproj -c Release
   ```

CrossOver bottles live at `~/Library/Application Support/CrossOver/Bottles/<name>/`,
and the game ends up at
`<bottle>/drive_c/Program Files (x86)/Steam/steamapps/common/PEAK/`. Everything in
[Installing BepInEx for PEAK](#installing-bepinex-for-peak) is relative to that
folder.

### Getting the assemblies without installing the game (verified working)

If Steam inside CrossOver is being difficult, you do not need it at all to *build*.
Pull the Windows depot directly with **DepotDownloader** — it is native arm64,
self-contained (no .NET runtime needed), and with a file list it fetches ~16 MB
instead of the whole game.

```bash
curl -LO https://github.com/SteamRE/DepotDownloader/releases/download/DepotDownloader_3.4.0/DepotDownloader-macos-arm64.zip
```

```bash
unzip DepotDownloader-macos-arm64.zip -d tools/depotdownloader && chmod +x tools/depotdownloader/DepotDownloader
```

macOS quarantines unsigned downloads, so clear it or Gatekeeper kills the binary:

```bash
xattr -dr com.apple.quarantine tools/depotdownloader
```

Restrict the download to the managed assemblies. The regex tolerates both path
separators, because Windows depot manifests use backslashes:

```bash
printf 'regex:.*PEAK_Data[\\\\/]Managed[\\\\/].*\\.dll$\n' > tools/managed-only.txt
```

Then download. **`-qr` logs you in by scanning a QR code with the Steam mobile app**,
so no password is typed anywhere:

```bash
./tools/depotdownloader/DepotDownloader -app 3527290 -os windows -osarch 64 -qr -dir ./peak-windows -filelist tools/managed-only.txt
```

The QR code refreshes every ~30 seconds — scan whichever one is at the bottom of the
terminal. A `Retrying Steam3 connection (TryAnotherCM)...` line partway through is
normal; it reconnects on its own. Afterwards `-username <you> -remember-password`
works instead of `-qr`.

Then copy the assemblies into `lib/`:

```bash
cp peak-windows/PEAK_Data/Managed/{Assembly-CSharp,PhotonUnityNetworking,PhotonRealtime,Photon3Unity3D,Zorro.Core.Runtime,PhotonVoice.API,PhotonVoice}.dll lib/
```

**SteamCMD** works too, but it is x86_64 (so it runs under Rosetta) and downloads the
whole game:

```bash
brew install steamcmd
```

```bash
steamcmd +@sSteamCmdForcePlatformType windows +force_install_dir ~/peak-windows +login YOUR_STEAM_NAME +app_update 3527290 validate +quit
```

The `+@sSteamCmdForcePlatformType windows` argument must come *before* `+login`, and
`+force_install_dir` *before* `+app_update`.

### Actually running it

Two things differ from a plain Windows install, and both fail silently if you miss
them:

- **BepInEx needs a DLL override under Wine.** BepInEx 5 loads through
  `winhttp.dll`, and Wine uses its own builtin unless told otherwise. In CrossOver:
  select the bottle, open *Wine Configuration* from the bottle's advanced menu, go to
  the *Libraries* tab, add `winhttp`, and set it to *Native then Builtin*. Without
  this, BepInEx never loads and `BepInEx/LogOutput.log` is never created — which looks
  exactly like a broken plugin.
- **`NEURO_SDK_WS_URL` is read from the process environment.** Wine passes the host
  environment through, so the most reliable way to set it is to launch the bottle from
  a shell that already has it exported:

  ```bash
  export NEURO_SDK_WS_URL=ws://localhost:8000
  ```

  ```bash
  "/Applications/CrossOver.app/Contents/SharedSupport/CrossOver/bin/cxstart" --bottle "YOUR_BOTTLE" --wait-children "C:\\Program Files (x86)\\Steam\\Steam.exe"
  ```

  Randy runs natively on the Mac, so the game reaching `ws://localhost:8000` through
  Wine is an ordinary loopback connection and needs no extra setup.

---

## Installing BepInEx for PEAK

1. Install PEAK through Steam.
2. Install the **[BepInExPack PEAK](https://thunderstore.io/c/peak/p/BepInEx/BepInExPack_PEAK/)**
   Thunderstore package. Either:
   - use a mod manager (r2modman or the Thunderstore app), pick PEAK, install
     `BepInEx-BepInExPack_PEAK`, and launch through the manager; or
   - download the zip, and copy the **contents of its `BepInExPack_PEAK` folder**
     (`BepInEx/`, `doorstop_config.ini`, `winhttp.dll`, `.doorstop_version`) into the
     PEAK game folder next to `PEAK.exe`.
3. Launch the game once. BepInEx generates `BepInEx/config` and
   `BepInEx/LogOutput.log`. If that log exists, the loader is working.
4. Copy `NeuroPeak.dll` into `BepInEx/plugins/`.

The pack is BepInEx 5.4.x (the current Thunderstore build reports `5.4.75301`), which
is why this project compiles against `BepInEx.Core` 5.4.21 and HarmonyX 2.10.2. Those
are the reference assemblies; the loader in the game supplies the real ones at runtime.

If you launch through Steam rather than a mod manager, set the launch option so the
websocket URL is visible to the game process — see
[Pointing the SDK at a test backend](#pointing-the-sdk-at-a-test-backend).

---

## Getting the game assemblies

PEAK's assemblies are not redistributed here and are never downloaded by the build.
Copy them yourself from:

```
<Steam>/steamapps/common/PEAK/PEAK_Data/Managed/
```

On macOS there is no native install to copy from — see
[Building and running on macOS](#building-and-running-on-macos) for how to pull the
Windows depot down purely to compile against.

into `lib/`:

| File | Why |
| --- | --- |
| `Assembly-CSharp.dll` | Every PEAK type the plugin touches |
| `PhotonUnityNetworking.dll` | `Character` derives from `MonoBehaviourPun`, so the compiler must be able to walk its base chain |
| `PhotonRealtime.dll` | `CharacterData` derives from `MonoBehaviourPunCallbacks`, whose interfaces live here |
| `Photon3Unity3D.dll` | Transitively referenced by the two above |
| `Zorro.Core.Runtime.dll` | `MapHandler` derives from `Zorro.Core.Singleton<T>` |
| `PhotonVoice.API.dll` | Optional, enables the built-in voice transmit sink |
| `PhotonVoice.dll` | Optional, same — this is the assembly holding `Photon.Voice.Unity.Recorder` |

`lib/*.dll` is gitignored. The project references `lib/*.dll` with a glob and
deliberately **excludes** `UnityEngine*.dll`, `Unity.*.dll`, `Newtonsoft.Json.dll`,
`0Harmony*.dll`, `BepInEx*.dll` and the BCL assemblies, because those come from NuGet —
copying them into `lib/` as well would give you duplicate-assembly errors.

The build fails with a clear `NEUROPEAK001` error if `lib/Assembly-CSharp.dll` is
missing.

---

## Building

Requirements: the .NET SDK (8 or newer). On macOS and Linux the .NET Framework 4.7.2
reference assemblies come from the `Microsoft.NETFramework.ReferenceAssemblies` package,
so no Windows or Mono install is needed.

Clone the Neuro SDK alongside this repository. It is not vendored here, and two things
need it: the voice chat sources (which the published NuGet package does not yet ship,
see [Voice chat](#voice-chat)) and Randy, the test backend.

```bash
git clone https://github.com/VedalAI/neuro-sdk.git
```

Without it the build still succeeds — voice chat is simply compiled out.

```bash
dotnet restore
```

```bash
dotnet build src/NeuroPeak/NeuroPeak.csproj -c Release
```

The output lands in `src/NeuroPeak/bin/Release/net472/NeuroPeak.dll`. Copy that one
file into `BepInEx/plugins/`.

`NuGet.config` adds the BepInEx feed (`https://nuget.bepinex.dev/v3/index.json`)
alongside nuget.org, because `BepInEx.Core` and `UnityEngine.Modules` for Unity 2022.3
are only published there.

The Neuro SDK is consumed as `VedalAI.NeuroSdk.Unity`, which ships its Unity sources
as content files rather than a DLL; they compile into `NeuroPeak.dll` together with an
embedded copy of Newtonsoft.Json that the SDK injects at runtime. That is the
modded-environment path described in `Unity/README.md`, and it is why the project also
carries a `UnityEngine.Modules` package reference: the SDK's MSBuild targets refuse to
compile without one.

### Optional build switches

| Property | Effect |
| --- | --- |
| `GameLibDirectory` | Overrides where the game assemblies are read from (default `lib/`) |
| `NeuroSdkVoiceSourceDirectory` | Where the SDK's voice-chat sources are read from (default `neuro-sdk/Unity/Assets/Voice/`) |

Two compile constants are set automatically:

- `NEUROSDK_VOICE` — set when `NeuroSdkVoiceSourceDirectory` contains
  `NeuroVoiceChat.cs`. Without it, `Voice/PeakVoiceBridge.cs` is excluded from the
  build and the plugin runs without voice chat.
- `PEAK_PHOTON_VOICE` — set when both `PhotonVoice.API.dll` and `PhotonVoice.dll`
  are in `lib/`. Without it, `Voice/PhotonVoiceTransmitSink.cs` is excluded.

All four combinations build cleanly.

---

## Pointing the SDK at a test backend

**Do this before you ever connect to the real Neuro.** The SDK reads the environment
variable `NEURO_SDK_WS_URL` (process, then user, then machine scope) and connects to
whatever is there. There is no in-game prompt and no confirmation step.

Start a test backend first:

- **Randy** — the randomised tester bundled in this repository, which picks actions at
  random. It is the fastest way to find out whether your validation and your action
  results hold up:

  ```bash
  cd neuro-sdk/Randy && npm install && npm start
  ```

  It listens on `ws://localhost:8000` by default.

- **Tony** — a manual tester where you choose the actions yourself. Good for walking
  through one climb deliberately.
- **Gary** — a local LLM-backed tester if you want something closer to real play.

Then point the game at it.

**Windows, Steam launch options** (right-click PEAK → Properties → Launch Options):

```
NEURO_SDK_WS_URL=ws://localhost:8000 %command%
```

**Windows, permanent user variable:**

```powershell
setx NEURO_SDK_WS_URL "ws://localhost:8000"
```

**r2modman / Thunderstore app:** Settings → *Set launch parameters* does not set
environment variables, so use the `setx` form above, or launch the game from a shell
that has the variable exported.

**Linux / Proton:**

```bash
NEURO_SDK_WS_URL=ws://localhost:8000 %command%
```

Confirm it worked before going further: `BepInEx/LogOutput.log` should contain
`Sending ws message {"command":"startup"...}` followed by an `actions/register` with
all five actions. If the URL is unset, the SDK logs
`Could not retrieve websocket URL` and the plugin simply does nothing.

Only change `NEURO_SDK_WS_URL` to the real endpoint once Randy has run a full climb
without the plugin throwing.

---

## Actions

| Name | Schema | Notes |
| --- | --- | --- |
| `move` | `direction`: `forward` \| `backward` \| `left` \| `right` (required), `duration`: 0.1–2.0 s (optional, default 0.5) | Relative to where she is looking. Sets `movementInput` for the duration, then stops. |
| `jump` | none | Rejected mid-air, while climbing, within 0.3 s of the last jump, with no jumps left, or out of stamina. |
| `grab` | `hold_seconds`: 0.2–20 (optional, default 4) | Rejected unless a climbable surface is actually within 1.25 m of the camera along the look direction. |
| `release` | none | Rejected if she is not holding anything. |
| `sprint` | `enabled`: boolean (required) | On the ground this runs; on a wall it triggers PEAK's climbing hop. |

These map onto PEAK's real input fields:

| Action | Fields written in the `CharacterInput.Sample` postfix |
| --- | --- |
| `move` | `movementInput` |
| `jump` | `jumpWasPressed`, `jumpIsPressed` |
| `grab` | `usePrimaryIsPressed` (+ `usePrimaryWasPressed` on the first frame) |
| `release` | `usePrimaryWasReleased` |
| `sprint` | `sprintIsPressed` (+ `sprintWasPressed` on the first frame) |

Grabbing is `usePrimary` because `Character.FixedUpdate` zeroes `data.sincePressClimb`
while `usePrimaryIsPressed` is held and no item is equipped, and
`CharacterClimbing.TryToStartWallClimb` only latches on when `sincePressClimb < 0.1`.
Releasing is `usePrimaryWasReleased` for the same reason: that is the condition
`CharacterClimbing.Update` checks before sending `StopClimbingRpc`. The
`sprintWasPressed` pulse matters on the wall, because PEAK requires sprint to have been
*pressed since the climb started* before it will let you climb-jump.

The intent only overrides input while it is live, and the postfix respects the
`playerMovementActive` argument, so a human at the keyboard still has control between
Neuro's commands and while a menu is open. Setting `MovementEnabled` to `false` in the
config disables the override entirely.

### Validation

Every action is validated against the real player state before the result is sent, and
failures come back as actionable messages, as `BEST_PRACTICES.md` asks. For example
`jump` checks `jumpsRemaining`, `sinceGrounded <= 0.2`, `sinceJump >= 0.3`,
`fullyConscious`, that she is not on a wall or a climb handle, and that she has
stamina — the same conditions `CharacterMovement.TryToJump` and `Character.CheckJump`
use. `grab` runs the raycast itself and mirrors PEAK's grab-angle rule (the surface
normal must sit between 50° and 170° from world up) so that "there is nothing
climbable within reach" is a true statement rather than a guess.

---

## Perception

The Neuro API is text only — there is no image channel — so `EnvironmentReporter`
turns the scene into prose.

It scans every `ScanInterval` (0.4 s by default) but **does not send on a timer**.
Each scan produces a coarse digest: segment, altitude bucketed to 10 m, posture
(ground / air / climbing / rope / vine / handle), stamina bucketed to 20 %, drop below
bucketed to 10 m, fog, injury bucketed to 25 %, and the list of nearby surfaces and
teammates. A message is only sent when that digest changes *and* at least
`AmbientMinInterval` (6 s) has passed. Standing still produces nothing;
`BEST_PRACTICES.md` is explicit that a stream of small updates makes her play worse.

Ambient descriptions go out with `silent: true`. A typical one:

```
## Around you
You are hanging off a rock face at about 214 m up in the Tropics, 31 m above the ground below you.
Stamina is at 45%.
Climbable from here:
- rock face in reach ahead, level with you, under a metre
- root ahead and to your right, 3 m above, 4 m
Careful: a 31 m drop straight below you.
Your team:
- Vedal is climbing, behind and to your left, 2 m below, 6 m
```

Things that need a reaction go out with `silent: false`, each rate-limited per kind by
`UrgentContext` so a bad situation cannot spam her:

| Event | Source |
| --- | --- |
| Fall damage taken | Harmony pre/postfix around `CharacterMovement.CheckFallDamage`, reporting the actual injury delta and the airtime |
| Stamina depleted | Postfix on `Character.UseStamina`, fired on the transition to empty, with different wording on the wall than on the ground |
| Checkpoint reached | Postfix on `Campfire.Light_Rpc` |
| Imminent fall | Airborne over 0.6 s, falling faster than 7 m/s, with more than `DangerousDropMeters` of air below |
| Grip failing | Holding on with stamina under `LowStaminaFraction` |
| Teammate down | Any other `Character` that is passed out, fully passed out or dead |

Positions are always relative and in plain words — `RelativePosition` projects the
offset onto her own look direction and produces "ahead and to your left, 3 m above,
6 m" rather than coordinates.

New areas are announced from a postfix on `MapHandler.GoToSegment`.

---

## Voice chat

This is the part with real external dependencies, so read this section before
expecting it to work.

### What the SDK gives you

`API/VOICE_CHAT.md` describes a second websocket at `/game/<name>/voice` carrying
48 kHz mono Float32 PCM in both directions, with per-speaker tagging upstream so Neuro
hears *who* said what. The Unity SDK wraps all of that in
`NeuroSdk.Voice.NeuroVoiceChat` (`Connect`, `RegisterSpeaker`, `SendSpeakerAudio`,
`UnregisterSpeaker`, `onAudioReceived`, `onSpeakingChanged`, `onCancelled`,
`onUnavailable`), including URL derivation, the handshake, and resampling to the wire
format.

**`NeuroVoiceChat` is not in the published `VedalAI.NeuroSdk.Unity` 2.0.0 NuGet
package.** It exists on `main` in the SDK repository but the released package predates
it. So the build compiles the SDK's voice sources straight out of the clone at
`neuro-sdk/Unity/Assets/Voice/`, and defines `NEUROSDK_VOICE`. If that folder is
absent the voice bridge is dropped from the build and the rest of the plugin is
unaffected, which is what the spec requires: "your game must work fully without it".
When a package version ships `Voice/`, point `NeuroSdkVoiceSourceDirectory` at an empty
directory to avoid duplicate types.

### Upstream: PEAK → Neuro (implemented)

PEAK uses Photon Voice. Each remote player's voice is played through an `AudioSource`
on their `CharacterVoiceHandler`, which is exactly the per-player, pre-mix stream the
voice API wants.

`PeakVoiceBridge` walks `Character.AllCharacters` once a second, and for every remote
character registers a speaker under their `characterName` and attaches a
`PeakVoiceCapture` to that `AudioSource`'s GameObject. `PeakVoiceCapture` implements
`OnAudioFilterRead`, a plain Unity callback, so no PEAK or Photon internals are
guessed at. It gates on a simple voice-activity threshold (so silence is not streamed,
as the spec asks), copies the buffer, and hands it to a `ConcurrentQueue` — the audio
thread must not touch the SDK. The bridge drains that queue in `Update` and calls
`SendSpeakerAudio`, which resamples and downmixes for the wire. Speakers are
unregistered when players leave. The local character is never registered, so Neuro
never hears herself.

If audio never arrives, the likely cause is that Unity did not rebuild the DSP filter
chain when the component was added at runtime; attaching `PeakVoiceCapture` earlier
(from a patch on `CharacterVoiceHandler.Start`) fixes that.

### Downstream: Neuro → PEAK (isolated integration point)

Neuro's voice arrives as 48 kHz mono PCM through `onAudioReceived`, and
`onSpeakingChanged` / `onCancelled` tell you when to key and release push-to-talk. It
must be fed into PEAK's voice **transmit** path, never played locally.

That last hop is behind `IPeakVoiceTransmitSink`, registered through
`PeakVoiceTransmitRegistry`. Two implementations ship:

- **`BufferedVoiceTransmitSink`** (default): keeps Neuro's audio in a bounded ring
  buffer and logs once that nothing is consuming it. This is the deliberately inert
  fallback, and the seam to write your own integration against.
- **`PhotonVoiceTransmitSink`**: compiled only when `PhotonVoice.API.dll` and
  `PhotonVoice.dll` are in `lib/`. It finds the local character's
  `Photon.Voice.Unity.Recorder`, sets `SourceType = InputSourceType.Factory` and
  `InputFactory = () => this`, implements `Photon.Voice.IAudioReader<float>` reading
  out of the ring buffer, calls `RestartRecording(true)`, and keys `TransmitEnabled`
  from `onSpeakingChanged`. This is Photon Voice's own documented custom-source API,
  not a guess about PEAK.

What has **not** been verified against a running game is how PEAK configures its
`Recorder` — whether it overrides `SourceType` on its own, and how it interacts with
the game's push-to-talk setting. Treat `PhotonVoiceTransmitSink` as the plausible
implementation to test first, not as something known to work. To substitute your own:

```csharp
PeakVoiceTransmitRegistry.Register(new MyVoiceSink());
```

---

## Harmony patches and how to re-target them

Every patch target is resolved in **one file**, `Patches/PeakPatchTargets.cs`:

| Method | Target | Visibility | Purpose |
| --- | --- | --- | --- |
| `InputSampled` | `CharacterInput.Sample(bool)` | internal | Inject Neuro's input |
| `FallDamageEvaluated` | `CharacterMovement.CheckFallDamage()` | private | Fall damage context |
| `StaminaSpent` | `Character.UseStamina(float, bool, bool)` | internal | Stamina depleted context |
| `CheckpointLit` | `Campfire.Light_Rpc(bool, float)` | private | Checkpoint context |
| `SegmentAdvanced` | `MapHandler.GoToSegment(Segment)` | public | New area context |
| `ClimbStarted` | `CharacterClimbing.StartClimbRpc(Vector3, Vector3)` | private | Available, unused |
| `ClimbStopped` | `CharacterClimbing.StopClimbingRpc(float)` | public | Available, unused |

Most of these are `private` or `internal` in PEAK; `AccessTools` resolves them
regardless. Visibility and signatures above were read out of the shipped
`Assembly-CSharp.dll` by reflection, not guessed.

`PeakPatchInstaller` patches manually rather than with attributes, so a target that a
game update renames or removes produces a **warning and a disabled hook**, not a crash
on load:

```
Could not find the PEAK method for fall damage. That hook is disabled.
Update PeakPatchTargets after decompiling the current Assembly-CSharp.dll.
```

To re-target after an update, change only `PeakPatchTargets.cs`. Patch bodies never
name a method by string.

Patch bodies also avoid PEAK's private fields: `CharacterMovement.character` and
`CharacterClimbing.character` are private, so `PeakPatchContext` resolves the owning
`Character` with `GetComponentInParent<Character>()` instead, which does not break when
a field is renamed.

---

## Decompiling Assembly-CSharp.dll yourself

Every PEAK type this plugin uses was read out of a decompile — there are **no
placeholder types** in the source. Re-do this after any game update.

1. Get a decompiler:
   - **[ILSpy](https://github.com/icsharpcode/ILSpy)** (Windows GUI, or `ilspycmd`
     cross-platform via `dotnet tool install -g ilspycmd`)
   - **[dnSpy](https://github.com/dnSpyEx/dnSpy)** (Windows, also debugs the live game)
   - **[dotPeek](https://www.jetbrains.com/decompiler/)** (Windows, free)
2. Open `<Steam>/steamapps/common/PEAK/PEAK_Data/Managed/Assembly-CSharp.dll`.
   Add the whole `Managed` folder to the assembly list so Photon, Zorro and Unity
   types resolve.
3. Export the whole thing to a folder so you can grep it:

   ```bash
   ilspycmd -p -o ./peak-decompiled "<Steam>/steamapps/common/PEAK/PEAK_Data/Managed/Assembly-CSharp.dll"
   ```

4. The classes that matter, all in the **global namespace**:

   | Type | What to look at |
   | --- | --- |
   | `Character` | `localCharacter`, `AllCharacters`, `IsLocal`, `characterName`, `input`, `data`, `refs`, `GetMaxStamina`, `UseStamina`, `CheckJump`, `CheckSprint`, `Fall` |
   | `Character.CharacterRefs` | `movement`, `climbing`, `afflictions`, `stats`, `head`, `ragdoll`, `items`, `view` |
   | `CharacterData` | `isGrounded`, `sinceGrounded`, `sinceJump`, `jumpsRemaining`, `isClimbing`, `isRopeClimbing`, `isVineClimbing`, `currentClimbHandle`, `currentStamina`, `extraStamina`, `TotalStamina`, `outOfStaminaFor`, `fullyConscious`, `passedOut`, `fullyPassedOut`, `dead`, `isInFog`, `lookDirection`, `lookDirection_Flat`, `lookDirection_Right`, `avarageVelocity`, `sincePressClimb`, `currentItem` |
   | `CharacterInput` | `Sample(bool)`, `movementInput`, `jumpWasPressed`, `jumpIsPressed`, `usePrimaryIsPressed`, `usePrimaryWasPressed`, `usePrimaryWasReleased`, `sprintIsPressed`, `sprintWasPressed` |
   | `CharacterMovement` | `Update` (where `Sample` is called), `TryToJump`, `CheckFallDamage`, `Land` |
   | `CharacterClimbing` | `TryToStartWallClimb`, `CanClimb`, `AcceptableGrabAngle`, `StartClimbRpc`, `StopClimbingRpc`, `RPCA_ClimbJump` |
   | `CharacterAfflictions` | `STATUSTYPE`, `GetCurrentStatus`, `AddStatus`, `statusSum` |
   | `CharacterStats` | `heightInMeters`, `peakHeightInUnits` |
   | `CharacterVoiceHandler` | The `Recorder` and `AudioSource` on the same GameObject |
   | `Campfire` | `Lit`, `advanceToSegment`, `Light_Rpc` |
   | `MapHandler` | `GoToSegment`, `GetCurrentSegment`, `segments` |
   | `Segment` | The biome enum |
   | `MainCamera` | `instance` — PEAK raycasts grabs from the camera, not the head |

5. The two behaviours worth reading in full before changing anything, because the
   plugin's input mapping depends on them: `CharacterClimbing.Update` (what starts and
   stops a climb) and `Character.FixedUpdate` (where `usePrimaryIsPressed` becomes
   `sincePressClimb`).

---

## Known version sensitivities

Everything below was checked against **app 3527290, depot 3527291, manifest
6303598190232800158**. The plugin compiles against that build with 0 errors, and all
seven Harmony targets resolve against it.

Two signatures differ from older PEAK builds, and both were corrected here after
checking the real assembly rather than a decompile of an older version:

- `Character.UseStamina(float usage, bool useBonusStamina = true, bool ignoreAscents = false)`
  — three parameters now, not two.
- `Campfire.Light_Rpc(bool updateSegment, float burningFor)` — two parameters now, not
  zero. The plugin only reports a checkpoint when `updateSegment` is `true`, because
  this RPC also fires when a client restores campfire state on join, which is not a
  checkpoint being reached.

Both of these previously resolved to `null`, which `PeakPatchInstaller` would have
turned into a logged warning and a silently disabled hook. If you update PEAK and see
those warnings, this is the first thing to re-check.

Enums that grow between builds, and why the plugin does not care:

- **`CharacterAfflictions.STATUSTYPE`** in this build is `Injury, Hunger, Cold, Poison,
  Crab, Curse, Drowsy, Weight, Hot, Thorns, Spores, Web, Arrow, Petrify, FlyTrap` —
  six more than older builds had. The plugin only ever names `Injury` and otherwise
  uses `statusSum`, so new members cannot break it.
- **`Segment`** in this build is `Beach, Tropics, Alpine, Caldera, TheKiln, Peak, Void`.
  The plugin never compares against a specific member; it calls `ToString()`.
- **`MapHandler`** exposes the instance method `GetCurrentSegment()` here. The plugin
  reaches it through a cached `FindObjectOfType<MapHandler>()` rather than
  `Singleton<T>.Instance`, so a missing map handler degrades to an empty region name.
- **`CharacterData.currentStamina`** is a public property backed by a private `_stam`
  field. The plugin uses the property.

### The Newtonsoft.Json build warning is expected

The build emits one `MSB3277` warning about Newtonsoft.Json 12.0.0.0 versus 13.0.0.0.
This is the Neuro SDK package's own design, not a problem with this project: the
package references its embedded 12.0.0.0 copy *and* declares a 13.0.3 package
reference, while PEAK ships 13.0.0.0.

It is safe. `ResourceManager.InjectAssemblies` only registers an `AssemblyResolve`
*fallback* handler — it never force-loads the embedded copy. At runtime either PEAK's
Newtonsoft 13 satisfies the reference, or the fallback supplies the embedded 12. There
is no duplicate load either way. The warning is left visible rather than suppressed so
that a genuine future conflict is not hidden.

## Configuration

Written to `BepInEx/config/com.sillyprootsoda.neuropeak.cfg` on first launch.

| Section | Key | Default | Meaning |
| --- | --- | --- | --- |
| Control | `MovementEnabled` | `true` | Let Neuro drive the local player |
| Perception | `PerceptionEnabled` | `true` | Send scene descriptions |
| Perception | `ScanInterval` | `0.4` | Seconds between scans |
| Perception | `AmbientMinInterval` | `6` | Floor between two ambient messages |
| Perception | `UrgentMinInterval` | `5` | Floor between two urgent messages of the same kind |
| Perception | `NearbyScanRadius` | `7` | Metres searched for climbable surfaces |
| Perception | `DangerousDropMeters` | `12` | Drop height that counts as dangerous |
| Perception | `LowStaminaFraction` | `0.2` | Stamina fraction that triggers the grip warning |
| Voice | `VoiceChatEnabled` | `true` | Bridge PEAK voice chat to Neuro |
| Voice | `VoiceActivityThreshold` | `0.0025` | Mean sample level before a teammate's audio is forwarded |

---

## Licence and attribution

The Neuro Game SDK is MIT licensed by Vedal AI; its Unity sources are compiled into
this plugin through the NuGet package and, for the voice chat side-channel, from the
clone in `neuro-sdk/`. PEAK and its assemblies belong to Aggro Crab and Landfall and
are neither included nor downloaded here.
