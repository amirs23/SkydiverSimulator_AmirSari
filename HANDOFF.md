# HANDOFF — SkydiverSimulator (VR Parachute, Quest 2)
**Last updated: 2026-06-22 (Sari)**

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
| Procedural ram-air canopy | ✓ added 2026-06-14 — ProceduralCanopy.cs builds the whole canopy at runtime (cells, slider, pilot chute, suspension lines), with a right-click "Bake Canopy" workflow that saves the static parts into the scene |
| Steering lines attach to the hands | ✓ fixed 2026-06-16 — cables now target the real skinned hand bones (`LeftCarpus 1`/`RightCarpus 1`). See "Avatar has duplicate bones" note below |
| Steering toggle grips | ✓ added 2026-06-16 — a small grip in each hand at the bottom of the steering lines (`showToggleHandles` on ProceduralCanopy) |
| Suspension/steering line thickness | ✓ fixed 2026-06-16 — `lineWidth` 0.006 → 0.035 so thin lines stop rendering dashed at distance |
| First/third-person camera switcher | ◐ code done 2026-06-17, **not yet tested on Quest** — `CameraViewController.cs` (see below) |
| Richer environment (town + trees) | ◐ baker done 2026-06-17, **not yet tuned/tested on Quest** — `GroundEnvironment.cs` (see below) |
| Tunable spawn altitude | ✓ added 2026-06-17 — `startHeight` field on `PlayerMovement` (was hardcoded 500m); lower it to test near the ground |
| Matlab-driven movement (UDP) | ◐ reworked 2026-06-22 — freeze-until-stream + Sim=world/XSens=pose authority done; **canopy orientation + slider still look wrong** (see "Matlab/simulator integration" below) |

---

## Matlab / simulator integration (SimulatorReceiver.cs, reworked 2026-06-22)

The lab simulator → Matlab → Unity UDP pipeline. `SimulatorReceiver.cs` is on the
**PhysicsController** object (listens UDP 9764; XSens stays on 9763). Test sender:
`Matlab/sim_to_unity.m` (spiral descent). **Run Unity Play FIRST, then the .m script.**

