# HANDOFF — SkydiverSimulator (VR Parachute, Quest 2)
**Last updated: 2026-05-31 (Sari)**

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
| Quest 2 head tracking (VR look-around) | ✓ fixed 2026-05-26 — see VRCameraRig.cs |
| Android parachute physics (no DLL) | ✓ fixed 2026-05-26 — pure C# model in PlayerMovement.cs |
| Avatar materials visible on Quest | ✓ fixed 2026-05-26 — all XSens mats converted to URP/Lit |
| Suspension lines connect to canopy | ✓ fixed 2026-05-26 — auto-finds mesh bounds, no manual transform needed |
| Dev color scheme (lines/canopy/avatar) | ✓ added 2026-05-26 — DevColorize.cs, lines yellow |
| Android parachute physics — Quest triggers steer | ✓ confirmed working on device 2026-05-26 |
| VR camera follows canopy/avatar in flight | ✓ fixed 2026-05-26 — followTarget on VRCameraRig |
| Canopy + avatar start at correct height (75m/70m) | ✓ fixed 2026-05-26 — auto-lift in PlayerMovement.Start() |
| Landing zone auto-positioned at predicted landing spot | ✓ fixed 2026-05-26 — uses actual heading + effective height |
| Avatar + canopy freeze on landing | ✓ fixed 2026-05-26 — _landed flag in PlayerMovement |
| Restart mid-flight or after landing | ✓ added 2026-05-26 — A button (Quest) / R key (Editor) |
| Wind particles follow direction of travel | ✓ fixed 2026-05-26 — combined up + backward drift in WindEffect |
| Destination arrow (nav indicator) | ✓ added 2026-05-31 — DestinationArrow.cs, floats in front of avatar, points at draggable target |

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
| `Assets/SuspensionLines.cs` | Draws lines between canopy and harness points — auto-detects canopy mesh center via Renderer bounds |
| `Assets/CameraFollow.cs` | Smooth camera follow behaviour |
| `Assets/DevColorize.cs` | **DEV ONLY** — attach to canopy/avatar to tint for visibility; remove before shipping |
| `Assets/Editor/FixXsensMaterials.cs` | Editor tool: Tools → Fix Xsens Materials for URP — converts Standard→URP/Lit, run once |

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

### RESOLVED: Head tracking (2026-05-26)
Root cause: Unity 6 no longer auto-applies XR pose to a plain Main Camera.
Fix: `VRCameraRig.cs` now manually reads `InputDevices.GetDeviceAtXRNode(XRNode.Head)` every `LateUpdate` and writes position/rotation to the camera transform. Uses `_sceneAnchor` (camera's initial world position) + HMD room-scale offset so the player stays in the right part of the world. Confirmed working on Quest 2.

### RESOLVED: Parachute static on Android (2026-05-26)
Root cause: `EOM_Solver.dll` is Windows x86-64 only — P/Invoke crashes silently on Android.
Fix: `PlayerMovement.cs` `#else` branch now has a full pure-C# parachute aerodynamics model (gravity, drag, glide, toggle-driven turns from Quest trigger axes). State vector layout identical to EOM_Solver so the rest of the code (HUD, VelocityArrows, etc.) is unaffected.

### RESOLVED: Avatar invisible on Quest (2026-05-26)
Root cause: XSens sample materials used the Standard (built-in) shader, which is stripped on Android URP builds.
Fix: Run **Tools → Fix Xsens Materials for URP** in the Editor (script at `Assets/Editor/FixXsensMaterials.cs`). Converts all materials under `Assets/Samples/Xsens` to `Universal Render Pipeline/Lit` and preserves albedo color.

---

## Completed since last session (2026-05-31, Sari)

| Task | Notes |
|------|-------|
| Destination arrow | ✓ Done. `DestinationArrow.cs` — arrow floats in front of avatar (crotch/chest area, configurable via Local Offset), always points horizontally toward destination. Drag any GameObject into the Destination slot in Inspector to change target. Fields: avatar, destination, localOffset, shaftLength, lineWidth, headFraction, arrowColor. |

---

## Completed since last session (2026-05-27, Amir)

| Task | Notes |
|------|-------|
| Brake / flare control | ✓ Done. Both triggers symmetric = braking. `BrakeLevel` static float (0–1) set each frame. Full brake: descent 0.3 m/s (was 5 m/s), glide 0.3× normal. Space bar = brake in Editor. |
| BRK indicator in HUD | ✓ Done. `SkydiverHUD` 4th line shows `BRK  XX%` when > 1%, color yellow→red. Only visible when braking. |

## Pending tasks (next session)

| Task | Notes |
|------|-------|
| Environment visuals | Grass texture on ground plane, sky-blue skybox, replace SkyGrid spheres with white cloud meshes |
| Canopy position + suspension line fix | Canopy should be slightly forward and tilted (filled with air), not directly above avatar. Suspension lines currently attach at avatar's sides — should attach at front harness points |

---

### RESOLVED: Suspension lines going wrong direction (2026-05-26)
Root cause: `canopy` Transform reference pointed to root of Canopy_Rotated prefab (y≈0), not the visual mesh high above.
Fix: `SuspensionLines.cs` now calls `GetComponentsInChildren<Renderer>()` and encapsulates their bounds to find the actual visual center, so any transform in the canopy hierarchy can be assigned — no need to hunt for the exact mesh child.

---

## Git workflow
```bash
git pull
# ... do work ...
git add .
git commit -m "description"
git push
```
