# SwiftlySnow

A personal snow effect and `!snow` settings menu for Counter-Strike 2 servers running SwiftlyS2. Built using the current `Core.Menu` API and compatible with the optional CustomHUD renderer.

> **Early test build:** the plugin compiled against SwiftlyS2.CS2 1.5.1 without errors. It has not been tested on a running CS2 server or clients. Resource delivery, rendering, and compatibility with later versions need in-game verification.

## Credits

SwiftlySnow is a from-scratch SwiftlyS2 reimplementation inspired by [Pisex/cs2-snow](https://github.com/Pisex/cs2-snow). This rework was created by [panikajo](https://github.com/panikajo) in collaboration with AI. It is not a GitHub fork of the original repository. The snow particle is a third-party asset; see **Third-party particle** below.

## Features

- `!snow` opens a menu with **Enable snow** and **Disable snow** actions (the in-game menu text is Russian).
- Each player's choice is independent and saved by SteamID in `snow-preferences.json`; snow is enabled by default.
- The effect is attached above the player's pawn and is visible only to its owner. It is removed on death or disconnect and recreated on spawn. An observer does not see another player's effect.
- Uses SwiftlyS2 `Core.Menu`: the standard renderer works without CustomHUD. If configured, CustomHUD can render the same menu.
- Includes the original example snow particle as a losslessly encoded file, plus a script to restore it. See **Third-party particle** and **License** below.

## Requirements

- A CS2 server with **SwiftlyS2 1.5.1**. Later versions must be compatible with the 1.5.1 API.
- The snow particle and its dependencies must be available to **both server and clients** at the appropriate resource paths. Precaching alone does not deliver files to clients. A properly prepared Workshop add-on and AddonsManager are one possible delivery method; preparing and testing such an add-on is not included.
- To build: **.NET 10 SDK** and NuGet access. To extract the supplied particle: **Python 3**.

## Installation

1. Clone the repository. To build locally:
   ```sh
   dotnet publish source/SwiftlySnow.csproj -c Release -o build/addons/swiftlys2/plugins/SwiftlySnow
   python3 scripts/extract-particle.py
   ```
   Alternatively, download `SwiftlySnow.zip` from [Releases](../../releases) or the `SwiftlySnow-build` artifact from a successful GitHub Actions run. Extract the archive first; it contains `addons/` and a separate `particle/` folder.
2. Copy the generated or extracted `addons/swiftlys2/plugins/SwiftlySnow/` folder to your server at `addons/swiftlys2/plugins/SwiftlySnow/`. Preserve `snow-preferences.json` when updating.
3. The extraction script produces `assets/particles/kolka/snow_more_snow.vpcf_c`; the release archive contains the same file under `particle/particles/kolka/`. Prepare and serve this file **and any required dependencies** to the server and clients using a compatible content distribution setup. The particle is referenced in code as `particles/kolka/snow_more_snow.vpcf`. The provided file is not a ready-made Workshop add-on; copying it only to the server does not ensure clients receive it. Confirm rights to use and redistribute the resource before distributing it.
4. Restart the server or reload the map after installing the plugin and resources so the precache event runs. Join and type `!snow`. Test with two players, including death, respawn, reconnect and map change. Check SwiftlyS2 logs if it fails. If the menu opens but there is no snow, check that the particle and all dependencies are accessible on the server and clients.

## GitHub Actions and releases

GitHub Actions builds the plugin and uploads `SwiftlySnow.zip` as a workflow artifact on pushes, pull requests, and manual runs. To publish a new test release, push a version tag such as `v0.1.1`, or run **Build and release** manually with a new version in `release_tag`. A release is published only after a successful build and is marked as a prerelease. Existing releases are not overwritten. Repository Actions must be enabled and workflows must have permission to write repository contents.

## Third-party particle

The encoded particle comes from [Pisex/cs2-snow](https://github.com/Pisex/cs2-snow/blob/main/particles/kolka/snow_more_snow.vpcf_c). It is stored in base64 because this repository was published through a text-only file interface; `scripts/extract-particle.py` restores the exact binary and verifies its SHA-256:

`e825bb365c19dd54101798972bd331d41572a9b08f7158e18f40fdebe6296ff5`

The source repository does not provide a separate license file for this asset. Its reuse and redistribution rights are **not established**; request permission from the rights holder before redistribution. AGPL-3.0 for the plugin does not grant rights to the particle or its dependencies. The original C++ plugin identifies itself as GPL in metadata but does not specify a version in a license file.

## License

The SwiftlySnow plugin source in `source/`, build/extraction scripts, and workflow are licensed under the [GNU Affero General Public License v3.0](LICENSE). The third-party particle and any of its dependencies are excluded; see **Third-party particle**. No endorsement by the original authors is implied.
