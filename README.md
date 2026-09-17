# NeuroPeak

A BepInEx plugin that hooks [PEAK](https://store.steampowered.com/app/3527290/PEAK/) up
to the [Neuro Game SDK](https://github.com/VedalAI/neuro-sdk), so Neuro can climb the
mountain herself. She gets movement and climbing actions, a running description of what's
around her, and optionally a seat in the game's voice chat.

yes this massive ass readme is made by claude

BepInGUID: `com.sillyprootsoda.neuropeak`

## What she can do

| Action | Parameters |
| --- | --- |
| `move` | `direction`: forward/backward/left/right, `duration`: 0.1–2.0s |
| `look` | `direction`: left/right/up/down, `degrees`: 1–180 |
| `jump` | — |
| `grab` | `hold_seconds`: 0.2–20 |
| `release` | — |
| `sprint` | `enabled`: true/false |
| `interact` | `hold_seconds`: 0.1–10 — pick up, open chests, light campfires |
| `select_slot` | `slot`: 0–8, where 0 puts your hands away |
| `use_item` | `mode`: primary/secondary, `hold_seconds`: 0.1–15 |
| `drop_item` | — |
| `throw_item` | `charge_seconds`: 0.2–3 — lob it at what you're looking at |
| `reach` | `hold_seconds`: 0.2–10 — put a hand out to grab a teammate |
| `open_backpack` | — |
| `crouch` | `enabled`: true/false |
| `ping` | — mark what you're looking at for your team |

Everything is validated against the real game state before it runs, and failures come
back as something she can act on — "You just jumped, wait a moment before jumping again"
rather than a silent no-op.

## How it works

PEAK samples player input once a frame in `CharacterInput.Sample`. Rather than faking
key presses, the plugin puts a Harmony postfix there and writes the same fields the game
already reads. Everything downstream — jumping, climbing, stamina, animation, Photon
replication — then behaves exactly as it does for a human.

Actions arrive over a websocket, get validated against a player-state snapshot published
each frame, and are queued onto the main thread before touching anything. Each one
becomes a short-lived intent that expires on its own, so nothing latches on if she stops
sending commands.

There are no action forces. PEAK is real-time and never blocks waiting for a decision,
and forces are the main source of race conditions in the API. Urgency is handled with
non-silent context messages instead.

## What she sees

The Neuro API is text-only, so the plugin describes the scene in prose:

```
You are standing on solid ground at about 49 m up in the Alpine.
World coordinates x 124, y 87, z -302, facing north-east (48 degrees).
Stamina is down to 45%, plus 20% bonus stamina on top that does not refill once it is gone.
You are worn down enough that stamina will not refill past 82%.
What is wrong with you: Hunger 34% — eat something; Cold 12% — get warm, a campfire or a torch helps.
It is afternoon on day 2 in the Alpine, and a snowstorm is about 40 seconds away.
The gloom is rising below you, about 60 m down. It will not stop, so do not go back that way.
You are looking at Wooden Chest — you could open it. Use `interact` for that.
Climbable:
- rock face ahead and to your left, level with you, 4 m, at x 119, y 87, z -298
Nearby:
- Berry (food, restores 15% hunger) behind you, 6 m, at x 130, y 86, z -305
Danger:
- Cactus Large — touching it gives you Thorns, ahead and to your right, 5 m
- The Scoutmaster is hunting you, 18 m away, behind and to your left
The mountain rises to the north-east, which is ahead and to your right from where you
are facing — the ground is about 7 m higher 14 m that way. Head there to keep climbing.
Vedal has a hand out, 3 m away. Use `reach` to grab them.
You are holding the Passport (just your passport, no use on the mountain).
In your bag: 2: Berry (food, restores 15% hunger). You are wearing a backpack.
```

Nothing there is hardcoded knowledge about PEAK's content. Item effects come off the
prefabs' own components (`Action_RestoreHunger`, `Action_ModifyStatus` and friends), so
the numbers are the game's. Hazards are found by looking for any component carrying a
`STATUSTYPE` field — which is how PEAK implements all of them — so cacti, spores, thorns
and anything added in a future update are all picked up without naming them. The way up
is worked out by sampling ground height in eight directions around her.

In the lobby she gets a much shorter report instead, with no weather, no hazards and no
talk of climbing, because none of that applies before a run starts.

It only sends when something meaningful changes, not on a timer — she reacts badly to
being spammed. Position feeds the change detector on a coarse grid rather than raw, or
every footstep would count as news. Things that need a reaction (fall damage, stamina
running out, a checkpoint, a storm arriving, the gloom closing in, a teammate reaching
for her, something hunting her) are sent non-silently so she actually responds.

## Voice chat

Optional, and it degrades quietly if the server doesn't support it.

**Hearing other players** works. Each remote player's voice comes through its own
`AudioSource`, which the plugin taps with `OnAudioFilterRead` and forwards to Neuro
tagged with that player's name.

**Her talking back** is the part that isn't proven. Neuro's audio is handed to an
`IPeakVoiceTransmitSink`. The shipped implementation drives Photon Voice's `Recorder`
through its documented custom-source API, but how PEAK configures that recorder hasn't
been verified against a live lobby — treat it as the thing to test first, not as
something known to work. Swap in your own with:

```csharp
PeakVoiceTransmitRegistry.Register(new MyVoiceSink());
```

Note that `NeuroVoiceChat` isn't in the published SDK NuGet package yet, so the build
picks it up from a local clone of the SDK (see below). Without that clone, voice chat
compiles out and nothing else changes.

## Setup

You need the .NET SDK (8 or newer) and PEAK installed.

**1. Clone the Neuro SDK next to this repo.** The build uses it for the voice chat
sources, and it's also where Randy lives.

```bash
git clone https://github.com/VedalAI/neuro-sdk.git
```

**2. Copy PEAK's assemblies into `lib/`.** They aren't redistributed here. From
`steamapps/common/PEAK/PEAK_Data/Managed/` you need:

```
Assembly-CSharp.dll  PhotonUnityNetworking.dll  PhotonRealtime.dll
Photon3Unity3D.dll   Zorro.Core.Runtime.dll
PhotonVoice.API.dll  PhotonVoice.dll            (optional, for voice)
```

Don't copy the `UnityEngine*`, `Newtonsoft.Json` or `System*` DLLs — those come from
NuGet and you'll get duplicate-assembly errors.

**3. Build.**

```bash
dotnet build src/NeuroPeak/NeuroPeak.csproj -c Release
```

**4. Install.** Get [BepInExPack PEAK](https://thunderstore.io/c/peak/p/BepInEx/BepInExPack_PEAK/)
from Thunderstore, drop its contents next to `PEAK.exe`, run the game once, then copy
`NeuroPeak.dll` into `BepInEx/plugins/`.

**5. Point it at a test backend before you go anywhere near the real Neuro.**

Randy picks random actions and is the fastest way to find out whether your validation
holds up:

```bash
cd neuro-sdk/Randy && npm install && npm start
```

Then set the URL it listens on:

```bash
setx NEURO_SDK_WS_URL "ws://localhost:8000"
```

**Restart Steam afterwards.** The game inherits its environment from Steam, and if the
variable isn't there the SDK logs `Could not retrieve websocket URL` and gives up
without retrying. This is the single most common reason it looks broken.

Randy also has an HTTP port on 1337 for firing specific actions by hand:

```bash
curl -X POST http://localhost:1337/ -H 'Content-Type: application/json' \
  -d '{"command":"action","data":{"id":"1","name":"move","data":"{\"direction\":\"forward\",\"duration\":1.0}"}}'
```

## Running it on macOS

PEAK is Windows-only, but it builds and runs fine on a Mac through CrossOver.

For the assemblies, either install PEAK in a CrossOver bottle and copy them out of
`<bottle>/drive_c/Program Files (x86)/Steam/steamapps/common/PEAK/`, or pull just the
managed DLLs with [DepotDownloader](https://github.com/SteamRE/DepotDownloader)
(`-app 3527290 -os windows -qr`), which is about 16 MB instead of 5 GB.

Three things bite under Wine:

- **BepInEx needs a DLL override.** Run `winecfg`, Libraries tab, add `winhttp`, set it
  to *Native then Builtin*. Without this BepInEx never loads and no log is written,
  which looks exactly like a broken plugin.
- **D3D12 crashes on startup.** Add `-force-vulkan` to Steam's launch options.
- **Mouse input may not work** — `EnableMouseInPointer` isn't implemented in Wine. A
  controller sidesteps it entirely.

Setting the websocket URL in a shell doesn't survive a GUI launch. Put it in the bottle's
registry instead, which works however you start the game:

```bash
cxstart --bottle "Steam" -- reg add 'HKCU\Environment' /v NEURO_SDK_WS_URL /d "ws://localhost:8000" /f
```

## Keeping up with PEAK updates

Every Harmony target lives in one file, `Patches/PeakPatchTargets.cs`:

| Target | Used for |
| --- | --- |
| `CharacterInput.Sample` | injecting her input |
| `CharacterMovement.CheckFallDamage` | fall damage messages |
| `Character.UseStamina` | stamina running out |
| `Campfire.Light_Rpc` | checkpoints |
| `MapHandler.GoToSegment` | entering a new area |

If PEAK renames one, the plugin logs a warning and disables that hook rather than
crashing. Decompile `Assembly-CSharp.dll` with [ILSpy](https://github.com/icsharpcode/ILSpy)
or dnSpy, fix the signature in that one file, rebuild.

Signatures do drift between versions — `Character.UseStamina` and `Campfire.Light_Rpc`
have both gained parameters — so check there first if a hook goes quiet.

## Logs

- `PEAK/BepInEx/LogOutput.log` — BepInEx and this plugin
- `AppData/LocalLow/LandCrab/PEAK/Player.log` — Unity, and anything the SDK logs

Check both. The pack ships with `WriteUnityLog = false`, so SDK messages only land in
`Player.log`. The `MSB3277` Newtonsoft warning at build time is expected and harmless.

## Config

`BepInEx/config/com.sillyprootsoda.neuropeak.cfg`, written on first launch. Worth
knowing about: `MovementEnabled` (hand control back to yourself), `PerceptionEnabled`,
`VoiceChatEnabled`, and `AmbientMinInterval` if she's getting too many or too few
descriptions.

## Credits

The Neuro Game SDK is MIT licensed by Vedal AI and compiles into this plugin. PEAK
belongs to Aggro Crab and Landfall; none of its files are included here.
