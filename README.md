# SkydiverSimulator — Amir & Sari
**Technion — Supervisor: Dr. Anna Clarke**
**Team: Amir Said (amir.said@campus.technion.ac.il) & Sari Morkos**

---

## What This Project Is

We are building **two Unity simulation projects** for a skydiving simulator:

### Project 1 — AR Freefall → [SariMorkos/SkydiverAR](https://github.com/SariMorkos/SkydiverAR.git)
- Skydiver in **free fall** (before parachute opens)
- Uses **XREAL Air2 Ultra** AR goggles
- Separate Unity project — see repo above
- Will use XSens motion capture to animate the skydiver body

### Project 2 — VR Parachute Simulator (THIS REPO — IN PROGRESS)
- Skydiver **under a parachute**, flying through the sky
- Uses **Oculus Meta Quest 2** VR headset
- Built on top of a previous student's project (Gilad)
- Real aerodynamics via a Matlab/C++ physics engine (EOM_Solver)

---

## How The System Works

```
XSens Suit (motion capture hardware)
        OR
Matlab script (replaying recorded .mvnx file)
        |
        | UDP packets (port 9763, MXTP02 protocol, 760 bytes)
        v
Unity (XSens Movella plugin receives packets)
        |
        v
Avatar skeleton animates in real-time
```

