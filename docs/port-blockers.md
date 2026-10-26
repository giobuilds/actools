# Linux port: blockers and how to tackle them

Companion to `docs/linux-port.md`. That document says what the port is. This one takes each known blocker, lists the realistic options, and recommends one. It ends with packaging as an AppImage and a set of staged milestones.

Nothing here is final. Each recommendation is the smallest thing that works today and leaves room to improve later.

## 1. Moving core logic out of AcTools and AcManager.Tools

### The problem

The new `src/` projects currently reimplement things (ini parsing in `src/AcTools.Core/Data`, the session writer in `src/AcTools.Core/Sessions/PracticeSession.cs`) instead of reusing the original code. The originals carry years of edge cases learnt from real players. A fresh rewrite will quietly miss some of them, and the game will either ignore a setting or refuse to load a session.

The originals cannot be referenced directly because they target .NET Framework 4.5.2 and pull in WPF, `System.Drawing`, SlimDX, `FirstFloor.ModernUI` and the Windows registry.

### Options

- **A. Keep rewriting from scratch.** Fast to start, but it drifts from what the game expects and you lose upstream fixes.
- **B. Retarget the old projects in place.** Tempting, but `AcTools` and `AcManager.Tools` reference each other's UI layers, vendored DLLs in `Libraries/`, Fody and `packages.config`. You would be fixing hundreds of errors before anything runs.
- **C. Copy files across one at a time and clean them up (recommended).** Move a file into `src/`, delete the Windows-only `using` lines, replace the few calls that need the OS with an interface, and keep the logic itself unchanged.

### Which files first

In order, because each one unlocks the next:

1. `AcTools/DataFile/` (the ini, lut and raw readers and writers). Everything else depends on these. Compare with the new `IniFile.cs` and keep whichever handles more cases, ideally the original.
2. `AcTools/Utils/AcPaths.cs` (223 lines). Most of it is pure path building. The parts that call `Environment.SpecialFolder.MyDocuments` should instead ask `IAcInstallLayout.DocumentsDirectory`.
3. `AcTools/Utils/AcRootFinder.cs` (43 lines). Do not move it. It reads the Windows registry. Its job is already done on Linux by `SteamAcDiscovery.cs`. Leave it in the Windows world.
4. `AcTools/Processes/Game.Properties.cs` (792 lines). This is the real `race.ini` writer: sections for the race, each session, every car, weather, ghost car and groove. It is the most valuable file in the repo for this port. Then the parts of `Game.cs` (430 lines) that build and write the ini, without the process launching, which becomes `IGameLauncher`.
5. `Game.Result.cs` (409 lines), which reads the results file after a race. Needed once you show lap times.
6. From `AcManager.Tools`: the car and track object scanners and the content installer, one at a time, as the UI needs them.

### How to cut the Windows dependencies

A short checklist for each moved file:

- **Registry calls:** replace with a method on a platform interface.
- **`MyDocuments` and other special folders:** take the path from `IAcInstallLayout`.
- **`System.Drawing` types (`Color`, `Bitmap`):** use plain numbers or a small record for colours. Images are a UI concern, so leave them for the Avalonia layer.
- **SlimDX vectors:** use `System.Numerics.Vector3`.
- **Dialogs and message boxes inside logic:** return a result (success, or a reason it failed) and let the UI decide what to show.
- **`FirstFloor.ModernUI` helpers:** most are small. Copy the one helper you need into `AcTools.Core`, or replace it with a standard library call.

A useful rule: `AcTools.Core` and `AcManager.Core` must never reference Avalonia or anything Windows-only. If a moved file needs one of those, the file is doing two jobs and should be split.

### Testing: golden files vs a real race.ini