### Confirmed design decisions (from Sari, 2026-06-22)
- **Authority: Sim = world, XSens = pose.** The simulator's CG channel moves the whole
  skydiver (Avatar root) through the world; XSens only articulates the limbs locally.
  Verified safe: `Packages/com.movella.xsens/.../XsensDevice.cs` writes only bone-LOCAL
  transforms (with `applyRootMotion` off it never touches the Avatar root's world pos),
  so moving the root and XSens posing don't fight.
- **Real simulator output frame (confirmed by Sari):** position is **Z-up (altitude),
  degrees, roll/pitch/yaw**. So `Frame Mode = EnuZupAerospace` is correct (NOT RawUnity).

### What was fixed this session
1. **Nothing moves without packets.** Before the first packet the canopy + body are
   hard-frozen (kinematic, zero velocity) and `PlayerMovement` is auto-found + disabled
   (its glide model was spinning the rig). Re-freezes if the stream stops >
   `streamTimeout` (0.5s). New knobs: `freezeUntilStream`, `streamTimeout`.
2. **Rig no longer tears apart.** Root cause: the canopy was position-driven by BOTH the
   sim AND `ProceduralCanopy`'s avatar-follow (`followTarget + 7m`, every LateUpdate).
   Fix: while the stream is live, SimulatorReceiver sets `proceduralCanopy.followTarget =
   null` so the sim is the sole canopy-position authority; restores it when frozen so the
   no-stream preview still looks right. New slot: `proceduralCanopy` (auto-finds).
3. **Removed a bogus 90° heading offset** in the rotation conversion (it assumed the
   canopy faced Unity +X; the mesh faces +Z). Added live calibration: `headingOffsetDeg`,
   `invertHeading`, `driveBodyRotation` (default OFF — body facing comes from XSens).

### EXACT data handling (audit trail — nothing else happens)
- Position (canopy `f[0,1,2]`, avatar `f[3,4,5]`): packet `(X,Y,Z)` → Unity `(X, Z, Y)`
  (altitude Z → Unity up). No scale, no offset — object placed at received coords.
- Velocity: same axis swap (HUD only).
- Rotation: packet `(roll,pitch,yaw)` deg → `Quaternion.Euler(-pitch, -yaw+headingOffsetDeg, -roll)`.

### STILL BROKEN — pick up here next session
- **Canopy orientation still looks wrong** even with the test sender set to level
  (`canopyRPY = [0,0,yaw]` now in `sim_to_unity.m`). "A bit better but still fucked up."
  → If it's wrong with roll=0, the rotation conversion has a real sign/axis bug. Derive
    the exact ENU(Z-up, deg RPY) → Unity attitude transform (proper `P·R·Pᵀ` conjugation
    for the Y/Z axis swap), not just per-axis negation. Test against the level sender.
- **The slider rectangle (the flat panel holding the suspension lines) doesn't look
  right** (Sari, 2026-06-22). Investigate in `ProceduralCanopy.cs` (slider build:
  `_sliderLocalY`, `_slHW`, `_slHD`, `MakeStaticLine` / riser attach). May be position,
  size, or that it's not tracking the sim-driven canopy correctly while live.
- Open question for the lab: does the **body heading** come from the sim or from XSens?
  (`driveBodyRotation` toggles this.)

---

## First/third-person camera switcher (CameraViewController.cs, 2026-06-17)

New `Assets/CameraViewController.cs` on the Main Camera. Keeps the head-tracked `VRCameraRig` active in **both** views (so look-around always works) and just changes what it follows + the offset — it does NOT hand off to `CameraFollow`'s LookAt, which would fight head rotation.

- **First person** (default): rig follows the `firstPersonBone` you drag in (use the real `Head 1` bone — Hips-rooted, see duplicate-bone note), at `firstPersonOffset`.
- **Third person**: rig swaps back to `VRCameraRig.followTarget` (canopy/avatar) and pulls back to `thirdPersonOffset` (default `(0,2,-14)`) so the whole canopy + pilot chute frame.
- **Toggle**: V key (Editor) / Quest **B** button (right controller — A is restart) / `CameraViewController.Instance.SetView(bool)` for a future Matlab UDP flag.
- **Desktop preview**: when no HMD is present it drives the camera itself (disables `CameraFollow`) so V visibly switches in the flat Editor. `firstPersonEuler` corrects bone facing in the Editor only (try 90/-90 on X) — irrelevant in VR where the HMD drives rotation.

**Inspector wiring on Main Camera:** add `CameraViewController` → drag in `vrCameraRig`, `cameraFollow`, and `firstPersonBone` (= `Head 1`).

**STILL TODO:** verify on the Quest 2 headset — confirm B toggles, first person sits at the eyes, third person frames the full canopy (tune `thirdPersonOffset`). Editor first-person facing may need `firstPersonEuler` tuning but that does not affect the headset.

---

## Richer environment (GroundEnvironment.cs, 2026-06-17)

New `Assets/GroundEnvironment.cs` — attach to an empty `Environment` object at the world origin. Right-click the component header → **"Generate Environment"** scatters a low-poly **town (center) + fields of trees** across most of the map into a saved `EnvironmentBaked` child, then Cmd+S to keep it. **"Clear Environment"** to regenerate (change `seed` for a new layout).

Design choices (per Sari): NOT a runtime chunk streamer — everything is placed up front so it's all visible from 500m with no pop-in. Keeps a clear landing circle around origin; props reuse a few shared URP/Lit materials + primitive meshes, colliders stripped, flagged Static for batching. Auto-raises `Camera.main` far-clip to 3000 + adds linear fog at launch so distant ground renders from altitude. Counts (~280 buildings / ~1400 trees), `areaHalfExtent`, `townHalfExtent`, `seed` all tunable.

**STILL TODO:** generate + save in the scene, then tune density/area and **test framerate on Quest 2** (lower counts if it dips). Confirm the VR rig camera is tagged `MainCamera` (or the far-clip bump won't apply).

---

## Matlab-driven movement (SimulatorReceiver.cs, 2026-06-17)

New `Assets/SimulatorReceiver.cs` — UDP receiver that replaces PlayerMovement's physics; Unity becomes a pure renderer of the lab/Matlab state. Listens on port **9764** (separate from the XSens skeleton stream on 9763, so both run at once). Each frame it applies canopy + skydiver position / orientation / linear+angular velocity to the Rigidbodies, stores wind (`SimulatorReceiver.Wind`), and feeds right/left steering into `PlayerMovement.SetToggles()` → existing `ToggleArmAnimation` converts those to shoulder pitch (arms up at 0, down at 1). It disables `PlayerMovement` so the two don't fight.

**Wire protocol:** one UDP datagram = an ASCII line of 29 comma-separated numbers (canopy pos/ori/linvel/angvel, skydiver pos/ori/linvel/angvel, wind, right+left steer). Full field order documented at the top of `SimulatorReceiver.cs` and `Matlab/sim_to_unity.m`.

**Test sender:** `Matlab/sim_to_unity.m` flies a 60s spiral descent with oscillating steering. Run it after pressing Play in Unity (set `SimulatorReceiver` port = 9764, wire canopy/body/playerMovement).

**Setup:** add `SimulatorReceiver` to a GameObject, drag in the canopy + body Rigidbodies and the `PlayerMovement` component.

**STILL TODO (2026-06-17 — connection confirmed working, but):**
- **Rotation/coordinate-frame mapping needs tuning.** `frameMode` defaults to `EnuZupAerospace` (RH, Z-up, aerospace yaw/pitch/roll) → converted in `ConvPos`/`ConvRot`. Confirm the REAL lab/EOM output frame (likely NED or other) and fix the axis/sign mapping. Positions looked OK; orientations were off.
- Decide who drives the **arms** when XSens is also connected — XSens mocap and `ToggleArmAnimation` both write the arm bones and will fight. Pick one (see "both connections at once" discussion).
- Verify on the lab Windows PC against the real `EOM_Solver` stream.

---

## Next / planned (as of 2026-06-17)

| Task | Priority | Notes |
|------|----------|-------|
| Test camera switcher on Quest 2 | MED | Code done (`CameraViewController.cs`). Put on the headset: confirm B toggles, eye position, and third-person framing; tune `thirdPersonOffset`. |
| Richer environment (buildings, trees, props) | MED | Add real 3D ground props on top of `GrassGround.cs` + `SkyGrid.cs`. Keep Quest 2 perf in mind (low-poly, LOD, instancing). |
| Skydiver/canopy movement via Matlab physics | HIGH | Drive avatar+canopy translation from the Matlab pipeline (`animate_to_unity.m` UDP + `EOM_Solver.dll` on the lab PC) instead of the pure-C# placeholder. Verify on the lab Windows machine. |

---

## Avatar has duplicate bones (important for any bone attachment)

The imported avatar contains **two copies of every arm/hand bone**:
- **Real skinned/animated bones** — nested under `Hips`, given a `" 1"` suffix by Unity to de-dupe the name (`Avatar/Hips/.../LeftShoulder 1/LeftElbow/LeftCarpus 1`). These move with the visible mesh; in the T-pose they spread along world **X** (`LeftCarpus 1` at X≈-0.79, `RightCarpus 1` at X≈+0.79).
- **Flat reference nodes** — parented directly under `Avatar` (`Avatar/LeftCarpus`, `Avatar/jLeftWrist`, the `p*` landmarks). These sit at the torso (X≈0) and **never move**.

When attaching anything to a hand/arm (cables, IK, `ToggleArmAnimation.cs`), target the **Hips-rooted `" 1"` bone**, not the flat `Avatar/<name>` / `j*` copy. `ProceduralCanopy.FindSkinnedBone()` does this automatically (matches the name ignoring a trailing `" 1"` and prefers the copy with a `Hips` ancestor).

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
| Grass ground | ✓ Done. `GrassGround.cs` — attach to Plane, auto-scales it to 2000x and applies URP/Lit green material. Also sets skybox ground color to blend at horizon. Two color fields: Grass Color (plane) and Skybox Ground Color (horizon). |
| Cloud layer (SkyGrid rewrite) | ✓ Done. `SkyGrid.cs` rewritten — fluffy white cloud clusters built from puff-spheres, no prefab needed. Grid follows avatar in XZ but stays at fixed Y (Cloud Height), so you descend through the layer. All fields Inspector-configurable. Avatar start height raised to 500m. |

---

## Completed since last session (2026-05-27, Amir)

| Task | Notes |
|------|-------|
| Brake / flare control | ✓ Done. Both triggers symmetric = braking. `BrakeLevel` static float (0–1) set each frame. Full brake: descent 0.3 m/s (was 5 m/s), glide 0.3× normal. Space bar = brake in Editor. |
| BRK indicator in HUD | ✓ Done. `SkydiverHUD` 4th line shows `BRK  XX%` when > 1%, color yellow→red. Only visible when braking. |

## Pending tasks (next session)

### Architecture change — lab simulator replaces internal physics
The internal physics engine (`PlayerMovement.cs` with `EOM_Solver.dll` + C# fallback) is being retired. The lab's physics simulator will send all movement data to Matlab, which forwards it to Unity via UDP. Unity becomes a pure renderer. The XSens avatar animation pipeline (port 9763) is unaffected.

Incoming UDP data per frame (port TBD — confirm with lab team):
- Canopy: position [X,Y,Z], orientation [roll,pitch,yaw], linear velocity [Vx,Vy,Vz], angular velocity [wx,wy,wz]
- Skydiver CG: same set
- Wind vector [vx,vy,vz]
- Left/right steering inputs (0–1 float each)

| Task | Priority | Notes |
|------|----------|-------|
| New UDP receiver for lab simulator | ◐ IN PROGRESS | `SimulatorReceiver.cs` done: listens 9764, freeze-until-stream, Sim=world/XSens=pose authority, position confirmed correct (Z-up). **Remaining: fix canopy orientation + slider rectangle** — see "Matlab / simulator integration" section above. Confirm real port with lab. |
| Arm animation from steering inputs | HIGH | Steering input values (left/right, 0–1) come in the simulator UDP packet. Convert to shoulder pitch: 0 = arms fully up, 1 = arms fully down along body. Apply to RightShoulder and LeftShoulder bones. Purely visual — physics effect already handled by simulator. |
| Ground environment — trees and buildings | MEDIUM | Add trees and buildings on the ground so the skydiver can see them growing larger as they descend from 500m. Use simple procedural geometry or low-poly prefabs — no high-detail assets needed. |
| First/third person view toggle | MEDIUM | Switch camera at runtime three ways: (1) keyboard key, (2) Meta Quest controller button (same pattern as A = restart), (3) flag received in the Matlab UDP stream. First person = camera at avatar head. Third person = current follow camera. |
| Remove DevColorize from Avatar | LOW | In Unity Editor: select Avatar in Hierarchy → find DevColorize component in Inspector → right-click → Remove Component. Keep DevColorize on other objects (canopy etc.) if attached. |
| Canopy overhaul — multi-cell mesh | HIGH | Replace single `Canopy_Rotated.obj` mesh with a procedural multi-cell ram-air canopy. Each cell a different color. 7 or 9 cells (confirm with Sari). |
| Suspension lines — full rewrite | HIGH | Full rewrite of `SuspensionLines.cs`. Structure: A+B lines from each cell boundary → front-left/right slider corners. C+D lines → rear-left/right slider corners. Slider = flat rectangle halfway down. Below slider: 4 risers to shoulders. Steering lines: cascade of 4 from outer rear cells each side → rear slider corners → skydiver's hands (yellow toggle loops). See suspension_lines reference in projectA folder. |
| Pilot chute | MEDIUM | Small dome mesh trailing behind the main canopy. Always oriented toward the velocity vector, not the canopy heading. |
| Horizon color mismatch | LOW | Grass plane green and skybox ground green are slightly different shades. Fine-tune the two color fields in `GrassGround.cs` (Grass Color + Skybox Ground Color). |

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
