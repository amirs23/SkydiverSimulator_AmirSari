# TASKS — SkydiverSimulator
**Last updated: 2026-06-15 (Amir) + 2026-06-29 (Sari)**
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
| VR setup fully configured in Unity Editor | Sari (2026-05-19) | XR Plug-in Management → Android → Oculus checked, Quest 2 target set |
| HUD / UI overlay | Sari (2026-05-19) | SkydiverHUD.cs — shows ALT, SPD, HDG, BRK |
| Physics simulation (EOM_Solver) hookup | Amir (2026-05-19) | PlayerMovement.cs fixed: rotation bug, camera override removed, cross-platform guard |
| Velocity/heading arrows | Amir (2026-05-19) | VelocityArrows.cs |
| Wind/environment effects | Amir (2026-05-19) | WindEffect.cs |
| Landing zone marker | Amir (2026-05-19) | LandingZoneMarker.cs |
| Scene wired up and verified on Mac | Amir (2026-05-20) | Integration_Ready.unity scene: avatar animates, HUD working, wind particles visible |
| Destination arrow (nav indicator) | Sari (2026-05-31) | DestinationArrow.cs |
| Grass ground | Sari (2026-05-31) | GrassGround.cs — 2000m green plane + skybox horizon blend |
| Cloud layer (SkyGrid rewrite) | Sari (2026-05-31) | SkyGrid.cs rewritten — fluffy puff-sphere cloud clusters |
| Procedural ram-air canopy | Amir (2026-06-14) | ProceduralCanopy.cs — 9 cells each a different colour, full suspension line system (A/B/C/D per rib), slider, 4 risers, steering lines, pilot chute. Replaces Canopy_Rotated + SuspensionLines.cs. |
| Toggle arm animation | Amir (2026-06-14) | ToggleArmAnimation.cs — arms animate between raised (toggles) and pulled (steering) based on left/right toggle inputs. Disable when using XSens — XSens drives arms directly. |
| Expose LeftToggle / RightToggle | Amir (2026-06-14) | PlayerMovement.cs — added LeftToggle and RightToggle static properties + SetToggles() static method |
| Steering lines → real skinned hand bones | Sari (2026-06-16) | ProceduralCanopy now resolves to Hips-rooted `" 1"` bones (LeftCarpus 1/RightCarpus 1). Fixed the "cables pinned to torso" bug. See README FIXED section. |
| Toggle grip handles + line thickness | Sari (2026-06-16) | `showToggleHandles` grips in each hand at bottom of steering lines. `lineWidth` 0.006→0.035. |
| First/third-person camera switcher (code) | Sari (2026-06-17) | CameraViewController.cs on Main Camera. V key (Editor) / Quest B button. NOT yet tested on Quest. |
| Richer environment baker (code) | Sari (2026-06-17) | GroundEnvironment.cs — right-click "Generate Environment" → town + trees up to 2500m half-extent. NOT yet tuned/tested on Quest. |
| GroundEnvironment extended + fog fixed | Amir (2026-06-15) | Raised areaHalfExtent to 3000m, cameraFarClip to 5000m, fog pushed past map edge so environment is visible from any height. Town + tree counts increased for horizon fill. |
| Matlab-driven movement — UDP receiver (matched to REAL sim format) | Sari (2026-06-29) | SimulatorReceiver.cs (port 9764). Reads the lab's actual 26-field `x_s` packet (NED, 3-2-1 Euler deg). Tested against sim_out.mat via sim_replay.m. See HANDOFF. |

---

## In Progress

| Task | Assigned to | Notes |
|------|-------------|-------|
| Test camera switcher on Quest 2 | Sari | CameraViewController.cs works in Editor. Verify B button toggles, first-person eye position, third-person framing. |

---

## To Do — VR Parachute

| Task | Assigned to | Priority | Notes |
|------|-------------|----------|-------|
| Confirm position-Z sign convention with lab | TBD | HIGH | See HANDOFF — `positionZIsAltitude` flag currently ON; example data is internally inconsistent. Confirm with the lab team. |
| Arm ownership — XSens vs SimulatorReceiver | TBD | HIGH | XSens drives arm bones AND SimulatorReceiver feeds toggle values to ToggleArmAnimation. Both write the same bones → conflict. Decide: if XSens suit is active, disable ToggleArmAnimation; if no suit, enable it. |
| Verify SimulatorReceiver on lab Windows PC | TBD | HIGH | Confirm real EOM_Solver output matches the 26-field format. Test live with the lab stream. |
| Tune GroundEnvironment on Quest 2 | Amir | MED | Run on device, profile framerate. Lower tree/building counts if it dips below 72fps. Check GPU instancing is active on Quest. |
| Assign SkyboxGrass material in Unity | Sari | LOW | Window → Rendering → Lighting → Skybox Material → drag Assets/Materials/SkyboxGrass.mat |
| Remove DevColorize from Avatar GameObject | TBD | LOW | |

---

## To Do — Project 1 (AR Freefall)

> See the SkydiverAR repo (SariMorkos/SkydiverAR) — that project has its own README and task list.

---

## How AI Agents Should Use This File

1. **Before starting any session**: run `git pull` and read this file
2. **When you complete a task**: move it to the Completed section, add your name and today's date
3. **When you start a task**: move it to In Progress and add your name
4. **When you discover a bug or issue**: add it to a new section called "Issues / Blockers"
5. **Always**: commit and push after updating this file

```bash
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
| com.unity.xr.oculus removed from manifest — incompatible with Unity 6 on Mac | Amir (2026-05-20) | Sari: re-add on Windows build machine (try com.unity.xr.oculus 4.3.0+) or switch to Meta XR SDK |
| ProceduralCanopy Follow Target must be Hips bone, NOT Avatar root | Amir (2026-06-14) | The Avatar root doesn't move — XSens drives bones. Set Follow Target = Hips bone. RESOLVED in SimulatorReceiver path by following the avatar transform. |
| Avatar has duplicate bones — skinned `" 1"` copies vs flat `j*` copies | Sari (2026-06-16) | All bone references MUST use the Hips-rooted `" 1"` copies. ProceduralCanopy.FindSkinnedBone() handles this automatically. ToggleArmAnimation Inspector slots may need re-checking. |

---

## Note for next AI session

- Run `git pull`, open `Integration_Ready.unity`
- Read HANDOFF.md — it has the detailed state of SimulatorReceiver and the NED frame math
- **Most urgent** (from HANDOFF): confirm position-Z sign convention with lab + arm ownership decision
- GroundEnvironment is coded but not yet tested on Quest — if this session is on the lab machine, test framerate and tune counts if needed
- ToggleArmAnimation.cs — check if Inspector slots point to the `" 1"` (skinned) bones or the flat `j*` nodes (non-moving); re-wire if needed