- **Golden-file tests (recommended as the main safety net).** Run the original Content Manager on Windows (or ask a friend), set up a few known sessions (hotlap, a race with AI, a track day, a custom weather), and save the `race.ini` it writes. Those files go in `tests/` as fixtures. Your test builds the same session with the moved code and compares the output with the saved file. Compare section by section and key by key, not as raw text, so that key order and line endings do not cause false failures.
- **Real game check (as a final step).** A test cannot tell you whether `acs.exe` actually loads the session. Keep a short manual checklist: launch each fixture session under Proton and confirm the car, track and session type are right.
- Once the golden tests pass with the moved code, you can delete the rewritten `PracticeSession.cs` or keep it as a thin wrapper.

## 2. Proton launch robustness

### The problem

`ProtonSession.cs` starts `acs.exe` directly with Proton. That is the right idea, because `steam -applaunch 244210` opens the official launcher, not your session. But it depends on several things that vary between machines.

### Where Steam lives

Check, in order, and let the user override:

- Native: `~/.local/share/Steam`, and `~/.steam/steam` (often a symlink to the first; resolve it so you do not list the same library twice).
- Flatpak: `~/.var/app/com.valvesoftware.Steam/data/Steam`.
- Snap: `~/snap/steam/common/.local/share/Steam`.
- Extra libraries on other drives: read every Steam root's `steamapps/libraryfolders.vdf` and look for the one that contains app 244210.

The game folder and the prefix (`steamapps/compatdata/244210`) are usually in the same library, but not always. Look up each one separately.

### Launch options

- **A. `steam -applaunch 244210`.** Most robust in terms of environment, but it opens the official launcher. Only useful as a fallback ("open the game normally").
- **B. Run Proton directly on `acs.exe` (current approach, recommended).** Needs the right Proton build, `STEAM_COMPAT_DATA_PATH` and `STEAM_COMPAT_CLIENT_INSTALL_PATH`, and Steam already running.
- **C. Swap `AssettoCorsa.exe` for a launcher stub so `-applaunch` runs your session.** This is what some Linux users do by hand. It changes game files and Steam can revert them on update. Avoid for now.

### Detecting the Proton version

Try in this order and record which source worked, so error messages can say so:

1. `compatdata/244210/version` (written by Proton itself, for example "8.0-5").
2. `compatdata/244210/config_info` (first lines include the Proton path).
3. Steam's `config/config.vdf`, `CompatToolMapping` for 244210 (the tool the user picked).
4. If none: the newest Proton in `steamapps/common/` or `compatibilitytools.d/`, and tell the user you guessed.

Note that GE-Proton and other custom builds live in `compatibilitytools.d`, and Flatpak Steam keeps those inside its own sandbox folder.

### Flatpak Steam

A native app running a Proton build that belongs to Flatpak Steam can fail because Proton expects the Steam runtime from inside the sandbox. Options:

- Launch through `flatpak run --command=... com.valvesoftware.Steam` so it runs inside the sandbox.
- Or, for the first release, detect Flatpak Steam and fall back to `-applaunch` with a clear message.

Recommendation: support native Steam fully first, and treat Flatpak as a second milestone with its own manual test.

### Failure messages

Every failure should say what was checked, what was missing and what to do. Examples:

- "Steam is not running. Start Steam, then press Drive again."
- "Could not find Assetto Corsa in any Steam library. Checked: (list). Set the game folder in Settings."
- "The game has no Proton prefix yet. Launch it once from Steam so Proton can create it."
- "Proton build not found (looked in compatdata version file, config.vdf). Pick one in Settings."
- "The game exited after 2 seconds. Check the log at (path)."

Save the Proton output to a log file under `~/.local/state/actools/` (or `$XDG_STATE_HOME`) and offer a button to open it. That one log will save hours of support later.

## 3. AcBridge: live data from shared memory

### The problem

Assetto Corsa publishes live physics, graphics and static data in Windows named shared memory (`Local\acpmf_physics`, `Local\acpmf_graphics`, `Local\acpmf_static`). Those names exist only inside Wine. A native Linux app cannot open them reliably.

