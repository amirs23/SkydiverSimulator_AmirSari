# TASKS — SkydiverSimulator
**Last updated: 2026-06-29 (Sari) — matched the REAL simulator output format: SimulatorReceiver now reads the 26-field `x_s` layout with a proper NED 3-2-1 → Unity rotation; canopy follows the avatar (rig stays intact, no more inversion); added `Matlab/sim_replay.m` to stream the example `sim_out.mat`. Tested OK against real data.**
**Amir's AI**: Claude (claude-sonnet-4-6)
**Sari's AI**: Claude (claude-sonnet-4-6)

> This file is the sync point between both AI agents.
> After completing any task, update this file, commit, and push immediately.
> Before starting work, do `git pull` to get the latest version.

---

## Completed Tasks

| Task | Done by | Notes |
|------|---------|-------|
| Unity project setup (version 6000.2.6f2, URP) | Amir | |
| XSens Movella plugin installed | Amir | com.movella.xsens 2023.0.0 |
| Matlab → UDP → Unity pipeline working | Amir | Uses animate_to_unity.m |
| Canopy mesh imported and oriented correctly | Amir | Use Canopy_Rotated.obj |
| SkyGrid script (floating spheres) | Amir | Assets/SkyGrid.cs |
| CameraFollow script | Amir | Assets/CameraFollow.cs |
| SuspensionLines script (canopy to shoulders) | Amir | Assets/SuspensionLines.cs |
| GitHub repo created and pushed | Amir | https://github.com/amirs23/SkydiverSimulator_AmirSari |
| VR packages added to manifest.json | Sari (2026-05-19) | com.unity.xr.management 4.4.0 + com.unity.xr.oculus 4.2.0 |
| VRCameraRig.cs script | Sari (2026-05-19) | Disables CameraFollow on Quest 2, sets Floor tracking origin |
| Android app ID updated | Sari (2026-05-19) | com.Technion.SkydiverVR |
| VR setup fully configured in Unity Editor | Sari (2026-05-19) | XR Plug-in Management → Android → Oculus checked, Quest 2 target set, VRCameraRig added to Main Camera, scene saved |
| HUD / UI overlay | Sari (2026-05-19) | SkydiverHUD.cs — shows ALT, SPD, HDG. Auto-creates world-space canvas on Main Camera. Needs canopy Rigidbody wired in Inspector. |
| Physics simulation (EOM_Solver) hookup | Amir (2026-05-19) | PlayerMovement.cs fixed: rotation bug (quaternion→eulerAngles), camera override removed, cross-platform guard added (#if WIN). Works on all platforms; on Mac/Quest state passes through unchanged. |
| Velocity/heading arrows | Amir (2026-05-19) | VelocityArrows.cs — cyan arrow = horizontal speed/direction, yellow arrow = vertical (descent). Attach to scene, drag canopy Rigidbody in Inspector. |
| Wind/environment effects | Amir (2026-05-19) | WindEffect.cs — 60 cloud-sphere particles drift upward past the avatar giving sense of descent. Attach to scene, drag Avatar in Inspector. |
| Landing zone marker | Amir (2026-05-19) | LandingZoneMarker.cs — pulsing orange bullseye (2 rings + crosshair) drawn with LineRenderers. Place GameObject on the ground at the target spot. |
| Scene wired up and verified on Mac | Amir (2026-05-20) | WindEffect (Avatar wired), LandingZoneMarker (at X:20 Z:20), VelocityArrows (canopy Rigidbody slot empty — activates when EOM_Solver runs on Windows) added to Integration_Ready.unity. XR packages conflict fixed (removed com.unity.xr.oculus — Sari must re-add on Windows build machine). Apply Root Motion unchecked on Avatar Animator. Scene verified: avatar animates, suspension lines connected, HUD working, wind particles visible, landing zone pulsing. |
| Destination arrow (nav indicator) | Sari (2026-05-31) | DestinationArrow.cs — arrow floats in front of avatar, points toward a draggable destination target. All fields Inspector-configurable: avatar ref, destination ref, local offset (position on avatar), shaft length, line width, head fraction, color. Add empty GameObject → Add Component → DestinationArrow → drag Avatar + destination target. |
| Grass ground | Sari (2026-05-31) | GrassGround.cs — attach to Plane. Auto-scales plane to 2000x, applies green URP/Lit material, sets skybox ground color for horizon blend. |
| Cloud layer (SkyGrid rewrite) | Sari (2026-05-31) | SkyGrid.cs rewritten — fluffy puff-sphere cloud clusters, fixed Y altitude, follows avatar in XZ. Avatar start height raised to 500m. |
| Matlab-driven movement — UDP receiver (matched to REAL sim format) | Sari (2026-06-29) | `SimulatorReceiver.cs` (port 9764, separate from XSens 9763) now reads the lab simulator's actual **26-field `x_s`** packet (parachute `_b` + skydiver `_s` pos/vel/rates/attitude + `delta_l/r` toggles). Frame = GNC/**NED** (X fwd, Y right, Z down), Euler **3-2-1 deg**; `ConvRot` builds body axes from R=Rz·Ry·Rx and remaps NED→Unity via LookRotation (replaces the old per-axis-negation hack — fixed the canopy-orientation bug). Avatar is the world body; canopy **follows the avatar +7 m** (rig stays intact) and takes only attitude from the sim. New knobs `positionZIsAltitude`, `toggleScale`. Replay sender `Matlab/sim_replay.m` streams the example `sim_out.mat` at 100 Hz — **tested OK** (descends, glides, ~44° turn at t≈30–50 s). **Still needs:** lab to confirm position-Z sign convention; resolve arm ownership when XSens also connected; verify against real EOM_Solver on lab Windows PC. See HANDOFF. |
| Richer environment — baker (code) | Sari (2026-06-17) | `GroundEnvironment.cs` — attach to an empty "Environment" object at origin, right-click → "Generate Environment" to scatter a low-poly town (center) + fields of trees across most of the map into a saved `EnvironmentBaked` child (mirrors ProceduralCanopy's bake; not runtime-streamed so it's all visible from altitude). Keeps a clear landing circle; strips colliders; flags props Static for batching; raises camera far-clip + adds fog so the ground renders from 500m. Counts/area/seed tunable. **Not yet tuned/tested on Quest.** Also added `startHeight` field to `PlayerMovement` (replaces hardcoded 500m) to spawn low for testing. |
| First/third-person camera switcher (code) | Sari (2026-06-17) | `CameraViewController.cs` on Main Camera. Keeps head-tracked `VRCameraRig` active in BOTH views (look-around always works); toggling only changes follow target + offset (does NOT switch to `CameraFollow`'s LookAt, which would fight the HMD). First person follows a dragged head bone (`Head 1`); third person pulls back to `thirdPersonOffset` to frame the full canopy. Toggle: V (Editor) / Quest **B** / `Instance.SetView(bool)` for a future UDP flag. Desktop fallback drives the camera so V works without a headset (`firstPersonEuler` corrects Editor-only facing). **NOT yet tested on Quest** — see "In Progress". |
| Steering lines → hands + toggle grips + line thickness | Sari (2026-06-16) | ProceduralCanopy steering cables now attach to the real skinned hand bones. **Key gotcha:** the avatar imports TWO copies of each arm bone — flat copies under `Avatar/` (`Avatar/LeftCarpus`, `jLeftWrist`) sit at the torso and never move; the real Hips-rooted copies carry a `" 1"` suffix (`LeftCarpus 1` = visible hand). `FindSkinnedBone()` ignores the trailing `" 1"` and prefers the `Hips`-rooted copy. Replaced the magenta debug markers with steering-toggle grips (`showToggleHandles`). Bumped `lineWidth` 0.006 → 0.035 so the thin lines stop rendering dashed at distance. See README → "FIXED — steering lines now attach to the hands". Commits `b2ac784`, `5d16ce3`. |

---

## In Progress

| Task | Assigned to | Notes |
|------|-------------|-------|
| Test camera switcher on Quest 2 | Sari | `CameraViewController.cs` is wired and works in the Editor. Put on the Quest 2 and verify: B button toggles views, first person sits at the eyes (tune `firstPersonOffset`), third person frames the whole canopy + pilot chute (tune `thirdPersonOffset`). HMD drives rotation in VR, so `firstPersonEuler` (Editor-only) is irrelevant on device. |

## To Do — VR Parachute

| Task | Assigned to | Priority | Notes |
|------|-------------|----------|-------|
| Richer environment (buildings, trees, props) | TBD | MED | Today the world is only `GrassGround.cs` (green plane) + `SkyGrid.cs` (clouds). Add real 3D ground props — buildings, trees, landmarks — so there's a believable scene to descend toward. Watch Quest 2 performance: low-poly assets, GPU instancing / LOD, and keep props near the landing zone. |
| Assign SkyboxGrass material in Unity | Sari | LOW | Window → Rendering → Lighting → Skybox Material → drag in Assets/Materials/SkyboxGrass.mat. Fixes horizon color mismatch. |

---

## How AI Agents Should Use This File

1. **Before starting any session**: run `git pull` and read this file
2. **When you complete a task**: move it to the Completed section, add your name and today's date
3. **When you start a task**: move it to In Progress and add your name
4. **When you discover a bug or issue**: add it to a new section called "Issues / Blockers"
5. **Always**: commit and push after updating this file

```bash
# Update workflow
git pull
# ... do work ...
git add TASKS.md
git commit -m "TASKS: update - completed [task name]"
git push
```

---

## Issues / Blockers

| Issue | Reported by | Status |
|-------|-------------|--------|
| EOM_Solver.dll is Windows-only, can't test physics on Mac | Amir | Open — test at lab |
| Canopy mesh pivot is off-center (not at geometric center) | Amir | Workaround: use X offset in local position |
| com.unity.xr.oculus removed from manifest — was incompatible with Unity 6 on Mac | Amir (2026-05-20) | Sari: re-add on Windows build machine using a Unity 6-compatible version (try com.unity.xr.oculus 4.3.0+) or switch to Meta XR SDK |
| Horizon color mismatch | Sari (2026-05-31) | FIXED. Created Assets/Materials/SkyboxGrass.mat with _GroundColor baked in. Assign it in Window → Rendering → Lighting → Skybox Material. Runtime hack removed from GrassGround.cs. |

## Note for Sari's AI — next session

- Pull latest, open Integration_Ready.unity
- The scene is fully working on Mac (no VR). All scripts are live.
- VelocityArrows needs canopy Rigidbody wired once physics runs on Windows
- SkydiverHUD needs canopy Rigidbody wired in Inspector (drag Canopy_Rotated's Rigidbody into the slot)
- XR/Oculus packages were removed due to Unity 6 incompatibility — re-add compatible version on Windows build machine before Quest 2 build
