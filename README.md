# SkydiverSimulator — VR Parachute Simulator (Meta Quest 2)

A VR parachute simulator. A skydiver avatar flies under a procedurally-built ram-air
canopy and is viewed in a Meta Quest 2. The avatar's **body pose** is driven by **XSens
motion-capture data** streamed from Matlab over UDP; the avatar's **world position and the
canopy's attitude** come from a **separate lab physics simulator**, also forwarded by
Matlab. Unity itself computes no physics — it is a renderer.

| | |
|---|---|
| **Institution** | Technion — supervised by **Dr. Anna Clarke** |
| **Authors** | Amir Said (amir.said@campus.technion.ac.il), Sari Morkos |
| **Unity version** | **6000.2.6f2** (exact) |
| **Render pipeline** | URP 17.2.0 |
| **Target** | Meta Quest 2 — standalone Android APK, sideloaded |
| **App package** | `com.Technion.SkydiverVR` |
| **Companion project** | AR Freefall Trainer (XREAL) — `SariMorkos/SkydiverAR` |

> **This project is the "VR Simulator" branch of Dr. Clarke's research programme.** It is
> not a continuation of an earlier student's system — there is no predecessor codebase.
> (`ProjectPresentation-Gilad.pptx`, if you come across it, is the programme's *funding
> proposal*, not a prior implementation.)

---

## Contents