### Design

A small Windows program, `AcBridge.exe`, built with .NET as `win-x64` self-contained (or with Native AOT, so it starts fast and has no runtime to install). It is copied into the prefix and started alongside `acs.exe` under the same Proton build and prefix.

Its only job: open the three mappings, read them on a timer, and send the bytes to the Linux app.

### Transport options

- **A. TCP on 127.0.0.1 (recommended).** Wine and Linux share the loopback network. Simple, well supported, easy to debug with standard tools. Bind only to 127.0.0.1, never to all interfaces.
- **B. A file in the prefix that the bridge rewrites.** Very simple, but slow and full of partial-write races at 20 updates a second.
- **C. A Unix socket.** Recent Wine supports AF_UNIX, but behaviour across Proton versions is less predictable. Not worth it yet.
- **D. UDP.** Fine for live data where a lost packet does not matter, but TCP keeps the first version simpler.

### Protocol

Keep it tiny and versioned:

- On connect, the bridge sends a hello: protocol version, AC version if known, and the size of each struct. The app refuses to read if the sizes do not match what it expects, instead of showing garbage.
- Then a stream of frames. Each frame: a one-byte type (physics, graphics, static), the packet id from the struct, the length, then the raw bytes.
- Physics about every 50 ms while driving, graphics about every 100 ms, static once at start and when it changes. Slow down when the packet id stops changing (paused or in menus).
- The bridge exits when `acs.exe` exits, or when the app disconnects for more than a few seconds.
- Port: a fixed default with a setting to change it. Pass it to the bridge on its command line.

### Struct layout

The original reader uses SlimDX vector types inside the structs. In the new code, write the structs with plain sequential float fields, so their size and order match the game and do not depend on any library. A unit test can check each struct size against the known size.

### Fallback

Drive must work without the bridge. If it is missing or fails to connect, live laps and anything else that needs it are switched off, with a small note in the UI.

## 4. Controller mapping: Wine DirectInput vs SDL

### The problem

The game reads `cfg/controls.ini`, which names devices and axes the way DirectInput inside Wine sees them. A native Linux app sees the same wheel through SDL (or evdev) with a different name, a different GUID format and sometimes a different axis order. If the app writes SDL names, the game may not recognise the device.

### Options

- **A. Edit only what the game already saved (recommended first).** Read `controls.ini`, show the device names and bindings already there, and let the user change gains, dead zones, FFB strength and assists. Do not change which device or axis is bound. No mapping needed.
- **B. Bind inside AC once, then edit outside.** For new devices, tell the user to bind once in the game's own controls screen. The app then edits the values around it. This avoids the mapping problem completely.
- **C. Bind in the app with SDL and translate (later).** Read the SDL GUID, which contains the USB vendor and product ID. Wine builds its DirectInput instance GUID from the same vendor and product IDs. Match on vendor and product ID, not on the name. Keep a small table of known differences in axis order for popular wheels and pedals, and grow it from user reports.
- **D. Run a helper inside Wine that lists devices as DirectInput sees them.** The most accurate, because it asks Wine directly. It could live in AcBridge as a "list devices" command. Good later, once the bridge exists.

### Recommendation

Ship A and B first. Add D through AcBridge when it exists, since asking Wine is more reliable than guessing. Use C only if D turns out to be impractical.

## 5. UI rebuild order

The original has 382 XAML files on a custom WPF toolkit. None of it carries over. Rebuild only what players use most, in this order:

1. **Shell and navigation:** window, side menu, dark theme, a Settings page for paths. Already started.
2. **Quick Drive:** car, track, session type, Drive button. Already started. Make it reliable before adding more.
3. **Content library:** a virtualised list of cars and tracks with search, previews and basic details. Libraries can be thousands of cars, so use virtualisation from day one.
4. **Content install:** drag and drop a zip, show what will be installed, confirm. Already started.
5. **Settings pages** that map to one ini each: video, audio, controls (values only), assists, Python apps, CSP basics. Several already started.
6. **Results and lap times,** once `Game.Result.cs` is moved.
7. **Online server browser.** Larger job, needs the server list parsing moved.
8. **Later or never:** showroom, paint shop, track tools, and the built-in browser (open the system browser instead).

