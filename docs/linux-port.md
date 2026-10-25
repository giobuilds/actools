# Port Content Manager to a native Linux app (C# / Avalonia)

## Goal

A native Linux desktop app that manages an Assetto Corsa install and launches it under Steam Proton, so the everyday Content Manager loop works on Linux: browse cars and tracks, install mods, edit the configs the game reads, set up a session, and press Drive.

Assetto Corsa and Custom Shaders Patch stay Windows binaries inside the Proton prefix. This project ports the manager, not the game.

## What this repo is today

One Visual Studio solution, 31 projects, all `TargetFrameworkVersion` v4.5.2 (old-style `csproj` + `packages.config`).

| Area | Approx. size | Linux fate |
|---|---|---|
| `AcManager` UI (pages, dialogs, showroom wrappers) | 91k C# + most of the XAML | Rebuild in Avalonia |
| `AcManager.Tools` (managers, race, install, online, CSP settings) | 98k C# | Extract domain logic; replace Win32/WPF edges |
| `FirstFloor.ModernUI` (forked WPF toolkit) | 43k C# | Leave behind |
| `AcManager.Controls` | 24k C# | Rebuild the few controls the new UI needs |
| `AcTools` (ini, KN5, ACD, paths, `Game` session writer) | 28k C# | Port the file and session core |
| `AcTools.Render` + 79 `.fx` shaders | 48k C# | Later, on a Vulkan renderer |
| Other tools (showroom exe, preview updater, plates, track bakery, neural tyres, wheel angles) | rest | After the drive loop |

About 2,400 C# files (363k lines) and 382 XAML files (58k lines). The UI is WPF on a heavily modified Modern UI for WPF. Rendering is SlimDX (Direct3D 11) plus HLSL. Embedded browser is CefSharp (WinForms + WPF). Input scanning is SlimDX DirectInput. Launch and Steam invites go through Steamworks.NET and `user32`/`kernel32`. Documents are resolved with `Environment.SpecialFolder.MyDocuments` and the Steam install is found in the Windows registry (`AcTools/Utils/AcRootFinder.cs`). Live telemetry opens Windows file mappings `Local\acpmf_physics`, `Local\acpmf_graphics`, and `Local\acpmf_static`.

A line-by-line XAML translation is the wrong project. Avalonia’s XAML dialect, property system, and styling do not accept this tree, and `FirstFloor.ModernUI` is itself a Windows UI framework.

## Strategy

Strangler port, play-first.

- Leave `AcManager.sln` in place so the Windows app still builds.
- Add a new SDK-style `net8.0` solution beside it. Copy and modernize modules one at a time. The old projects reference absolute `Libraries\` DLLs, Fody, and `packages.config`; retargeting them in place will not produce a Linux app.
- Rebuild the shell in Avalonia. Keep behavior by reusing the code that already writes `race.ini` and the other AC configs (`AcTools/Processes/Game.Properties.cs`).
- Put every OS call behind a small platform interface. The first implementation is Linux + Proton. A Windows implementation can come back later and is not required to ship the Linux app.
- Ship a useful driver (library, quick drive, launch, content install, settings) before Custom Showroom, paint shop, or the track tools.

License stays Ms-PL. Keep third-party license notices. Prefer current NuGet packages over the vendored x86 binaries in `Libraries\`.

## Target architecture

```
src/
  AcTools.Core/                  net8.0  ini/lut/kn5/acd/ksanim, session writer, filters
  AcManager.Core/                net8.0  cars, tracks, weather, install, CSP file model, online
  AcTools.Platform.Abstractions/ net8.0  IAcInstallLayout, IGameLauncher, ISessionBridge, IInputDevices
  AcTools.Platform.Linux/        net8.0  Steam libraries, Proton prefix, process, DBus notify
  AcBridge/                      net8.0  win-x64 helper that runs inside the prefix
  AcManager.App/                 net8.0  Avalonia UI
tests/
  AcTools.Core.Tests/
  AcManager.Core.Tests/
```

`AcManager.App` talks only to `AcManager.Core` and the platform interfaces. Core code does not reference Avalonia, `System.Windows`, SlimDX, or the Windows registry.

Dependency direction:

```
Avalonia app
    → AcManager.Core → AcTools.Core
    → IGameLauncher / IAcInstallLayout / ISessionBridge
            ↑
      Platform.Linux          AcBridge (inside Proton, TCP to the app)
