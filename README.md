# SkydiverSimulator — Amir & Sari
**Technion — Supervisor: Dr. Anna Clarke**
**Team: Amir Said (amir.said@campus.technion.ac.il) & Sari Morkos**

---

## What This Project Is

**VR Parachute Simulator** — a Unity simulation of a skydiver **under a parachute**, flying through the sky:
- Uses **Oculus Meta Quest 2** VR headset
- Built on top of a previous student's project (Gilad)
- Real aerodynamics via a Matlab/C++ physics engine (EOM_Solver) / the lab simulator stream

---

## How The System Works

There are two separate data streams coming into Unity, **both delivered via Matlab**. The critical distinction: **pose and position come from two different sources and must be applied separately.**

```
Stream 1 — Avatar POSE only  (body posture / joint rotations)
──────────────────────────────────────────────────
XSens Suit (motion capture hardware)
        OR
Matlab script (replaying recorded .mvnx file)
        |
        | UDP packets (port 9763, MXTP02 protocol)
        v
Unity — XSens plugin animates avatar skeleton in real-time
        (bone ROTATIONS only — NOT world position)

Stream 2 — Avatar POSITION + parachute/skydiver state
──────────────────────────────────────────────────
Lab physics simulator (external, not in this repo)
        |
        | Matlab forwards data as UDP packets (port 9764)
        v
Unity — positions canopy and skydiver CG, updates HUD
        (SimulatorReceiver.cs — 26-field NED packet, see HANDOFF)

Each frame Unity receives: canopy position/orientation/velocity,
skydiver CG position/orientation/velocity, toggle inputs.
Unity is a pure renderer — all physics are computed externally.
```

### CRUCIAL — pose vs. position split (read before touching avatar/canopy code)

- **XSens animates POSE ONLY.** It drives the avatar's joint rotations (arms moving, body bending) — it does **not** move the avatar through the world.
- **Position comes from a completely separate system** — the lab simulator, via `SimulatorReceiver.cs` on port 9764.
- These are **two independent streams**. Visual attachments (suspension/steering lines) must sample live bone world-positions every `LateUpdate` rather than assuming a fixed avatar position — see `ProceduralCanopy.cs`.

### The Matlab Scripts
- `Matlab/animate_to_unity.m` — streams avatar skeleton data to Unity on port 9763. Run after pressing Play.
- `Matlab/sim_replay.m` — replays `sim_out.mat` (example simulator data) at 100 Hz on port 9764. Run for testing without the lab.
- Anna's original script `load_mvnx_and_animate.m` — **do NOT modify it**

---

## Unity Project Setup

### Version
- **Unity 6000.2.6f2** (Silicon/Mac) — use this exact version
- **Universal Render Pipeline (URP)**

### Required Packages (already installed)
- `com.movella.xsens` 2023.0.0 — XSens Movella plugin
- `com.unity.live-capture` 4.0.0-pre.3 — dependency for XSens

### Scene: `Integration_Ready.unity`
This is the main scene. It contains:
| GameObject | Purpose |
|---|---|
| **Avatar** | The skydiver skeleton, animated by XSens plugin |
| **Parachute** (ProceduralCanopy) | Procedural ram-air canopy, follows avatar |
| **Main Camera** | Head-tracked (VRCameraRig) + third-person fallback (CameraFollow) |
| **PhysicsController** | SimulatorReceiver (port 9764) + PlayerMovement |
| **Environment** | GroundEnvironment — town + tree fields baked at edit time |
| **Plane** | GrassGround — 2000m green ground plane |

### XSens Connection Setup
- Go to **Window > Live Capture > Connections**
- There should be an **Xsens Connection** with port **9763** and **Start On Play = true**
- If not, click **+** and add one

---

## Scripts

