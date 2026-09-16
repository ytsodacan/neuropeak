# Local game assemblies

Copy the following files here from your PEAK installation
(`steamapps/common/PEAK/PEAK_Data/Managed/`). They are **not** redistributed with
this repository and are **not** fetched from any remote source.

Required:

- `Assembly-CSharp.dll`
- `PhotonUnityNetworking.dll`
- `PhotonRealtime.dll`
- `Photon3Unity3D.dll`

Optional (enables the built-in Photon Voice transmit sink, see the root README):

- `PhotonVoice.API.dll`
- `PhotonVoice.dll`

Do not copy `UnityEngine*.dll`, `Newtonsoft.Json.dll`, `0Harmony.dll`,
`BepInEx*.dll`, `mscorlib.dll`, `netstandard.dll` or `System*.dll` here — those
come from NuGet and the project deliberately excludes them from `lib/`.

## On macOS

PEAK has no macOS build, so there is no local install to copy from. Pull the Windows
depot down with DepotDownloader or SteamCMD instead — see the "Building and running on
macOS" section of the root README.md.