### Making it look nicer

A few habits that help a lot in Avalonia:

- Put colours, spacing and font sizes in one resource file and refer to them by name, so a theme change is one edit.
- Use styles with classes (like CSS) instead of setting properties on each control.
- Keep a consistent spacing scale (for example 4, 8, 12, 16, 24) and stick to it.
- Look at Content Manager's original layout for inspiration, then keep it simpler.
- Use the Avalonia previewer or hot reload so you see changes as you edit.

## 6. Packaging as an AppImage, when mature

### Build

- Publish the app with `dotnet publish` for `linux-x64`, self-contained, so users do not need .NET installed. Single-file publish is optional. Avalonia's native libraries (`libSkiaSharp.so`, `libHarfBuzzSharp.so`) are extracted or sit next to the binary either way, so test that they are found.
- Trimming makes the file smaller but can break MVVM bindings that use reflection. Start without trimming. Try it later with compiled bindings switched on.
- Consider `linux-arm64` later for Steam Deck alternatives and ARM laptops. The Steam Deck itself is x64.

### Assemble the AppImage

- Lay out an AppDir: the published files in `usr/bin`, a `.desktop` file, an icon (256x256 PNG plus SVG if you have one), and an `AppRun` that starts the binary.
- Use `appimagetool` to turn the AppDir into an AppImage. `linuxdeploy` is mainly for apps with many system libraries to bundle. A self-contained .NET app needs fewer, but check for system libraries Avalonia expects: fontconfig, libX11 and friends, and libICU (or set invariant globalisation to avoid needing ICU).
- Test on an older distro (an Ubuntu LTS in a container or VM), because AppImages built on a very new system can need a newer glibc than users have.

### Updates

- Embed update information in the AppImage pointing at GitHub Releases, and publish the `.zsync` file next to it. Then AppImageUpdate, or a manager such as AppImageLauncher or Gear Lever, can download only the changed parts.
- Inside the app, a simple "a new version is available" check against the GitHub Releases API is enough at first.

### Desktop integration

- A proper `.desktop` file with a reverse-domain app id, a name that is clearly not the official Content Manager, categories (Game; Utility), and the icon.
- Ms-PL requires the licence and notices to travel with the binary, so include `LICENSE.txt` and the third-party notices in the AppImage and show them in an About page.

### CI

- A GitHub Actions workflow on an Ubuntu runner: restore, run the tests, publish, build the AppDir, run appimagetool, upload the AppImage and `.zsync` as release assets when you push a tag.
- Run the tests on every pull request, and build the AppImage only on tags so CI stays fast.

## Staged milestones

1. **One real drive.** Tests pass on the target machine. The app finds the game, prefix and documents with native Steam. One car, one track, Drive loads straight into that session, and the app notices when the game exits. Clear failure messages and a launch log.
2. **Trustworthy sessions.** `DataFile`, `AcPaths` and `Game.Properties.cs` moved across. Golden-file tests for hotlap, race, track day and custom weather pass. The rewritten session writer retired.
3. **Library and install.** Virtualised content list with previews. Zip install and remove with the original installer logic.
4. **Settings.** One page per ini, values only for controls. Bind-inside-AC guidance for new devices.
5. **Flatpak Steam and custom Proton builds** supported, each with a manual test checklist.
6. **AcBridge.** Live lap times and results. Device listing through Wine, then in-app controller binding.
7. **First shareable build.** AppImage built in CI on tags, zsync updates, licence notices included, tested on an older distro.
8. **Online.** Server browser and join.