### `ProceduralCanopy.cs`
Builds a full ram-air parachute at runtime (cells, slider, pilot chute, suspension lines, risers, steering lines) — no external mesh needed. Attach to an empty GameObject ("Parachute") above the avatar.
- **Follow Target**: Avatar — canopy stays at `followTarget.position + followOffset` (default +7 m Y).
- **Left/Right Shoulder, Left/Right Hand**: avatar bones. If an Inspector ref is null at runtime, `ResolveBones()` re-finds it by name under the Avatar.
- **`[DefaultExecutionOrder(10000)]`** — ensures this runs after XSens applies the pose.
- **Bake workflow**: right-click → **"Bake Canopy (static parts → scene)"** — generates cells + slider + pilot chute + suspension lines as real saved child objects (`CanopyBaked`). On Play the baked statics are NOT regenerated; only the dynamic riser/steering cables are built. Right-click → **"Clear Baked Parts"** to redo.
- **Key gotcha — duplicate bones**: the imported avatar has TWO copies of every arm/hand bone. `FindSkinnedBone()` always resolves to the Hips-rooted `" 1"` bone (the one that moves the visible mesh), ignoring the flat non-moving copies under `Avatar/`. See HANDOFF for full explanation.

### `ToggleArmAnimation.cs`
Animates the avatar's arms to match toggle input (visual only — does NOT affect physics). Neutral = arms raised; pulling left/right swings that arm down toward the hip. Reads `PlayerMovement.LeftToggle`/`RightToggle`, falls back to XR trigger + keyboard (A/D/Space).
- **Disable when using XSens suit** — XSens drives arm bones directly and will conflict.
- Attach to the Avatar; drag in shoulder/elbow bones for each arm (use the `" 1"` skinned bones, not the flat `j*` nodes).

### `SimulatorReceiver.cs`
UDP receiver (port 9764) that replaces PlayerMovement's physics when the lab simulator is running. Unity becomes a pure renderer of the Matlab/simulator state.
- Reads the lab simulator's **26-field `x_s` packet** (NED frame, 3-2-1 Euler degrees).
- Positions the Avatar (skydiver CG); canopy follows the avatar via `ProceduralCanopy.followTarget`.
- Feeds `delta_l/delta_r` toggle values into `PlayerMovement.SetToggles()` → drives `ToggleArmAnimation`.
- Freezes everything until the first packet arrives. Restores freeze if stream stops > 0.5s.
- **Test**: run `Matlab/sim_replay.m` after pressing Play — no lab hardware needed.
- See HANDOFF.md for full wire format and frame-math details.

### `CameraViewController.cs`
First/third-person camera switcher. Attach to Main Camera.
- Keeps `VRCameraRig` active in BOTH views (look-around always works).
- **First person**: follows `firstPersonBone` (`Head 1` — Hips-rooted head bone).
- **Third person**: pulls back to `thirdPersonOffset` (default `(0,2,-14)`) to frame the full canopy.
- Toggle: **V** (Editor) / Quest **B** button / `CameraViewController.Instance.SetView(bool)` for a future UDP flag.

### `GroundEnvironment.cs`
Generates a low-poly landscape at edit time (right-click → **"Generate Environment"**) and bakes it into a child `EnvironmentBaked` object. Save the scene after generating. On Play the baked objects are already there — no runtime generation.
- **Town center**: ~350 buildings in a 350m radius.
- **Tree fields**: ~2500 trees spread across the full map, filling the horizon.
- **Area**: 3000m half-extent (6×6 km) — covers the visible horizon from 500m altitude.
- **Far clip**: raises `Camera.main.farClipPlane` to 5000m at launch.
- **Fog**: starts beyond 3000m (well past the map edge) so it softens the horizon but never hides the environment from any altitude.
- Right-click → **"Clear Environment"** to regenerate with a new `seed`.

### `PlayerMovement.cs`
Controls physics simulation using EOM_Solver.
- **Windows lab PC only** — on Mac/Quest the DLL call is skipped (`#if UNITY_STANDALONE_WIN`).
- Exposes `static float LeftToggle`, `RightToggle`, `BrakeLevel` and `static void SetToggles(float,float)` — called by SimulatorReceiver when the lab stream is active.
- `startHeight` field (default 500m) — lower it to test near the ground.

### `CameraFollow.cs`
Makes the Main Camera smoothly follow the Avatar.
- **Target**: Avatar transform | **Offset**: (0, 4, -10) | **Smooth Speed**: 8

### `SkyGrid.cs`
Fluffy cloud clusters at a fixed altitude that follow the avatar in XZ — giving a sense of descent.

### `VelocityArrows.cs`
Real-time velocity arrows on the canopy. Cyan = horizontal direction/speed. Yellow = vertical.

### `WindEffect.cs`
60 small cloud-sphere particles that drift upward past the avatar, giving a sense of descent speed.

