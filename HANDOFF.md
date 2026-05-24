# HANDOFF — SkydiverSimulator (VR Parachute, Quest 2)
**Last updated: 2026-05-24**

---

## Quick orientation

| Item | Detail |
|------|--------|
| What it is | VR parachute simulator — skydiver under open canopy, viewed in Quest 2 |
| Team | Sari Morkos + Amir Said, Technion, supervised by Dr. Anna Clarke |
| Repo | https://github.com/amirs23/SkydiverSimulator_AmirSari (private — Amir's GitHub) |
| Unity version | 6000.2.6f2 |
| Target device | Meta Quest 2 (standalone Android APK, sideloaded) |
| Companion project | AR Freefall (XREAL) — see `SariMorkos/SkydiverAR` |

**Always start a session with:**
```bash
cd <project-path>
git pull
```
**Always end a session with:** update this file, commit, push.

---

## Current state

| Feature | Status |
|---------|--------|
| VR camera rig (Quest 2) | ✓ complete |
| Canopy mesh + suspension lines | ✓ complete |
| Sky grid environment | ✓ complete |
| HUD overlay (ALT / SPD / HDG) | ✓ complete |
| EOM_Solver physics DLL (Matlab-compiled) | ✓ present in Assets/Plugins/ |
| Avatar skeleton + XSens pipeline | ✓ working |
| Android APK build for Quest 2 | ✓ builds and deploys |
| Velocity arrows (`VelocityArrows.cs`) | ✓ added by Amir 2026-05-21 |
| Landing zone marker (`LandingZoneMarker.cs`) | ✓ added by Amir 2026-05-21 |
| Wind effect (`WindEffect.cs`) | ✓ added by Amir 2026-05-21 |
| Quest 2 head tracking (VR look-around) | ✗ not working — see open issues |

---

## Project paths

| Machine | Path |
|---------|------|
| Sari's Mac | `/Users/sarimorkos/Documents/projectA/workingFiles/SkydiverSimulator_AmirSari` |
| Amir's Mac | `/Users/amirsaid/Desktop/UNITY_F/` (approximate) |
| Lab computer | Clone fresh (see below) |

---

## How to clone on a new (lab) computer

```bash
git clone https://github.com/amirs23/SkydiverSimulator_AmirSari.git
cd SkydiverSimulator_AmirSari
```

No extra files needed — the `EOM_Solver.dll` is committed to `Assets/Plugins/`.

---

## How to build the APK (lab computer → Quest 2)

1. Open the project in **Unity 6000.2.6f2**
2. Wait for package resolution (progress bar bottom-right)
3. **File → Build Settings**
   - Platform: Android ✓
   - Make sure the Quest 2 scene is in the build list
4. Enable **Developer Mode** on the Quest 2 (via Meta Quest Developer Hub or the phone app)
5. Connect Quest 2 to the lab computer via USB-C
6. Put on the headset and **Allow USB Debugging** when prompted
7. Click **Build And Run**

If Build And Run fails, build the APK then sideload manually:
```bash
adb devices                        # confirm Quest 2 is listed
adb install -r SkydiverVR.apk
```

### First-time ADB setup (if adb not found)
```bash
# Mac
brew install android-platform-tools

# Or download Android SDK platform-tools and add to PATH
```

---

## How to run the full VR demo in the lab

1. Put on the Quest 2
2. Find the app in **Unknown Sources** in the Quest 2 app library
3. Launch it
4. The parachute simulation starts immediately — physics driven by EOM_Solver

### Optional: run Matlab animation alongside (for XSens avatar)
If the XSens avatar pipeline is needed:
1. Open Matlab → `cd` to the Matlab scripts folder
2. Run `animate_to_unity.m`
3. Make sure Unity (or the APK) is listening on UDP port 9763

---

## Scene structure

| GameObject | Role |
|-----------|------|
| VRCameraRig | Quest 2 camera — tracks headset position/rotation |
| CanopyMesh | Parachute canopy geometry |
| SuspensionLines | Lines from canopy to harness |
| SkyGrid | Visual reference grid (sky environment) |
| HUD (on camera) | ALT / SPD / HDG world-space overlay |
| Avatar | Skydiver body (XSens-driven skeleton) |

---

## Key scripts

| Script | What it does |
|--------|-------------|
| `Assets/VRCameraRig.cs` | Manages Quest 2 camera / XR session |
| `Assets/PlayerMovement.cs` | Physics — calls `EOM_Solver.dll` each FixedUpdate for canopy + body state |
| `Assets/SkydiverHUD.cs` | ALT / SPD / HDG overlay driven by canopy Rigidbody velocity |
| `Assets/SkyGrid.cs` | Sky environment grid |
| `Assets/SuspensionLines.cs` | Draws lines between canopy and harness points |
| `Assets/CameraFollow.cs` | Smooth camera follow behaviour |

### EOM_Solver.dll
- Matlab-compiled native library for parachute equations of motion
- Located at `Assets/Plugins/EOM_Solver.dll`
- Called from `PlayerMovement.cs` via P/Invoke every physics frame
- If the DLL needs to be recompiled, that is done in Matlab and the new DLL dropped into `Assets/Plugins/`

---

## XR / VR packages

| Package | Version |
|---------|---------|
| `com.unity.xr.management` | 4.4.0 |
| `com.unity.xr.oculus` | 4.2.0 |

These resolve automatically from `Packages/manifest.json` — no manual steps needed.

---

## Known issues / notes

- `EOM_Solver.dll` is x86-64 (Mac/Windows). If building on a different arch, the DLL may need recompilation in Matlab.
- HUD shows zeros until `EOM_Solver` physics are running (canopy Rigidbody not assigned → shows 0,0,0).
- XSens suit / Matlab stream is optional — avatar animates from XSens but the parachute physics are independent.
- Never modify Anna's original Matlab scripts (`load_mvnx_and_animate.m`).

### OPEN: Head tracking not working (2026-05-24)
The app runs on Quest 2 and the scene is visible, but moving your head does not move the camera — the view is frozen.

What was tried:
1. Set `m_AutomaticLoading: 1` / `m_AutomaticRunning: 1` in `Assets/XR/XRGeneralSettingsPerBuildTarget.asset` — Oculus XR loader now auto-initializes on Android.
2. Rewrote `VRCameraRig.cs` to use a coroutine (waits up to 5s for `XRSettings.isDeviceActive`) instead of checking on frame 1 — avoids the race condition where `CameraFollow` was never disabled.

Neither fix resolved it. The scene uses a plain `Main Camera` GameObject — no XR Origin / XR Rig hierarchy. Next thing to try: **replace the Main Camera with a proper XR Origin prefab** (from `com.unity.xr.interaction.toolkit` or the `OVRCameraRig` from the Meta XR SDK). The current setup relies on the XR plugin driving a bare camera, which may not be supported in Unity 6.

---

## Git workflow
```bash
git pull
# ... do work ...
git add .
git commit -m "description"
git push
```