1. [How the system works](#1-how-the-system-works)
2. [What you need before you start](#2-what-you-need-before-you-start)
3. [First-time setup](#3-first-time-setup)
4. [Running it in the Editor (no headset)](#4-running-it-in-the-editor-no-headset)
5. [Deploying to the Quest 2](#5-deploying-to-the-quest-2)
6. [Networking — the part that catches everyone](#6-networking--the-part-that-catches-everyone)
7. [What's in the project](#7-whats-in-the-project)
8. [The canopy — how it's built and how to change it](#8-the-canopy--how-its-built-and-how-to-change-it)
9. [Camera and views](#9-camera-and-views)
10. [Troubleshooting](#10-troubleshooting)
11. [Known issues and permanent notes](#11-known-issues-and-permanent-notes)
12. [Open tasks](#12-open-tasks)
13. [Git workflow](#13-git-workflow)

---

## 1. How the system works

**Two independent streams arrive over UDP, on two different ports, from two different
sources.** Keeping them straight is the single most important thing to understand here.

```
  Matlab (any computer)                         Unity (Editor or Quest 2)
  ─────────────────────                         ─────────────────────────

  STREAM 1 — body POSE only (joint rotations, never world position)

  session_tig100126-003.mvnx
        │  load_mvnx.m parses it
        ▼
  animate_to_unity.m
        │  MXTP02 packets (XSens MVN protocol, 23 body segments)
        │
        └────── UDP : 9763 ──────►  In the EDITOR:  XsensDevice (Live Capture plugin)
                                    In a BUILD:     XsensUDPReceiver.cs  ← different path!
                                                          │
                                                          ▼
                                                    Avatar skeleton (bone rotations)

  STREAM 2 — world POSITION + canopy attitude + toggles

  Lab physics simulator (external, not in this repo)
        │   or sim_replay.m  (replays sim_out.mat)
        │   or sim_to_unity.m (synthetic spiral — needs no data file)
        │  26-field ASCII line, NED frame, 3-2-1 Euler degrees
        │
        └────── UDP : 9764 ──────►  SimulatorReceiver.cs  (plain C# socket)
                                              │
                                              ▼
                                    Avatar world position, canopy attitude,
                                    steering-toggle values, HUD
```

**Unity is only a renderer.** It computes no aerodynamics. Two consequences:

- **Nothing moves until Matlab is streaming.** An idle scene almost always means the UDP
  stream isn't arriving, not that Unity is broken.
- **Pose and position are separate and must stay separate.** XSens rotates the skeleton in
  place; the simulator carries the avatar through the world. Anything that attaches to the
  body (suspension lines, steering lines) must sample live bone positions every
  `LateUpdate` rather than assume a fixed avatar location — see
  [§8](#8-the-canopy--how-its-built-and-how-to-change-it).

### ⚠️ The pose path is different in the Editor and in a build

This surprises everyone, so it is stated up front:

| | Editor | Standalone build (Quest APK) |
|---|---|---|
| **Pose (9763)** | `XsensDevice` — the Live Capture plugin | **`XsensUDPReceiver.cs`** — a plain UDP socket |
| **Position (9764)** | `SimulatorReceiver.cs` | `SimulatorReceiver.cs` (same) |

**The XSens plugin cannot work in a build — this is by design, not a bug.** It receives
data through a Live Capture *Connection*, and in
`com.unity.live-capture/Runtime/Core/Communication/ConnectionManager.cs` the only call that
loads saved connections is wrapped in `#if UNITY_EDITOR`. In a player build that line is
compiled out and the manager is constructed **empty**, so nothing ever binds port 9763.
Connections are also stored in `UserSettings/LiveCapture/ConnectionManager.asset`, a
per-user folder that is never included in a build. Live Capture is an Editor
virtual-production tool; players were never in scope.

`XsensUDPReceiver.cs` exists to fill that gap. It is on the **`XsensUDP`** object in the
scene with **Mode = BuildsOnly**, so the plugin owns the Editor and the socket owns the
APK. **Never run both at once** — they write the same bones and would fight for port 9763.

---

## 2. What you need before you start

**Software**

| | |
|---|---|
| **Unity 6000.2.6f2** | Exact version. Install via Unity Hub **with the Android Build Support module** (includes OpenJDK + Android SDK/NDK — you need these for the Quest build and for `adb`). |
| **Matlab** | Any recent version. **No toolboxes required** — the scripts use Java UDP sockets directly. |
| **Git** | To clone this repo. |

**Hardware** — only for the headset demo. Sections 3–4 work on any Mac or PC with nothing
attached.

- Meta Quest 2 + USB-C cable
- A private network both the Quest and the Matlab computer can join — see
  [§6](#6-networking--the-part-that-catches-everyone)
- A **Windows** PC only if you want the built-in `EOM_Solver.dll` physics — see
  [§7](#7-whats-in-the-project). Not needed for the simulator-driven demo.

> ✅ **A fresh clone opens and runs.** Everything needed is either in the repo or fetched
> automatically. There is no missing SDK to hunt down. (The companion AR project has such a
> blocker; this one does not.) Total repo size is ~50 MB.

---

## 3. First-time setup

```bash
git clone https://github.com/amirs23/SkydiverSimulator_AmirSari.git
cd SkydiverSimulator_AmirSari
```

**Open the project:**

1. Unity Hub → **Add** → select the cloned folder.
2. Open with **6000.2.6f2**. If Hub offers a different version, install 6000.2.6f2 first —
   **do not upgrade the project.**
3. First open takes several minutes (package resolution + shader/asset import). Let it
   finish.
4. Open the scene: **`Assets/Integration_Ready.unity`**. This is the scene — ignore
   `Assets/Scenes/SampleScene.unity`, which is an unused Unity template leftover.

Fetched or embedded automatically — nothing to install by hand:

| Package | Source |
|---|---|
| `com.movella.xsens` 2023.0.0 | **embedded** in `Packages/` |
| `com.unity.live-capture` 4.0.0-pre.3 | Unity registry — **required dependency** of the XSens plugin |
| `com.unity.xr.management` 4.6.0 | Unity registry |
| `com.unity.xr.oculus` 4.2.0 | Unity registry — the Quest build path |

---

## 4. Running it in the Editor (no headset)

The fastest way to confirm the project is set up correctly. Everything runs on one machine.

**Step 1 — check the XSens connection** (Editor pose path).
`Window → Live Capture → Connections`. There should be an **Xsens Connection** on port
**9763**, showing **connected**. If not, select it and click **Connect**.

> This is the most common reason "nothing happens" in the Editor. No connection = no pose.
> (It is also why the plugin can't work on the headset — see
> [§1](#1-how-the-system-works).)

**Step 2 — press Play.** The avatar hangs under the canopy, frozen. That's correct —
`SimulatorReceiver` deliberately freezes the scene until the first packet arrives.

**Step 3 — start a stream.** Either or both, from the `Matlab/` folder:

```matlab
sim_to_unity     % synthetic banked spiral descent — needs NO data file. Best first test.
sim_replay       % replays the real sim_out.mat recording (ask Sari for the file)
animate_to_unity % XSens body pose from the recorded .mvnx session
```

All three default to `127.0.0.1`, which is correct for Editor-on-this-machine.

**What you should see:** with `sim_to_unity`, the avatar descends in a circle, the canopy
banks into the turn, and the arms pull down alternately as the toggles oscillate. With
`animate_to_unity`, the body pose animates.

Press **V** to toggle first/third person.

---

## 5. Deploying to the Quest 2

### 5a. One-time headset setup

1. Enable **Developer Mode**: in the **Meta Horizon** phone app → your headset →
   **Headset Settings → Developer Mode → On**. Reboot the headset. (Requires a free
   verified developer account at `developers.meta.com`.)
2. Plug the Quest into the computer with USB-C.
3. **Put the headset on** and accept the **"Allow USB debugging?"** prompt — tick
   **Always allow from this computer**.

Confirm the computer sees it. `adb` ships with Unity's Android module; it is usually not on
your PATH:

```bash
# macOS
/Applications/Unity/Hub/Editor/6000.2.6f2/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb devices -l
```

You want one device listed. **`unauthorized` means you missed the prompt in step 3** — put
the headset back on and accept it. An empty list means Developer Mode is off or the cable
is charge-only.

### 5b. Build

1. **File → Build Settings** → Platform **Android** → **Switch Platform** if needed (the
   first switch re-imports every asset and takes a while).
2. Scenes in build: **`Assets/Integration_Ready`** only.
3. **Texture Compression: ASTC** — the Quest's native format; the default is slower to
   build and larger.
4. **Run Device**: your Quest.
5. **Build And Run**.

After the first install the app also lives in the headset library under **Unknown Sources**.

### 5c. Stream to the headset

The Quest is now a separate machine, so `127.0.0.1` will not reach it. Point Matlab at the
**headset's IP** (headset: Settings → Wi-Fi → tap the network → IP address).

Each sender has a commented device-IP line right below the default — swap which one is
active:

```matlab
host = '127.0.0.1';         %if running in the Editor on this pc
%host = '192.168.68.100';   %if running on the device — replace with YOUR headset's IP
```

`animate_to_unity.m` uses `address = java.net.InetAddress.getByName(...)` in the same shape.

> The example IPs are **not fixed addresses** — they are whatever your network handed out
> last time. Re-check the headset's IP whenever it changes network.

**Watch the on-device log** — this is how you debug anything on the headset:

```bash
<adb-path> logcat -s Unity
```

You want to see `[SimulatorReceiver] Listening on UDP 9764` and
`[XsensUDP] Listening on UDP 9763`, then `First packet received`.

---

## 6. Networking — the part that catches everyone

Both streams are **plain UDP** from the Matlab computer to the headset. The two devices
must be able to address each other directly.

### ❌ The Technion campus Wi-Fi will not work

It has **client isolation** — devices on it cannot reach each other. Packets leave Matlab
and are silently dropped. Nothing errors; the scene just never moves. This is the most
confusing failure mode in the project.

### ✅ Use a phone hotspot or any private network

1. Turn on a personal hotspot (any phone).
2. Connect **both** the Quest and the Matlab computer to it.
3. **Re-check the Quest's IP — it changes with the network** — and update the sender.
4. Run the script.

Any network you control works: home router, travel router, laptop hotspot. The only
requirement is that UDP can flow between the two devices.

### Checking

```bash
ping <quest-ip>          # from the Matlab computer
```

No reply means the network is the problem, not the app. If ping works but nothing moves,
check `logcat` (§5c) to see whether packets are arriving.

---

## 7. What's in the project

### Scene — `Assets/Integration_Ready.unity`

| GameObject | Role |
|---|---|
| `Avatar` | The skydiver. XSens sample FBX, **Humanoid** rig. Carries `XsensDevice` (the Editor pose path) + `Animator`. |
| `Parachute` | `ProceduralCanopy` — the whole canopy, built in code. See [§8](#8-the-canopy--how-its-built-and-how-to-change-it). |
| `PhysicsController` | `SimulatorReceiver` (UDP 9764) + `PlayerMovement`. |
| `XsensUDP` | `XsensUDPReceiver` — the **build** pose path (UDP 9763). Mode = BuildsOnly. |
| `Main Camera` | `VRCameraRig` (head tracking) + `CameraViewController` (view toggle) + `CameraFollow` + `SkydiverHUD`. |
| `Environment` | `GroundEnvironment` — town + tree fields, baked at edit time. |
| `Plane` | `GrassGround` — 2000 m green ground plane. |
| `Lighting` | `SceneLighting` — one-stop daytime sun/ambient setup. |
| `SkyGrid` / `GridManager1–4` | Cloud clusters that follow the avatar in XZ. |
| `WindEffect`, `VelocityArrows`, `LandingZone`, `DestinationArrow` | Visual aids. |

### Scripts — `Assets/`

| Script | Where | What it does |
|---|---|---|
| `ProceduralCanopy.cs` | `Parachute` | Builds the entire canopy in code — cells, slider, suspension lines, steering cascade, risers, pilot chute. **Read [§8](#8-the-canopy--how-its-built-and-how-to-change-it) before editing.** |
| `SimulatorReceiver.cs` | `PhysicsController` | UDP 9764. Reads the simulator's 26-field `x_s` packet (NED, 3-2-1 Euler deg), positions the avatar, drives canopy attitude + toggles + HUD. Freezes the scene until the first packet; re-freezes if the stream stops >0.5 s. |
| `XsensUDPReceiver.cs` | `XsensUDP` | UDP 9763, **builds only**. Plain socket MXTP02 parser — the pose path the plugin can't provide in an APK. Rotation-only by design. |
| `CameraViewController.cs` | `Main Camera` | First/third-person. **V** (Editor) / Quest **B** button / `Instance.SetView(bool)`. |
| `VRCameraRig.cs` | `Main Camera` | Quest head tracking. Falls back to desktop mode if no HMD appears within 5 s. |
| `PlayerMovement.cs` | `PhysicsController` | The **built-in** physics path via `EOM_Solver.dll` — **Windows only** (`#if UNITY_STANDALONE_WIN`); skipped on Mac/Quest. Exposes `LeftToggle`/`RightToggle`/`SetToggles()`, which `SimulatorReceiver` drives when the lab stream is live. |
| `GroundEnvironment.cs` | `Environment` | Edit-time baker: right-click → **Generate Environment** → town + trees into a saved `EnvironmentBaked` child (nothing is generated at runtime). Raises the camera far-clip to 5000 m and pushes fog past the map edge so the ground stays visible from altitude. **The scene is currently baked at `areaHalfExtent: 900` (a 1.8 × 1.8 km patch), 280 buildings, 1400 trees** — note the *script defaults* are 3000 m / larger counts, and the scene overrides them. Re-bake after changing any of it. See [§12](#12-open-tasks) #6. |
| `SceneLighting.cs` | `Lighting` | Sun, soft shadows, gradient ambient. |
| `ToggleArmAnimation.cs` | *not attached* | Swings the arms to match toggle input. **Conflicts with XSens**, which drives the same bones — see [§12](#12-open-tasks). |
| `SuspensionLines.cs`, `Canopy_Rotated.obj` | *unused* | **Legacy**, superseded by `ProceduralCanopy`. Kept for reference. |

### Matlab — `Matlab/`

| File | Purpose |
|---|---|
| `sim_to_unity.m` | **Synthetic** banked spiral descent → UDP 9764. Needs no data file — the best first end-to-end test. Sends the current 26-field format. |
| `sim_replay.m` | Replays the real `sim_out.mat` recording → UDP 9764 at 100 Hz. |
| `animate_to_unity.m` | Streams the recorded `.mvnx` body pose → UDP 9763 (MXTP02). |
| `load_mvnx.m` | Parses the `.mvnx` recording. |
| `session_tig100126-003.mvnx` | The recorded mocap session (~4 MB), committed. |
| `sim_out.mat` | **Gitignored** — a per-machine lab recording. Ask Sari for it, or use `sim_to_unity.m` instead, which needs nothing. |

No Matlab toolboxes needed — all three senders use `java.net.DatagramSocket` directly.

### Native

`Assets/Plugins/EOM_Solver.dll` — the lab aerodynamics engine, **Windows-only**. It is the
alternative to the simulator stream: `PlayerMovement` calls it on a Windows standalone
build. On Mac and on the Quest it is skipped and the state passes through unchanged, which
is why the **simulator stream (9764) is the path that works everywhere**.

---

## 8. The canopy — how it's built and how to change it

`ProceduralCanopy.cs` builds the whole parachute in code. Nothing is modelled by hand.

### ⚠️ The static parts are BAKED — changing the script does nothing on its own

`Start()` **skips** `BuildStatic` whenever a `CanopyBaked` child exists. So editing the
script or its Inspector values has **no visible effect** until you re-bake:

1. **Stop Play mode.** (Baking in Play mode looks like it worked and is **silently
   discarded** on Stop.)
2. Select `Parachute` → right-click the **ProceduralCanopy** component header →
   **Bake Canopy (static parts → scene)**.
3. **Cmd+S / Ctrl+S.**
4. Confirm with `git status` that `Integration_Ready.unity` actually changed.

> **The APK is built from the scene *file*, not from your Editor session.** An unsaved bake
> means the Editor looks right while the headset stays wrong. That exact trap cost a full
> session on 2026-07-16. The `git status` check is what makes it visible.

**Static** (baked, rides rigidly with the canopy): cells, slider, suspension lines, steering
cascade, pilot chute. **Dynamic** (rebuilt every `LateUpdate`, in world space): the risers
and the steering line's toggle segment — because they attach to *moving bones*.

### The geometry, and the one bug worth knowing about

**The canopy's origin is its NOSE, not its centre.** `AttachLocal()` builds the chord as
`z = -chord * chordFrac`, so the geometry runs from `z = 0` (leading edge) back to
`z = -chord` (trailing edge). The true chord centre is at `z = -chord/2`.

Anything centred on `z = 0` is centred on the **pivot**, not the wing. That single fact
caused three long-standing bugs, all fixed on 2026-07-16:

- the slider's corners sat half a chord in front of the slider mesh, so the suspension lines
  never touched it and all leaned forward;
- the canopy hung a half-chord behind the pilot, **and rotated about its nose** — swinging
  the whole wing around him on every turn;
- the steering lines were faked as vertical cables because the real corners "didn't line up".

`_slCz` now carries the chord centre, and `LateUpdate` offsets the canopy by the *rotated*
chord-centre vector so it banks about its own centre. **If the geometry ever looks
off-centre again, check `_slCz` and that `LateUpdate` offset first** — don't reach for the
old "apply an X offset to the mesh" workaround, which is obsolete.

### Steering lines follow the real rig

Per Dr. Clarke's rig diagram: **trailing edge → rear slider grommet → toggle in the hand.**
`BuildSteeringCascade()` draws `steeringCascadeCount` (default 4) lines per side from the
trailing edge inward from each wingtip to the rear slider corner — static, baked. The
toggle segment stays **dynamic and anchored to the hand bone**, so with a mocap suit on it
reads as real steering.

### Inspector knobs

All slider dimensions are fractions, so they scale with the canopy. **They only take effect
on a re-bake** (see above).

| Field | Default | Meaning |
|---|---|---|
| `sliderDropFrac` | `0.30` | How far below the canopy the slider sits, as a fraction of span. Larger = lower. **Currently under review — see [§12](#12-open-tasks).** |
| `sliderHalfWidthFrac` | `0.18` | Slider half-width (fraction of span). |
| `sliderHalfDepthFrac` | `0.22` | Slider half-depth (fraction of chord). |
| `sliderChordCentreFrac` | `0.5` | Where the slider centres along the chord. `0.5` = the true centre. **This is the knob that fixes "slider not centred"** — before it existed, the corners were centred on the nose. |
| `steeringCascadeCount` | `4` | Trailing-edge attachment points per side. Real rigs use ~4. |

### ⚠️ The avatar has duplicate bones

The imported avatar contains **two copies of every arm/hand bone**: the real **skinned**
ones nested under `Hips` and suffixed `" 1"` by Unity (e.g. `LeftCarpus 1`), and flat
**non-moving** reference nodes at the rig root (`LeftCarpus`, `jLeftWrist`) that sit at the
torso and never move.

**Anything that attaches to a bone must resolve to the Hips-rooted `" 1"` copy.**
`ProceduralCanopy.FindSkinnedBone()` does this (it ignores a trailing `" N"` and prefers the
copy with a `Hips` ancestor), and `XsensUDPReceiver` mirrors the same logic. A plain
`transform.Find("LeftCarpus")` binds the dead copy — packets arrive perfectly and nothing
visibly moves, which is a miserable thing to debug. This was the original "steering lines
pinned to the torso" bug.

---

## 9. Camera and views

`CameraViewController.cs` on `Main Camera` owns the view. `VRCameraRig` stays active in
both modes, so head-tracked look-around always works.

| | |
|---|---|
| **Third person** | Pulls back to `thirdPersonOffset` — currently `{x: -4, y: 7, z: -13}` in the scene, framing the canopy from above and behind. (The script's own default is `(0, 2, -14)`; the **scene value wins**. Change it in the Inspector, not the code.) |
| **First person** | Follows `firstPersonBone` — the Hips-rooted `Head 1` bone. |

**Toggle:** **V** in the Editor · the Quest **B** button on device ·
`CameraViewController.Instance.SetView(bool)` from code (exposed for a future UDP flag).

`firstPersonEuler` is an Editor-only convenience — in VR the HMD drives rotation, so it is
ignored on device.

---

## 10. Troubleshooting

**Nothing moves.** Work down in order — the cause is almost always near the top.

1. **Is Matlab actually streaming?** The scene freezes deliberately until the first packet.
   Start with `sim_to_unity` — it needs no data file.
2. **In the Editor: is the Xsens Connection green?** `Window → Live Capture → Connections`,
   port 9763. Only affects pose, not position.
3. **On the headset: is the IP right?** It changes every time the Quest joins a new network.
4. **On the headset: is it the right network?** Campus Wi-Fi silently drops the packets —
   see [§6](#6-networking--the-part-that-catches-everyone). `ping <quest-ip>` to confirm.
5. **Check `logcat`** (§5c) to see whether packets are arriving at all. This distinguishes
   "the network is eating them" from "Unity isn't handling them".

**The body pose won't animate on the headset (but the world motion works).**
Expected if `XsensUDPReceiver` isn't set up: the plugin **cannot** work in a build. Confirm
the `XsensUDP` object is in the scene with **Mode = BuildsOnly** and `Avatar Root` assigned.
See [§1](#1-how-the-system-works).

**The headset shows a flat floating window instead of VR.**
XR failed to initialise. Check **Edit → Project Settings → XR Plug-in Management → Android →
Oculus** is ticked, **and** that `ProjectSettings.asset`'s `preloadedAssets` still lists
`XRGeneralSettingsPerBuildTarget.asset` and `OculusSettings.asset`. Unity silently strips
those if the Oculus package ever goes missing, and without them XR never starts. This has
happened once already.

**The canopy looks wrong after I changed something.**
You probably didn't re-bake, or you baked in Play mode. See
[§8](#8-the-canopy--how-its-built-and-how-to-change-it).

**`adb devices` shows `unauthorized`.** You missed the "Allow USB debugging?" prompt inside
the headset. Put it on and accept it.

**`adb devices` is empty.** Developer Mode is off, or the cable is charge-only.

> ⚠️ **Don't start debugging the coordinate maths.** The instinct when motion looks wrong is
> that Unity and Matlab/XSens disagree on coordinates. On the companion AR project that
> hypothesis was investigated and **ruled out**; a conjugate-quaternion "fix" was tried and
> **reverted**. The real cause was the rig type. The conversion used here —
> `Vector3(-py, pz, px)` · `Quaternion(qy, -qz, -qx, qw)` — is the one that was verified
> working on-device. Check the Editor state before the algebra.

---

## 11. Known issues and permanent notes

- **The XSens plugin cannot work in a build.** Not a bug, not fixable by configuration —
  Live Capture is Editor-only by construction. `XsensUDPReceiver.cs` is the build path.
  See [§1](#1-how-the-system-works).
- **Never run the plugin and `XsensUDPReceiver` at once.** Both write the same bones and
  both want port 9763. `Mode = BuildsOnly` keeps them apart — leave it.
- **Re-bake in Edit mode, save, and verify with `git status`.** A bake that lives only in a
  Play-mode session or an unsaved Editor is a bake that doesn't exist. The APK reads the
  file.
- **`com.unity.xr.oculus` keeps getting removed.** It has been dropped from
  `manifest.json` more than once because it is old (`4.2.0` declares `unity: 2022.3`) and
  awkward on Mac. **Without it there is no Quest build at all.** It currently resolves fine
  alongside `xr.management 4.6.0`. If it needs replacing, the candidates are
  `com.unity.xr.oculus` 4.3.0+ or the Meta XR SDK — but replace it, don't just delete it.
- **`EOM_Solver.dll` is Windows-only.** The simulator stream on 9764 is the cross-platform
  path and the one the project actually runs on.
- **Technion Wi-Fi blocks device-to-device traffic.** Use a hotspot.
  See [§6](#6-networking--the-part-that-catches-everyone).
- **Bone attachments must use the Hips-rooted `" 1"` bones.** See
  [§8](#8-the-canopy--how-its-built-and-how-to-change-it).
- **Doc-drift warning.** This README has previously recorded a fix in one section while
  leaving the contradicting claim live in another — and once described the project as built
  on a previous student's system, which it is not. **When a fix lands, update every section
  it touches in the same commit.**

---

## 12. Open tasks

| # | Task | Where |
|---|---|---|
| 1 | **Slider height.** Dr. Clarke's rig diagram shows the slider **low, just above the risers** — where it ends up on a fully-open canopy. Ours sits at `sliderDropFrac = 0.30`. Raise it and re-bake (§8). Must be judged **by eye**: the riser height can't be computed offline, because the Inspector-assigned bones are the flat non-moving copies and the real ones only exist at runtime. This deliberately **overrides** HANDOFF's older "roughly halfway down the lines" spec. | 🏠 any machine |
| 2 | **Confirm the position-Z sign convention with the lab.** `SimulatorReceiver.positionZIsAltitude` is currently **ON**; the example data is internally inconsistent. Needs a human answer from the simulator's authors. | 💬 lab |
| 3 | **Arm ownership — XSens vs `ToggleArmAnimation`.** Both write the same arm bones. Decide the rule: if the suit is live, XSens wins and `ToggleArmAnimation` must be disabled; with no suit, enable it so the toggles are visible. Currently unattached. | 🏠 any machine |
| 4 | **Verify `SimulatorReceiver` against the real `EOM_Solver` output** on the lab Windows PC — confirm the live stream really matches the 26-field format. | 🔬 lab PC |
| 5 | **Canopy and avatar headings come from different sources.** The canopy's yaw comes from the **simulator**; the avatar's comes from **XSens**. Nothing ties the two reference frames together, so in `.mvnx` replay they can sit ~180° apart and the steering lines cross. `SimulatorReceiver` already has `headingOffsetDeg` / `invertHeading` for the sim side. Related to #3. | 🔬 needs the suit |
| 6 | **`GroundEnvironment` — reconcile the area, then tune on the Quest.** The stated goal is an environment *visible from any altitude and filling the horizon*. The script defaults to `areaHalfExtent: 3000` (6 × 6 km), but **the scene is baked at `900`** — a 1.8 km patch that will not fill the horizon from 500 m up. Decide the real target, re-bake (§8), then profile on-device: lower building/tree counts if it drops below 72 fps, and confirm GPU instancing. | 🔬 headset |
| 7 | **Verify the camera switcher on the Quest** — B button toggles, first-person eye position, third-person framing. | 🔬 headset |
| 8 | **Remove `DevColorize` from the Avatar** — a development helper left attached. | 🏠 any machine |

> 🏠 = any machine · 🔬 = needs hardware · 💬 = needs a conversation

**Not open, so nobody re-opens them:** the slider centring, the canopy hanging behind the
pilot, the steering-line routing, and on-device body pose are all **done and verified**
(2026-07-16).

---

## 13. Git workflow

```bash
git pull                 # always start here — more than one person pushes to this repo
# ... do work ...
git add .
git commit -m "description"
git push
```

**Always `git pull` before starting.** Two people push here and `Integration_Ready.unity`
conflicts badly if it diverges.

**Always save and commit the scene when it works.** The scene holds the canopy bake, the
XSens wiring, and every Inspector value. A state that lives only in someone's working tree
is a state that regresses — that has already happened on this project more than once.

**Don't commit build output.** `.gitignore` covers the APK and Unity's
`*_BackUpThisFolder_ButDontShipItWithYourGame/` (~645 MB) — both are far over GitHub's
100 MB per-file limit.

`HANDOFF.md` is a running session log with deeper technical detail (wire formats, frame
math, session-by-session history). `TASKS.md` is the live task list. **This README is the
place to start.**