### `LandingZoneMarker.cs`
Pulsing orange bullseye on the ground. Placed at the auto-calculated predicted landing spot.

---

## File Structure

```
SkydiverSimulator_AmirSari/
├── Assets/
│   ├── ProceduralCanopy.cs     — ram-air canopy (cells, slider, pilot chute, suspension + steering lines, toggle grips; bake workflow)
│   ├── ToggleArmAnimation.cs   — swings avatar arms to match toggle input (Quest triggers / A,D,Space)
│   ├── SimulatorReceiver.cs    — UDP receiver (port 9764): lab simulator → positions avatar + canopy
│   ├── CameraViewController.cs — first/third-person camera switcher (V / Quest B / API)
│   ├── GroundEnvironment.cs    — bakeable low-poly environment (town + trees, 6km×6km, horizon-fill)
│   ├── VRCameraRig.cs          — Quest 2 head-tracking camera
│   ├── CameraFollow.cs         — third-person chase camera
│   ├── SkyGrid.cs              — cloud layer (follows avatar in XZ, fixed altitude)
│   ├── GrassGround.cs          — green ground plane + horizon blend
│   ├── SuspensionLines.cs      — (legacy) lines from canopy to shoulders
│   ├── DestinationArrow.cs     — nav arrow pointing toward landing target
│   ├── SkydiverHUD.cs          — ALT / SPD / HDG world-space HUD
│   ├── PlayerMovement.cs       — physics via EOM_Solver (Windows only) / pure-C# fallback
│   ├── VelocityArrows.cs       — velocity direction arrows on canopy
│   ├── WindEffect.cs           — cloud-sphere wind particles
│   ├── LandingZoneMarker.cs    — pulsing orange bullseye target on ground
│   ├── Plugins/EOM_Solver.dll  — aerodynamics engine (Windows only)
│   ├── Integration_Ready.unity — main scene
│   └── Canopy_Rotated.obj      — (legacy) old static canopy mesh
├── Matlab/
│   ├── animate_to_unity.m      — XSens skeleton → UDP 9763
│   ├── sim_replay.m            — replay sim_out.mat → UDP 9764 (test without lab)
│   └── sim_to_unity.m          — synthetic spiral descent → UDP 9764 (uses OLD format)
├── README.md                   — this file
├── HANDOFF.md                  — running status + technical handoff between sessions
├── TASKS.md                    — task tracking for both AI agents
└── ProjectSettings/
```

---

## How To Run (step by step)

1. Open Unity Hub → open `SkydiverSimulator_AmirSari` project
2. Open scene `Assets/Integration_Ready.unity`
3. Press **Play** in Unity
4. Open and run `Matlab/animate_to_unity.m` in Matlab — the avatar will start animating
5. For physics: run `Matlab/sim_replay.m` (test replay) or start the lab simulator

---

## VR Setup for Oculus Quest 2

### Packages added (auto-install on first open)
- `com.unity.xr.management` 4.4.0 — XR Plugin Management
- `com.unity.xr.oculus` 4.2.0 — Oculus XR Plugin for Quest 2

### One-time manual steps in Unity Editor

1. Go to **Edit > Project Settings > XR Plug-in Management**
2. Click the **Android** tab → check **Oculus**
3. Go to the **Oculus** sub-page → check **Quest 2**
4. **Cmd+S** to save

### Build for Quest 2
1. **File > Build Settings** → Platform: **Android** → Switch Platform
2. **Build and Run** (with Quest 2 connected via USB)

---

## HUD Overlay (SkydiverHUD.cs)

Shows **ALT / SPD / HDG / BRK** in VR. Canvas auto-created at 0.6m in front of Main Camera.
Scene setup: Add Component → SkydiverHUD → drag Canopy Rigidbody in Inspector.

---

## What's Done So Far

