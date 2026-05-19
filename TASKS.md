# TASKS — SkydiverSimulator
**Last updated: 2026-05-19 (Sari)**
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

---

## In Progress

| Task | Assigned to | Status |
|------|-------------|--------|
| Canopy position fine-tuning | Amir | Positioning canopy above avatar head |
| Suspension lines visual polish | Amir | Adjusting spread and attachment points |

---

## To Do — Project 2 (VR Parachute)

| Task | Assigned to | Priority | Notes |
|------|-------------|----------|-------|
| Physics simulation (EOM_Solver) hookup | Amir | HIGH | Windows lab PC only, needs DllImport working |
| Physics simulation (EOM_Solver) hookup | Amir | HIGH | Windows lab PC only, needs DllImport working |
| HUD / UI overlay | Sari | MEDIUM | Show altitude, speed, heading on screen |

| Velocity/heading arrows | Sari | MEDIUM | Visual arrows showing direction of travel |
| Wind/environment effects | TBD | LOW | |
| Landing zone marker | TBD | LOW | Target on ground the skydiver aims for |

---

## To Do — Project 1 (AR Freefall)

| Task | Assigned to | Priority | Notes |
|------|-------------|----------|-------|
| Create new Unity project | TBD | HIGH | Separate project from this one |
| Install XREAL SDK | TBD | HIGH | For XREAL Air2 Ultra goggles |
| Set up freefall scene | TBD | HIGH | Avatar falling through sky |
| Connect XSens to new project | TBD | HIGH | Same pipeline as Project 2 |

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