```

App settings for this program live under `$XDG_CONFIG_HOME/actools` (default `~/.config/actools`). Game settings stay where the game reads them, inside the prefix.

## How the app finds and runs the game

Steam app id is **244210**. On Linux the files are split:

- Game tree (cars, tracks, `acs.exe`, CSP `extension` folder): `steamapps/common/assettocorsa`, from `libraryfolders.vdf`. Also check `~/.steam/steam`, `~/.local/share/Steam`, Flatpak `~/.var/app/com.valvesoftware.Steam/data/Steam`, and Snap.
- Proton prefix: `steamapps/compatdata/244210/pfx`.
- Documents the game actually reads: `<prefix>/drive_c/users/steamuser/Documents/Assetto Corsa`, including `cfg/race.ini`, `cfg/video.ini`, `cfg/controls.ini`, `cfg/python.ini`, setups, replays, and `cfg/extension` (CSP). `AcPaths.GetDocumentsDirectory()` today returns the Linux user’s real Documents folder, which Proton does not use. That method becomes `IAcInstallLayout.DocumentsDirectory`.

Discovery acceptance: the app reports game root, documents root, Proton prefix, and whether `acs.exe`, `content/cars`, and `content/tracks` exist. The user can override all three paths.

Launch, in the same order Content Manager already uses:

1. Build the session with the existing `Game.*Properties` writers (`race.ini` sections `RACE`, `SESSION_*`, `CAR_*`, weather, ghost, groove).
2. Write that ini, and any touched `video.ini` / `controls.ini` / CSP cfg, into the prefix documents directory with AC’s line endings and encoding.
3. Start `acs.exe` with the Proton build recorded for app 244210 (`compatdata/244210/version`, then `config_info`, then `config.vdf`). The working directory is the game root. `steam -applaunch 244210` starts `AssettoCorsa.exe`, the official menu, which does not load the practice session.
4. Watch for a process whose name is `acs.exe`. On exit, return to the app.

`STEAM_COMPAT_DATA_PATH` is `compatdata/244210` (the directory that contains `pfx`). `STEAM_COMPAT_CLIENT_INSTALL_PATH` is the Steam client that owns that prefix. Steam has to already be running so the game can see the account.

Flatpak Steam is a sandbox. A host-installed app can only see the prefix if that path is on the host filesystem (it is, under `~/.var/...`). Document that, and grant read/write to the Steam library and the prefix. Do not require the user to run Content Manager inside the Steam sandbox.

## Shared memory (live timing while you drive)

`BetterMemoryMappedAccessor` calls `MemoryMappedFile.OpenExisting("Local\\acpmf_physics")` and the graphics and static twins. Those names exist inside Wine, not as Linux mappings the native app can open reliably across Proton versions.

Ship `AcBridge`, a small `win-x64` executable copied into the prefix and started before the game (or on demand):

- It runs under Wine, so `OpenExisting` works.
- It serves the three structs over `127.0.0.1` on a fixed local port. Wine and the host share loopback. A Unix socket through `Z:` is more fragile; skip it.
- Payload is the raw struct bytes plus packet id, at the same intervals the current reader uses (about 50 ms live, slower when idle).
- Replace SlimDX vectors inside those structs with explicit sequential fields so the layout matches the game and does not depend on SlimDX.

The Avalonia app implements `ISessionBridge` against that socket. If the bridge is absent, Drive still works; live laps, Discord presence, and the in-session overlay stay disabled.

## Input and CSP

The game, under Proton, reads `controls.ini` and CSP’s force-feedback config. The Linux app edits those files. It does not inject DirectInput.

- Phase 2 reads and writes the ini (axes, buttons, assists, FFB gain) and shows the device names already stored there.
- Capturing a new binding uses SDL2 (`SDL_Joystick` / gamepad API on evdev). Saving a binding writes the same keys `ControlsSettings` writes today.
- Wine’s DirectInput names and SDL names often differ. The first binding flow stores the SDL name and the axis index, then offers “bind once inside AC” when the written device does not match what the game saved back. A later mapping table can tighten that.
- `AcTools.WheelAngles` (vendor wheel SDKs, Win32 HID) stays on the backlog. Linux FFB for those wheels is handled by the kernel driver and by CSP inside Proton.

CSP itself is already a Windows DLL that Proton loads. `PatchHelper` and `PatchSettingsModel` are file editors for `Documents/Assetto Corsa/cfg/extension` and the game’s `extension` directory. Port the file model and a reduced settings UI. Do not port CSP.

## What moves, what is rewritten

**Move into `AcTools.Core` with mechanical cleanup**

- `AcTools/DataFile` (ini, lut, raw).
- KN5, KNH, KsAnim, ACD readers and writers. Swap SlimDX and `System.Drawing` types for `System.Numerics` and byte spans at the boundary.
- `StringBasedFilter`.
- `AcTools.GenericMods`. Hard links stay; directory junctions become symlinks.
- `Game` / `Game.Properties` session writer, once `AcPaths` is injected instead of `MyDocuments`.

**Move into `AcManager.Core` by cutting UI references**

- Car, track, weather, skin, and replay scanners and object models.
- Content installation (`ContentInstallation`, SharpCompress / 7-Zip).
- Content-repair rules that only change files.
- Online server list parsing and “join” (write server data, then launch).
- CSP feature flags and the extension cfg reader (`PatchHelper`).
- Python app enablement via `python.ini`.

`AcManager.Tools` currently references `FirstFloor.ModernUI`, `PresentationFramework`, and `System.Drawing`. Each extraction deletes those usings and turns dialogs into return values the Avalonia layer displays.

**Rewrite**

- Every window, page, and dialog. New shell: Drive, Content, Online, Settings. Virtualized lists (libraries are thousands of cars). CommunityToolkit.Mvvm. Dark theme in Avalonia styles, inspired by Content Manager’s layout.
- In-app web (CefSharp, mods browser, some online pages): open the system browser, or a single Avalonia WebView only where an embedded page is required.
- Charts: OxyPlot.Avalonia or LiveCharts2 where a page actually needs a graph.
- Ini and text editing: AvaloniaEdit.
- Notifications: `libnotify` via DBus. Tray icon: Avalonia tray. Single instance: a lock file under `$XDG_RUNTIME_DIR`.
- Images: previews are mostly JPEG/PNG and can load directly. DDS and BC7 go through a current Magick.NET Linux package (`Magick.NET-Q8-x64`), not the vendored 7.0.4 x86 build.
- Car sound preview: defer. FMOD has Linux libraries, but the current wrapper is Windows-only and is not needed to drive.
- Discord: a current Rich Presence library with a Linux native component, fed by `ISessionBridge`.
- Steam invites and overlay callbacks in `SteamStarter`: out of the first release. Launch goes through the Steam client. Facepunch.Steamworks is the candidate if invites come back.
- `AcTools.Render`, Custom Showroom, preview baking, paint shop, LOD generator, track map and outline, ambient shadows, track bakery: phase 4+, on Veldrid or Silk.NET (Vulkan). HLSL can be cross-compiled, but the SlimDX device, Win32 window, and shader resource loader are a new renderer. About 48k lines plus 79 shaders. This is its own project after the drive loop.

## Phases

### Phase 0 — Foundation

Outcome: `dotnet test` is green on Linux CI, and the app window shows the detected install.

- New solution, `Directory.Build.props`, nullable enabled on new projects, central package versions.
- GitHub Actions (or the existing CI) runs `dotnet test` on `ubuntu-latest`. No game binary in CI.
- Interfaces: `IAcInstallLayout`, `IGameLauncher`, `INotificationService`, `IAppPaths`.
- Steam library parser for `libraryfolders.vdf` plus the path candidates above.
- Proton prefix and documents detection, with manual override saved in the XDG config.
- Avalonia shell with a single “Install” page.

### Phase 1 — Library and Quick Drive

Outcome: pick a car, skin, track, and weather, press Drive, and land in AC under Proton.

- Port ini + the session writers. Golden tests: fixture `race.ini` files compared to output from the current `RaceProperties`, practice, hotlap, and track-day writers.
- Scan `content/cars` and `content/tracks`. List with search using `StringBasedFilter`. Show skin and preview image.
- Quick Drive modes for this slice: Practice, Hotlap, Race, Track Day. Same ini keys the Windows app writes.
- Basic video settings that are pure ini (`video.ini`: resolution, fullscreen). Leave gamescope and Proton overrides documented.
- Launch and “game is running / game exited” state.
- Manual checklist on a real prefix: one Kunos car, one mod car, CSP installed and not installed.

### Phase 2 — Make the install pleasant

Outcome: install a car from a zip, change controls and common CSP options, join a listed server.

- Archive install into `content/cars` and `content/tracks`, including the path rules already in `ContentInstallation`.
- File-only content repair suggestions.
- Controls, audio, assists, and Python apps as ini editors.
- A reduced CSP page for the options people actually change (weather FX, FFB post-process, graphics presets), backed by `PatchHelper`.
- Online: fetch the server list, filter it, join by writing the server session and launching.
- SDL2 device list. Editing existing bindings is in scope; perfect Wine-name matching is best-effort, with the in-game bind fallback.

### Phase 3 — While you are in the car

Outcome: the app can see laps and session state from a running Proton game.

- `AcBridge` win-x64, struct compatibility tests against captured dumps of the three mappings.
- UI: connected / live / disconnected, last lap, session status.
- Replays and car setups lists (documents folders).
- Discord presence fed by the bridge.
- Weather selection and the remaining quick-drive modes (drift, drag, weekend) once their `Game.Properties` types are ported and covered by golden ini tests.

Dedicated-server hosting stays optional. The dedicated server is also a Windows binary; running it is another Proton launch, not part of the driving milestone.

### Phase 4 — Showroom and authoring

Outcome: view a car in a native window and export a preview image.

- New `AcTools.Render.Veldrid` project. Port Lite showroom first (mesh, materials, skin textures, orbit camera).
- Dark showroom (shadows, extra effects) only after Lite loads real KN5 cars.
- Paint shop on Magick.NET for Linux.
- Batch previews, then LOD / track-map / ambient-shadow tools, each as a follow-up with its own acceptance test.

### Phase 5 — Parity backlog

Pull from here only when a driving workflow needs it: championships, RSR / SRS, workshop uploader, license plates, neural tyre tool, wheel-vendor tools, track bakery, in-app web mod browser.

## Testing

- Unit tests run on Linux with fixture trees. No Steam, no Proton, no GPU.
- Golden `race.ini` / `controls.ini` fragments checked into `tests/fixtures`.
- Path tests: libraryfolders samples, prefix documents layout, Flatpak and native Steam roots.
- Bridge tests: byte-level struct layout (size and field offsets) shared by `AcBridge` and the client.
- One manual script for the developer machine: detect, list, install a zip, quick drive, confirm the session in-game, confirm the bridge packet id advances.
- UI checks for each phase use the running Avalonia app (launch, search, select, drive, install). Screenshot-only checks are not enough.

## Risks

- **Proton documents path varies** if the user has moved `Documents` with a Wine symlink. Detection prefers the prefix path and always allows an override.
- **Flatpak Steam** hides process and device access differently than native Steam. Launch via `steam -applaunch` still works if `steam` is on `PATH` (the Flatpak exports it). Process tracking may need to look at `flatpak` children.
- **Control identity mismatch** between SDL and Wine DirectInput. Mitigated by round-tripping `controls.ini` and the in-game bind fallback.
- **Shared-memory layout drift** if CSP’s “reduce g-forces” or a future patch changes the struct. Pin offsets with tests and a version field; disable the bridge on mismatch rather than misreading telemetry.
- **32-bit assumptions.** The Windows solution is x86-first. The Linux app is `linux-x64`. `AcBridge` is `win-x64` to match current `acs.exe`.
- **Vendored native binaries** (CefSharp, SlimDX, old Magick.NET, FMOD, LevelDB) have no Linux build in-tree. Replace with NuGet or defer the feature. Do not copy those DLLs into the Linux output.
- **Scope.** Full parity, including the renderer, is a long project. The driving app is Phases 0–2. Treat Phase 4 as optional until Drive is daily-usable.

## Out of scope

- Porting `acs.exe` or CSP to Linux.
- Running the current WPF UI under Avalonia.
- An x86 Linux build.
- Rebuilding every dialog before Practice mode launches the game.

## First slice after approval

Phase 0 and the start of Phase 1:

1. Add `src/AcManager.App` (Avalonia, `net8.0`) and the core/platform projects above.
2. Move `IniFile` and the path logic behind `IAcInstallLayout`.
3. Implement Steam library and Proton prefix discovery, including manual override.
4. Scan cars and show them in a virtualized list with search.
5. Drive writes a minimal practice `race.ini` into the prefix and starts `acs.exe` under the prefix's Proton build. `steam -applaunch 244210` opens the official launcher menu, so it is not the Drive path.

That slice is the first thing you can use while playing. Later phases stack on the same layout and launcher.