- [x] Unity project set up with XSens plugin
- [x] Matlab → UDP → Unity skeleton animation pipeline working
- [x] SkyGrid rewritten — fluffy cloud clusters at fixed altitude, avatar descends through them
- [x] CameraFollow script (camera tracks avatar)
- [x] SuspensionLines script (legacy — lines from canopy to shoulders)
- [x] GitHub repo set up and shared
- [x] VR packages (com.unity.xr.management + com.unity.xr.oculus) + Quest 2 configured
- [x] VRCameraRig.cs — Quest 2 head tracking
- [x] HUD overlay (SkydiverHUD.cs) — ALT, SPD, HDG, BRK
- [x] Physics simulation — parachute aerodynamics, Quest 2 triggers steer left/right, both = brake/flare
- [x] Velocity arrows (VelocityArrows.cs)
- [x] Wind effect (WindEffect.cs)
- [x] Landing zone marker (LandingZoneMarker.cs)
- [x] Destination arrow (DestinationArrow.cs)
- [x] Grass ground (GrassGround.cs)
- [x] Avatar restart — A button (Quest) / R key (Editor)
- [x] Avatar materials converted to URP/Lit — visible on Quest 2
- [x] **ProceduralCanopy.cs** — 9-cell ram-air canopy, full suspension/steering line system, slider, risers, pilot chute (Amir, 2026-06-14)
- [x] **ToggleArmAnimation.cs** — arms animate between raised/pulled with toggle inputs (Amir, 2026-06-14)
- [x] PlayerMovement `LeftToggle`/`RightToggle`/`SetToggles()` — cross-script toggle values (Amir 2026-06-14 + Sari 2026-06-16)
- [x] Steering lines attach to real skinned hand bones (`LeftCarpus 1`) — fixed duplicate-bone bug (Sari, 2026-06-16)
- [x] Toggle grip handles on steering lines + line thickness 0.006→0.035 (Sari, 2026-06-16)
- [x] **SimulatorReceiver.cs** — UDP receiver for lab simulator (port 9764, 26-field NED packet, tested with sim_replay.m) (Sari, 2026-06-29)
- [x] **CameraViewController.cs** — first/third-person switcher, V key / Quest B button (Sari, 2026-06-17)
- [x] **GroundEnvironment.cs** — bakeable town + tree fields, 6×6 km, horizon-fill, fog pushed past map edge (Amir, 2026-06-15)

## What Still Needs To Be Done

| Task | Priority | Notes |
|------|----------|-------|
| Test camera switcher on Quest 2 | MED | CameraViewController.cs works in Editor — verify on headset (B toggles, first-person eye pos, third-person framing) |
| Tune GroundEnvironment on Quest 2 | MED | Lower counts / verify framerate once tested on device |
| Confirm position-Z sign convention with lab | HIGH | See HANDOFF — `positionZIsAltitude` flag currently ON |
| Arm ownership when XSens + SimulatorReceiver both active | HIGH | XSens and ToggleArmAnimation both write arm bones — decide winner |
| Verify SimulatorReceiver against real EOM_Solver on lab Windows PC | HIGH | |
| Assign SkyboxGrass material | LOW | Window → Rendering → Lighting → Skybox Material → drag Assets/Materials/SkyboxGrass.mat |
| Remove DevColorize from Avatar GameObject | LOW | |

---

## Important Notes

- **EOM_Solver.dll is Windows-only** — physics simulation only works on the lab PC
- **Never modify Anna's original scripts** (`load_mvnx_and_animate.m`, etc.)
- **Save Unity scene** with Cmd+S after every change in Edit mode — changes in Play mode are lost
- **Git workflow**: `git pull` → do work → `git add . && git commit -m "description" && git push`

---

## For Both AI Agents — Read This First

1. Read this README and `HANDOFF.md` completely
2. Read `TASKS.md` to see what's done and what's assigned to you
3. The project is in Unity 6000.2.6f2 — use this exact version
4. The main scene is `Assets/Integration_Ready.unity`
5. After every task, update `TASKS.md`, `README.md`, and `HANDOFF.md`

### AI Sync Protocol — follow this every session

**At the start of every session**, run `git pull` and re-read `TASKS.md`, `README.md`, and `HANDOFF.md` before doing anything.

**After completing any task:**
1. Move the task to **Completed** in `TASKS.md` — add your name and today's date
2. Add it to the **What's Done So Far** list in this README
3. Add a script section or update `HANDOFF.md` if it's a significant change
4. Commit and push:
```bash
git add .
git commit -m "brief description of what was done"
git push
```

**Why this matters:** The other AI starts every session with zero memory. `TASKS.md`, `README.md`, and `HANDOFF.md` are the only way it knows the current state of the project.
