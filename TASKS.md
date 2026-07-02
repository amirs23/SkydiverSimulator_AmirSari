# TASKS — SkydiverSimulator
**Last updated: 2026-06-14 (Amir)**
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
| Destination arrow (nav indicator) | Sari (2026-05-31) | DestinationArrow.cs — arrow floats in front of avatar, points toward a draggable destination target. All fields Inspector-configurable. |
| Grass ground | Sari (2026-05-31) | GrassGround.cs — attach to Plane. Auto-scales plane to 2000x, applies green URP/Lit material. |
| Cloud layer (SkyGrid rewrite) | Sari (2026-05-31) | SkyGrid.cs rewritten — fluffy puff-sphere cloud clusters, fixed Y altitude, follows avatar in XZ. Avatar start height raised to 500m. |
| Procedural ram-air canopy | Amir (2026-06-14) | ProceduralCanopy.cs — 9 cells each a different colour, full suspension line system (A/B/C/D per rib), slider, 4 risers, steering lines, pilot chute. All generated at runtime, no external mesh needed. Replaces Canopy_Rotated + SuspensionLines.cs. See README for full setup. |
| Toggle arm animation | Amir (2026-06-14) | ToggleArmAnimation.cs — arms animate between raised (holding toggles) and pulled (steering) based on left/right toggle inputs. Works with Quest triggers and A/D/Space keyboard. Disable when using XSens suit — XSens drives arms directly. |
| Expose LeftToggle / RightToggle | Amir (2026-06-14) | PlayerMovement.cs — added LeftToggle and RightToggle static properties so ToggleArmAnimation and other scripts can read per-hand values. |

---

## In Progress

*(none)*

---

## To Do — Project 2 (VR Parachute)

| Task | Assigned to | Priority | Notes |
|------|-------------|----------|-------|
| Wire ProceduralCanopy in Unity Editor | Sari | HIGH | See "ProceduralCanopy scene setup" section in README. Disable old Canopy_Rotated + SuspensionLines.cs. |
| New UDP receiver for lab simulator data | TBD | HIGH | Replace PlayerMovement.cs + EOM_Solver.dll. Unity reads canopy/body position+velocity from lab Matlab script via UDP. Unity becomes pure renderer. |
| Arm animation driven by steering inputs from UDP | TBD | HIGH | When lab simulator sends left/right toggle values over UDP, feed them into ToggleArmAnimation instead of reading Quest triggers. |
| First/third person camera toggle | TBD | MEDIUM | Keyboard + Quest button + Matlab UDP signal. |
| Ground environment — trees/buildings | TBD | MEDIUM | Visible from altitude. |
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
| ProceduralCanopy Follow Target must be Hips bone, NOT Avatar root | Amir (2026-06-14) | The Avatar root doesn't move — XSens drives bones. Set Follow Target = Hips bone so canopy tracks avatar correctly. |

---

## Note for Sari's AI — next session

- Pull latest, open `Integration_Ready.unity`
- **Most important task**: wire up ProceduralCanopy in the Editor (see README "ProceduralCanopy scene setup" section)
  - Create empty GameObject → Add Component → ProceduralCanopy
  - Follow Target = **Hips bone** (not Avatar root)
  - Left/Right Shoulder + Hand bones → wire correct bones
  - Disable old `Canopy_Rotated` GameObject and `SuspensionLines` component
- ToggleArmAnimation.cs is for keyboard/Quest testing — **disable it when using XSens Matlab pipeline**
- XSens arm movement works automatically: Movella drives all bones including arms → ProceduralCanopy steering lines follow hand bones live