### The Matlab Script
- File: `/Users/amirsaid/Desktop/UNITY_F/animate_to_unity.m` (Amir's Mac version)
- Loads a `.mvnx` motion capture file (`session_tig100126-003.mvnx`)
- Sends skeleton data to Unity via UDP using Java networking (no toolbox needed on Mac)
- Anna's original script is `load_mvnx_and_animate.m` — do NOT modify it
- Run this script AFTER pressing Play in Unity

### The Physics Engine (EOM_Solver)
- `Assets/Plugins/EOM_Solver.dll` — compiled Matlab aerodynamics model
- Called from `PlayerMovement.cs` via P/Invoke (C# DllImport)
- **Windows only — does not work on Mac**
- Must be tested on the lab Windows PC
- Takes a 24-element state vector (position, rotation, velocity for canopy + body) and returns the next time step

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
| **Canopy_Rotated** | Parachute mesh (child of Avatar, positioned above head) |
| **Main Camera** | Follows the avatar with CameraFollow.cs |
| **GridManager** | Manages the SkyGrid of floating spheres |
| **Plane** | Ground plane |

### XSens Connection Setup
- Go to **Window > Live Capture > Connections**
- There should be an **Xsens Connection** with port **9763** and **Start On Play = true**
- If not, click **+** and add one

---

## Scripts

### `CameraFollow.cs`
Makes the Main Camera smoothly follow the Avatar.
- **Target**: Avatar transform
- **Offset**: (0, 4, -10) — behind and above
- **Smooth Speed**: 8

### `SuspensionLines.cs`
Draws two white lines from the canopy down to the avatar's shoulders.
- **Canopy**: Canopy_Rotated
- **Left Shoulder**: jLeftShoulder bone
- **Right Shoulder**: jRightShoulder bone
- **Canopy Spread**: 0.8 (how wide the lines spread at the canopy end)

### `SkyGrid.cs`
Creates an 11×11 grid of spheres that follow the avatar to simulate flying through the sky.
- **Sphere Prefab**: assign a sphere prefab
- **Avatar To Follow**: Avatar transform
- **Grid Size**: 5
- **Spacing**: 10.0

### `PlayerMovement.cs`
Controls physics simulation using EOM_Solver.
- **Windows lab PC only** — on Mac/Quest the DLL call is skipped (state held constant via `#if UNITY_STANDALONE_WIN`)
- Calls `EOM_Solver.dll` each FixedUpdate with current state
- Returns next position/rotation for canopy and body
- State vector is 24 doubles: [CanopyPos, CanopyRot, CanopyLinVel, CanopyAngVel, BodyPos, BodyRot, BodyLinVel, BodyAngVel]
- Drag **rbCanopy** and **rbBody** Rigidbodies into the Inspector

### `VelocityArrows.cs`
Shows real-time velocity arrows on the canopy. Attach to a new empty GameObject in the scene.
- **Cyan arrow** — horizontal velocity direction and speed
- **Yellow arrow** — vertical velocity (pointing down = descending, up = climbing)
- **canopyRigidbody**: drag the canopy Rigidbody in Inspector
- **arrowScale**: 0.4 (world units per m/s)

### `WindEffect.cs`
Creates 60 small cloud-sphere particles that drift upward past the avatar, giving a sense of descent speed. Attach to a new empty GameObject in the scene.
- **followTarget**: drag the Avatar transform in Inspector
- **driftSpeed**: 8 m/s upward drift (simulates falling at 8 m/s)
- **spawnRadius**: 10 m — particles spawn in a sphere this size around the avatar

### `LandingZoneMarker.cs`
Draws a pulsing orange bullseye on the ground as the target landing zone. Attach to a new empty GameObject and position it where you want the target.
- Two rings + crosshair, all LineRenderers (no mesh or prefab needed)
- **outerRadius**: 5 m, **innerRadius**: 1.2 m
- **pulseSpeed**: 1.2 Hz — alpha pulses so it's visible from altitude

---

## File Structure

```
SkydiverSimulator_AmirSari/
├── Assets/
│   ├── Canopy.obj              — wrong orientation, don't use
│   ├── Canopy_Rotated.obj      — correct canopy mesh (arch curves up)
│   ├── CameraFollow.cs         — camera tracks avatar
│   ├── SuspensionLines.cs      — lines from canopy to shoulders
│   ├── SkyGrid.cs              — floating sphere grid
│   ├── PlayerMovement.cs       — physics via EOM_Solver (Windows only, no-op on Mac/Quest)
│   ├── VelocityArrows.cs       — cyan/yellow velocity direction arrows on canopy
│   ├── WindEffect.cs           — cloud-sphere wind particles (sense of descent)
│   ├── LandingZoneMarker.cs    — pulsing orange bullseye target on ground
│   ├── Plugins/
│   │   └── EOM_Solver.dll      — aerodynamics engine (Windows only)
│   ├── Integration_Ready.unity — main scene
│   └── Scenes/                 — default Unity scenes
├── README.md                   — this file
├── TASKS.md                    — task tracking for both AI agents
└── ProjectSettings/
    └── ...
```

---

## How To Run (step by step)

1. Open Unity Hub → open `SkydiverSimulator_AmirSari` project
2. Open scene `Assets/Integration_Ready.unity`
3. Press **Play** in Unity
4. In Matlab, navigate to `/Users/amirsaid/Desktop/UNITY_F/`
5. Open and run `animate_to_unity.m`
6. The avatar should start animating in Unity

---

## VR Setup for Oculus Quest 2

### Packages added (auto-install on first open)
- `com.unity.xr.management` 4.4.0 — XR Plugin Management
- `com.unity.xr.oculus` 4.2.0 — Oculus XR Plugin for Quest 2

### One-time manual steps in Unity Editor (Sari must do this once)

After opening the project for the first time after pulling this commit:

1. Wait for packages to import (progress bar in bottom-right)
2. Go to **Edit > Project Settings > XR Plug-in Management**
3. If prompted to install XR Plug-in Management, click **Install**
4. Click the **Android** tab (the robot icon)
5. Check the box next to **Oculus**
6. Close Project Settings
7. Go to **Edit > Project Settings > XR Plug-in Management > Oculus** (Android tab)
8. Set **Target Devices** → check **Quest 2**
9. **Cmd+S** to save, then `git add . && git commit -m "XR: enable Oculus loader for Android" && git push`

### Scene setup (do after the above steps)
1. Open `Assets/Integration_Ready.unity`
2. Select **Main Camera** in the Hierarchy
3. Click **Add Component** → search for **VR Camera Rig** → add it
4. Drag **Main Camera** into the **Camera Follow** slot on `VRCameraRig`
5. Save the scene with **Cmd+S**

### Build for Quest 2
1. **File > Build Settings**
2. Platform: **Android** → click **Switch Platform**
3. Click **Player Settings** → set Company Name: `Technion`, Product Name: `SkydiverVR`
4. In **Build Settings**, click **Build** (or **Build and Run** with headset connected via USB)
5. The app ID is already set to `com.Technion.SkydiverVR`

---

## HUD Overlay

### SkydiverHUD.cs — what it does
Displays a heads-up display in the lower-right corner of the VR view showing:
- **ALT** — canopy altitude in meters
- **SPD** — horizontal speed in km/h
- **HDG** — heading in degrees (0–360°)

The canvas is created entirely in code — no manual UI setup needed. It attaches itself to the Main Camera as a world-space canvas 0.6m in front, so it follows the player's head in VR.

Shows zeros as placeholders until EOM_Solver physics are running.

### Scene setup for HUD
1. Select **Main Camera** in Hierarchy
2. Click **Add Component** → search **SkydiverHUD** → add it
3. Drag the **Canopy_Rotated** object's **Rigidbody** into the **Canopy Rigidbody** slot
4. **Cmd+S** to save

---

### VRCameraRig.cs — what it does
- Attached to Main Camera alongside `CameraFollow`
- On Quest 2 at runtime: detects active XR device, disables `CameraFollow` (headset drives camera instead), sets tracking origin to **Floor**
- On desktop/editor: does nothing — `CameraFollow` works normally

---

## What's Done So Far

- [x] Unity project set up with XSens plugin
- [x] Matlab → UDP → Unity skeleton animation pipeline working
- [x] Canopy mesh imported and positioned above avatar
- [x] SkyGrid (floating spheres) working
- [x] CameraFollow script (camera tracks avatar)
- [x] SuspensionLines script (lines from canopy to shoulders)
- [x] GitHub repo set up and shared
- [x] VR packages added (com.unity.xr.management + com.unity.xr.oculus) — see VR Setup section above
- [x] VRCameraRig.cs script — handles VR/desktop camera switching
- [x] XR Plug-in Management configured in Unity Editor — Oculus enabled on Android, Quest 2 target set
- [x] VRCameraRig component added to Main Camera in Integration_Ready.unity
- [x] HUD overlay (SkydiverHUD.cs) — shows altitude, speed, heading in VR

## What Still Needs To Be Done

See `TASKS.md` for the full task list with assignments.

---

## Important Notes

- **EOM_Solver.dll is Windows-only** — physics simulation only works on the lab PC
- **Never modify Anna's original scripts** (`load_mvnx_and_animate.m`, etc.)
- **Save Unity scene** with Cmd+S after every change in Edit mode — changes in Play mode are lost
- **Git workflow**: after making changes → `git add . && git commit -m "description" && git push`
- The `.gitignore` excludes: Library/, Temp/, Build/, Logs/, UserSettings/ — these are auto-generated

---

## For Both AI Agents — Read This First

1. Read this README completely
2. Read `TASKS.md` to see what's done and what's assigned to you
3. The project is in Unity 6000.2.6f2 — make sure your user has this version installed
4. The main scene is `Assets/Integration_Ready.unity`
5. After every task, update both `TASKS.md` AND this README to reflect what was done
6. Communicate with the other AI through `TASKS.md` — keep it accurate at all times

### AI Sync Protocol — follow this every session

**At the start of every session**, run `git pull` and re-read `TASKS.md` and `README.md` before doing anything.

**After completing any task:**
1. Move the task to **Completed** in `TASKS.md` — add your name and today's date
2. Add it to the **What's Done So Far** list in this README
3. If you added a script or changed how something works — add or update a section in this README explaining it (like the "VR Setup" section above)
4. Commit and push everything:
```bash
git add .
git commit -m "brief description of what was done"
git push
```

**Why this matters:** The other AI starts every session with zero memory of previous sessions. `TASKS.md` and `README.md` are the only way it knows what the current state of the project is. If you don't update them, the other AI will repeat your work or break things.
