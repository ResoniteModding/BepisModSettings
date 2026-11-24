<p align="center">
    <img src="https://raw.githubusercontent.com/ResoniteModding/BepisModSettings/main/icon.png">
</p>

# Bepis Mod Settings
[![Thunderstore Version](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fthunderstore.io%2Fapi%2Fexperimental%2Fpackage%2FResoniteModding%2FBepisModSettings%2F&query=%24.latest.version_number&label=Thunderstore&style=flat&logo=thunderstore&logoColor=white)](https://thunderstore.io/c/resonite/p/ResoniteModding/BepisModSettings/)
[![Thunderstore Downloads](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fthunderstore.io%2Fapi%2Fv1%2Fpackage-metrics%2FResoniteModding%2FBepisModSettings%2F&query=%24.downloads&label=downloads&style=flat&logo=thunderstore&logoColor=white)](https://thunderstore.io/c/resonite/p/ResoniteModding/BepisModSettings/)
[![NuGet Version](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fnuget-modding.resonite.net%2Fv3%2Fregistration%2Fresonitemodding.bepismodsettings%2Findex.json&query=%24.items%5B0%5D.upper&label=NuGet&style=flat&logo=nuget&logoColor=white&color=004880)](https://nuget-modding.resonite.net/packages/resonitemodding.bepismodsettings)
[![Build](https://img.shields.io/github/actions/workflow/status/ResoniteModding/BepisModSettings/build.yml?style=flat&logo=github)](https://github.com/ResoniteModding/BepisModSettings/actions/workflows/build.yml)
[![License](https://img.shields.io/badge/license-LGPL--3.0--or--later-blue?style=flat)](#license)

A [Resonite](https://resonite.com/) mod that lets you edit your mod settings in settings tab of the dashboard.

Example home page:<img width="1619" height="845" alt="image" src="https://github.com/user-attachments/assets/f978a2b4-5ff0-450c-b85e-2752b8057b43" />
Typical configuration screen:<img width="1891" height="834" alt="image" src="https://github.com/user-attachments/assets/6662da50-3d01-4032-a504-01660dfecd86" />
Flags enum:<img width="1625" height="850" alt="image" src="https://github.com/user-attachments/assets/1e350606-5c78-42a7-a39f-2d275045d64c" />
Configuration page footer:<img width="1358" height="480" alt="image" src="https://github.com/user-attachments/assets/3884946a-54cd-4ce9-969d-94cdce5a9d05" />


## Installation (Manual)
1. Install [BepisLoader](https://github.com/ResoniteModding/BepisLoader) for Resonite.
2. Download the latest release ZIP file (e.g., `ResoniteModding-BepisModSettings-1.7.0.zip`) from the [Releases](https://github.com/ResoniteModding/BepisModSettings/releases) page.
3. Extract the ZIP and copy the `plugins` folder to your BepInEx folder in your Resonite installation directory:
   - **Default location:** `C:\Program Files (x86)\Steam\steamapps\common\Resonite\BepInEx\`
4. Start the game. If you want to verify that the mod is working you can check your BepInEx logs.

## Usage for Developers

### Adding BepisModSettings to Your Mod

1. Add the NuGet package reference to your mod's `.csproj` file:

```xml
<PackageReference Include="ResoniteModding.BepisModSettings" Version="1.*" />
```

2. Add it as a dependency in your `thunderstore.toml`:

```toml
[package.dependencies]
ResoniteModding-BepisModSettings = "1.0.0"
```

### Examples

Usage examples can be found in [`Examples.cs`](./BepisModSettings/Examples.cs).

## Attributions
Portions of this project are derived from the [MonkeyLoader Resonite GamePack](https://github.com/ResoniteModdingGroup/MonkeyLoader.GamePacks.Resonite), originally licensed under the GNU Lesser General Public License v3.0.

## License

This project is licensed under the GNU Lesser General Public License v3.0 or later. See [LICENSE](LICENSE) for details.
