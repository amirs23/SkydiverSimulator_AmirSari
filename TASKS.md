# TASKS — SkydiverSimulator
**Last updated: 2026-07-16 (Sari).** Merged Amir's 2026-07-02..06 push (lighting, skybox, Matlab sender fixes). Then **found and fixed the canopy's long-standing root bug: its origin is the NOSE, not the chord centre** — which was causing three separate symptoms (slider/line mismatch, forward-leaning lines, canopy hanging 1.1 m aft and swinging on turns). Also **rerouted the steering lines** through the rear slider grommets to match Anna's rig diagram. Re-added `com.unity.xr.oculus`. See Completed + Issues.

> ⚠️ **`ProceduralCanopy` static geometry is BAKED into the scene.** Changing the script or its Inspector values does **nothing** on its own — `Start()` skips `BuildStatic` whenever a `CanopyBaked` container exists. After any change: **stop Play**, right-click the component header → *Bake Canopy (static parts → scene)*, then **Cmd+S**. Baking while in Play mode looks like it worked and is silently discarded on Stop — this cost us two rounds on 2026-07-16.
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
| Daytime lighting + sky/grass colour match | Amir (2026-07-04..06) | SceneLighting.cs — one-stop daytime setup (sun, soft shadows, gradient ambient). SkyboxGrass assigned in-scene; green horizon matches the grass plane; blue sky tint. |
| Assign SkyboxGrass material in Unity | Amir (2026-07-04) | Done in-scene (`e80bf3c`) — closes the LOW task that was assigned to Sari. |
| **Slider position + suspension-line convergence** | Sari (2026-07-16) | **Root cause: the canopy's origin is its NOSE, not its centre.** `AttachLocal()` builds the chord as `z = -chord*chordFrac`, so geometry runs z = 0 (nose) → -2.2 (tail); the true chord centre is z = **-1.1**. But `SL_FL/FR/RL/RR()` centred the slider corners on **z = 0** — the *pivot*. Lines therefore converged **1.1 m (half a chord) in front of the slider mesh**, and the front corners sat at z = **+0.484**, *ahead of the leading edge*, so **all four rows leaned forward** (A/C 13.1°, B/D 19.2°) — Sari's "looks like `\|/` instead of `\/`". Fix: new `_slCz` chord-centre term; corners now z = -0.616 / -1.584, which are exactly the slider mesh's front/rear edges, and A/B bracket the front corner while C/D bracket the rear. `BuildSlider` now derives from the same `_slCz` instead of re-hardcoding `-chord*0.5f` — that duplication *was* the bug. Exposed `sliderDropFrac` / `sliderHalfWidthFrac` / `sliderHalfDepthFrac` / `sliderChordCentreFrac` as Inspector knobs. **Closes the old "canopy mesh pivot is off-center" issue.** |
| **Canopy hung 1.1 m behind the pilot (and swung on turns)** | Sari (2026-07-16) | Same nose-vs-centre root cause, third symptom. `followOffset` put the canopy's **origin (the nose)** above the avatar, so the wing sat 1.1 m aft — and because `SimulatorReceiver` drives `canopy.rotation` on the **same GameObject**, the canopy rotated **about its nose**, swinging the whole wing around the pilot on every turn (would have been obvious in the replay's ~44° turn). Fix in `LateUpdate`: offset by the **rotated** chord-centre vector so the chord centre lands on the follow point. Verified the centre now stays 0.00 m off the pilot at yaw 0/44/90/180 — it was 1.10 m off at *every* yaw, with the error direction swinging round. Execution order is safe: SimulatorReceiver writes rotation in `FixedUpdate`; this script is `[DefaultExecutionOrder(10000)]`. |
| **Steering lines — rerouted via rear slider grommets** | Sari (2026-07-16) | Now matches the real rig (**Anna's rig-manual diagram**, 2026-07-16): **trailing edge → rear slider grommet → toggle in hand.** New `BuildSteeringCascade()` (STATIC, bakes) draws `steeringCascadeCount` (default 4) lines per side from the trailing edge — `AttachLocal(x, 1f, midHeight:false)` — inward from each wingtip to the rear slider corner. The toggle segment stays **DYNAMIC** in `UpdateDynamicLines`, now starting at the rear grommet instead of on the canopy, still anchored to the resolved hand bone: **the re-route changed the top endpoint only; the hand-attach machinery was untouched**, exactly as specced. Deleted `SetSteerCable()` — the fake "cable hanging straight down from above the hand" hack, whose own comment admitted it was a workaround for corners that "don't line up with the hands" (that was the centring bug talking). `_steerLines` 4 → 2, **and the guard was updated to `< 2`** — it still read `< 4`, which would have early-returned every frame and silently frozen the risers *and* the steering lines. |

---

## In Progress

| Task | Assigned to | Notes |
|------|-------------|-------|
| Test camera switcher on Quest 2 | Sari | `CameraViewController.cs` works in the Editor. Verify: B button toggles views, first person sits at the eyes (tune `firstPersonOffset`), third person frames the whole canopy + pilot chute (tune `thirdPersonOffset`). HMD drives rotation in VR, so `firstPersonEuler` (Editor-only) is irrelevant on device. **← Sari 2026-07-16 asked for "a way to change POV while running the APK on the headset". ✅ It already exists:** `V` is Editor-only, but the same toggle is already bound to the Quest's **B button** (and `CameraViewController.Instance.SetView(bool)` is exposed for a future UDP flag). **Nothing to build — this task IS that task:** put the headset on and press B. If B doesn't fire on device, it's an Input System binding issue, not a missing feature. **Gated on the APK build** — see the `com.unity.xr.oculus` row in Issues. |

---

## To Do — VR Parachute

| Task | Assigned to | Priority | Notes |
|------|-------------|----------|-------|
| **Avatar won't animate on the Meta headset** | Sari | **HIGH** | ### 🔧 FIX WRITTEN 2026-07-16 — `Assets/XsensUDPReceiver.cs` (ported from AR). **Not yet wired into the scene, not yet tested on device.** Add the component, drag the Avatar root into `avatarRoot`, leave Mode = **BuildsOnly** (plugin keeps the Editor, socket takes the APK — never both, they write the same bones). Then Build & Run and check `adb logcat -s Unity` for `[XsensUDP] Listening on UDP 9763` → `First packet received`. Point `animate_to_unity.m` at the **headset's** IP. **Two deliberate departures from AR's version — do not "restore" them:** (1) **rotation only** — `applyHipsHeight` defaults OFF because SimulatorReceiver owns the avatar's world position in VR ("the simulator's CG channel carries the Avatar root through the world; XSens poses the limbs locally on top"); AR's Hips-position block would fight it every frame. (2) **bone resolution mirrors `ProceduralCanopy.FindSkinnedBone()`** — this avatar imports duplicate bones and the real skinned ones carry a `" 1"` suffix under Hips; AR's plain name lookup would silently bind the flat non-moving copies and nothing would appear to happen. **⚠️ Untested combination:** AR's receiver was verified on-device with a **Generic** rig; this avatar is **Humanoid** (`animationType: 3`). Direct `localRotation` writes do work here (the plugin does the same thing successfully in the Editor), but receiver+Humanoid is unproven. **If the pose comes out mirrored/rolled, the suspect is the single conversion line** `new Quaternion(qy, -qz, -qx, qw)` — the plugin instead composes `inverseParent * (orientation * tPoseRotation)`; the two conventions were never reconciled, only ported.<br><br>### ✅ ROOT CAUSE CONFIRMED AT SOURCE LEVEL 2026-07-16 — stop diagnosing, it's proven.<br>**The XSens plugin is Editor-only by construction and CANNOT work in a standalone build.** Proof, in `com.unity.live-capture`'s `Runtime/Core/Communication/ConnectionManager.cs`: the **only** call that loads saved connections is `InternalEditorUtility.LoadSerializedFileAndForget(k_AssetPath)` wrapped in **`#if UNITY_EDITOR`**. In a player build that line is compiled out, the `else` branch runs, and it constructs an **empty ConnectionManager with zero connections**. `WriteToFile()` is `#if UNITY_EDITOR` too, and `XsensConnection.OnEnable()` guards its client start the same way. On top of that, connections are persisted to **`k_AssetPath = "UserSettings/LiveCapture/ConnectionManager.asset"`** — a per-user, per-machine folder that is **never included in a player build** (and is normally gitignored). So in the APK there is no connection, nothing ever binds **9763**, and the avatar cannot animate. **Not a network issue, not an IP setting, not a rig/coordinate bug, not the suit.** Live Capture is a virtual-production tool for driving the *Editor* from external hardware; a standalone player was never in scope.<br>**Why the contrast is diagnostic:** sim replay = `SimulatorReceiver.cs`, a plain C# `UdpClient` on **9764** → ships in the build → **works on device**. XSens = Live Capture on **9763** → doesn't exist in the build → **never works on device**. Same headset, same Wi-Fi.<br>**FIX (only viable path):** drive the bones on-device from a plain UDP socket. **`ARproject/SkydiverAR/Assets/XsensUDPReceiver.cs`** (291 lines) is a proven, on-device-tested MXTP02 parser — plain `UdpClient(9763)`, segID→bone-name map, T-pose rotations. **Lucky break: it maps against `Avatar.FBX`, and this VR scene uses that same `Assets/Samples/Xsens/.../Avatar.FBX`.** Keep the plugin for Editor work (`m_IsLive`), use the socket in builds. **Not a copy-paste** — the plugin composes `inverseParent * (orientation * tPoseRotation)` whereas AR's receiver applies `new Quaternion(qy, -qz, -qx, qw)` against its own `_tPoseRots`; the two conventions must be reconciled and re-verified.<br><br>*Original 2026-07-16 report (kept for context):* wearing the Quest the avatar does not animate, but it **does** move under simulation replay. **That contrast is the whole diagnosis, and it rules out the network.** Both streams are UDP to the same device on the same Wi-Fi, but they take **different code paths**: sim replay = `SimulatorReceiver.cs`, a **plain C# UDP socket** on **9764** → works on device. XSens pose = the **`com.movella.xsens` plugin via `com.unity.live-capture`** on **9763** → doesn't. If the network were blocking device-to-device (a real, documented Technion-Wi-Fi issue), **both** would fail. Only the plugin path fails. **→ Leading hypothesis: the Live Capture connection is Editor-bound.** It's configured through **Window → Live Capture → Connections** — an *Editor window* — and that connection is not established in a standalone Android build, so on-device there is simply nothing listening on 9763. **Corroborating evidence from the AR project:** AR confirmed *on-device* avatar animation (2026-05-26) while using a **custom plain-UDP receiver** — the same approach that works here for the simulator. **Test to confirm (decisive, cheap):** on the headset, log whether any packet arrives on 9763 at all. Zero packets = Editor-bound connection confirmed (not a coordinate/rig problem). **If confirmed, the fix direction:** drive the bones on-device from a plain UDP socket instead of the Live Capture connection — the AR project's `XsensUDPReceiver.cs` is a proven, on-device-tested MXTP02 parser whose coordinate conversion is *already verified identical* to the plugin's. Keep the plugin for Editor work; use the socket path in builds. **Do not chase this as a rig or coordinate bug** — pose works fine in the Editor. |
| Confirm position-Z sign convention with lab | TBD | HIGH | See HANDOFF — `positionZIsAltitude` flag currently ON; example data is internally inconsistent. Confirm with the lab team. |
| Arm ownership — XSens vs SimulatorReceiver | TBD | HIGH | XSens drives arm bones AND SimulatorReceiver feeds toggle values to ToggleArmAnimation. Both write the same bones → conflict. Decide: if XSens suit is active, disable ToggleArmAnimation; if no suit, enable it. |
| Verify SimulatorReceiver on lab Windows PC | TBD | HIGH | Confirm real EOM_Solver output matches the 26-field format. Test live with the lab stream. |
| **Tune slider height to match the real rig** | Sari | MED | **Decision 2026-07-16 (Sari): follow Anna's rig-manual diagram over HANDOFF's written spec.** The diagram shows the slider **down at the bottom, just above the rapide links/toggles** — that's where a slider ends up on a fully-open canopy. HANDOFF's "roughly halfway down the lines" is therefore **overridden**. Knob is `sliderDropFrac` (fraction of span; default `0.30` ≈ 39% of the way down to the avatar). **Must be set by eye** — the riser/shoulder height can't be computed offline because the Inspector-assigned bones are the flat non-moving copies that sit at the rig root; the real skinned `" 1"` bones are only positioned at runtime. Set it, **re-bake, Cmd+S**. |
| Tune GroundEnvironment on Quest 2 | Amir | MED | Run on device, profile framerate. Lower tree/building counts if it dips below 72fps. Check GPU instancing is active on Quest. |
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
| ~~Canopy mesh pivot is off-center (not at geometric center)~~ | Amir | ✅ **RESOLVED 2026-07-16.** Confirmed real and fully diagnosed: the origin is the **nose** (geometry runs z = 0 → -chord), so anything centred on z = 0 is centred on the pivot, not the wing. It was causing **three** separate symptoms — slider/line mismatch, the forward-leaning `\|/` lines, and the canopy hanging 1.1 m aft and swinging on turns. All three fixed (see Completed). The old "use an X offset in local position" workaround is obsolete — **don't reintroduce it.** |
| **Canopy heading (sim) and avatar heading (XSens) are independent frames** | Sari (2026-07-16) | **Open — this is why the steering lines twist.** `SimulatorReceiver` drives `canopy.rotation` from the **sim's** yaw; the avatar's heading comes from the **XSens** stream (`XsensDevice` writes each bone's absolute orientation, incl. Pelvis — `XsensDevice.cs:429`). Nothing ties the two reference frames together. In `.mvnx` replay they're ~180° apart, so the avatar faces the camera and the lines cross. **Not an avatar bug:** with a live suit the avatar faces wherever the wearer faces — the 180° is an artefact of how the recorded subject was standing. **⚠️ Do NOT try to fix it by rotating the avatar root** — the plugin composes `inverseParent * absoluteOrientation`, so the parent cancels out exactly and rotating the root does **nothing**. Real fix is aligning the two heading references (`SimulatorReceiver` already has `headingOffsetDeg` / `invertHeading` for the sim side). Related to the arm-ownership row. |
| `m_Channels: 3` in this scene (Position + Rotation), vs `2` (rotation-only) in AR | Sari (2026-07-16) | **Open — unverified, noticed in passing.** The vault/HANDOFF describe the pose pipeline as *rotation-only* ("does not move the avatar through the world"), and AR deliberately runs `m_Channels: 2`. This scene's `XsensDevice` is set to **3**, so Pelvis **position** is streamed too (`XsensDevice.cs:408-410`). May be deliberate, may be drift. Worth a look before the lab test. |
| **`com.unity.xr.oculus` keeps getting removed from `manifest.json`** | Sari (2026-07-16) | **Open — needs a decision with Amir.** Removed again in the 2026-07-04 rewrite (and `xr.management` bumped 4.4.0 → 4.6.0). **Re-added at 4.2.0 on 2026-07-16** because without it there is no Quest APK build, and three open tasks are on-device tests. Note 4.2.0 declares `unity: 2022.3` — two majors behind this project's 6000.2 editor; it resolves and is cached, and it only needs `xr.management` **≥** 4.4.0 so 4.6.0 satisfies it. **This will keep ping-ponging until we agree**: Amir removes it (breaks his Mac), Sari needs it (Quest build). Options: guard it per-machine, move to Meta XR SDK, or try `com.unity.xr.oculus` 4.3.0+ for real Unity 6 support. |
| ProceduralCanopy Follow Target must be Hips bone, NOT Avatar root | Amir (2026-06-14) | The Avatar root doesn't move — XSens drives bones. Set Follow Target = Hips bone. RESOLVED in SimulatorReceiver path by following the avatar transform. |
| Avatar has duplicate bones — skinned `" 1"` copies vs flat `j*` copies | Sari (2026-06-16) | All bone references MUST use the Hips-rooted `" 1"` copies. ProceduralCanopy.FindSkinnedBone() handles this automatically. ToggleArmAnimation Inspector slots may need re-checking. |

---

## Note for next AI session

- Run `git pull`, open `Integration_Ready.unity`
- Read HANDOFF.md — it has the detailed state of SimulatorReceiver and the NED frame math
- **The canopy's static parts are BAKED** — see the warning at the top of this file before touching `ProceduralCanopy`. Bake in **Edit mode**, then **Cmd+S**, then confirm with `git status` that the scene actually changed.
- **The origin-is-the-nose bug is fixed — don't re-derive it.** If canopy geometry ever looks off-centre again, check `_slCz` / the `LateUpdate` chord-centre offset before suspecting anything else. The old "canopy pivot" workaround (X offset in local position) is obsolete.
- **Most urgent** (from HANDOFF): confirm position-Z sign convention with lab + arm ownership decision
- **Steering lines twisting is a heading-frame problem, not an avatar bug** — sim drives the canopy's yaw, XSens drives the avatar's. See Issues. Rotating the avatar root does nothing (the plugin cancels the parent out).
- GroundEnvironment is coded but not yet tested on Quest — if this session is on the lab machine, test framerate and tune counts if needed
- ToggleArmAnimation.cs — check if Inspector slots point to the `" 1"` (skinned) bones or the flat `j*` nodes (non-moving); re-wire if needed
