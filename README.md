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
9. [Camera, views, and controls](#9-camera-views-and-controls)
10. [Troubleshooting](#10-troubleshooting)
11. [Permanent notes — read before changing anything](#11-permanent-notes--read-before-changing-anything)
12. [Git workflow](#12-git-workflow)

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

**The XSens plugin cannot work in a build — this is by design.** It receives
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
| `Avatar` | The skydiver. XSens sample FBX, **Humanoid** rig. Carries `XsensDevice` (the Editor pose path) + `Animator`, and `DevColorize` (a development tint helper — see the scripts table below). |
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
| `PlayerMovement.cs` | `PhysicsController` | **Legacy standalone physics — not the live path.** `SimulatorReceiver` disables it whenever the simulator stream is running, which is normal operation. See [§7 Native](#native). Still owns two things that *are* live: **restart** ([§9](#9-camera-views-and-controls)) and `startHeight` (default 500 m — lower it to test near the ground). Also exposes `LeftToggle`/`RightToggle`/`SetToggles()`, which `SimulatorReceiver` drives from the stream's `delta_l`/`delta_r`. |
| `DevColorize.cs` | `Avatar` (and possibly others) | **Development only** — tints objects a flat colour so they're easy to pick out while working. Remove the component from the Inspector when you want the real materials. |
| `Editor/FixXsensMaterials.cs` | Editor menu | **Tools → Fix Xsens Materials for URP.** Converts the XSens sample materials from the built-in Standard shader to URP/Lit. Run-once, already done — but see [§11](#11-permanent-notes--read-before-changing-anything) if the avatar ever renders invisible on the headset. |
| `GroundEnvironment.cs` | `Environment` | Edit-time baker: right-click → **Generate Environment** → town + trees into a saved `EnvironmentBaked` child (nothing is generated at runtime). Raises the camera far-clip to 5000 m and pushes fog past the map edge so the ground stays visible from altitude. **The scene is baked at `areaHalfExtent: 900`** — a 1.8 × 1.8 km patch, 280 buildings, 1400 trees. The *script defaults* are larger (3000 m); **the scene values win**. `areaHalfExtent`, the counts, and `seed` are all tunable — raise the extent for more ground coverage from altitude, lower the counts if the headset framerate drops. **Re-bake after changing any of it** (§8). |
| `SceneLighting.cs` | `Lighting` | Sun, soft shadows, gradient ambient. |
| `ToggleArmAnimation.cs` | *not attached* | Swings the arms to match toggle input, so steering is visible with no mocap suit. **Deliberately unattached: XSens drives the same arm bones**, so the two would write over each other. The rule is one or the other — with a suit live, XSens owns the arms and this stays off; with no suit, attach it to see the toggles move. |
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

**`EOM_Solver.dll` — legacy, not in use.**

> **This project does not use `EOM_Solver.dll`, and does not use `PlayerMovement`'s
> built-in physics.** All flight comes from **Dr. Clarke's team's simulator over UDP 9764**
> ([§1](#1-how-the-system-works)). This section exists only so nobody mistakes the leftover
> code for the live path.

`Assets/Plugins/EOM_Solver.dll` is an older, self-contained physics route that predates the
simulator integration. `PlayerMovement` still contains it behind
`#if UNITY_STANDALONE_WIN && !UNITY_EDITOR` (the DLL on a Windows player) with a pure-C#
approximation on every other platform. Neither runs in normal operation: **`SimulatorReceiver`
disables `PlayerMovement` as soon as the stream is live**, and the stream is the point of
the project.

It is kept, not deleted, because it is the only thing that animates the scene with **no
simulator running** — useful for a quick smoke test that the rig, HUD and canopy render.
Treat anything it produces as a placeholder, never as a result: **it is not the lab's
aerodynamics.**

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
> means the Editor looks right while the headset doesn't. The `git status` check in step 4
> is what makes that visible — don't skip it.

**Static** (baked, rides rigidly with the canopy): cells, slider, suspension lines, steering
cascade, pilot chute. **Dynamic** (rebuilt every `LateUpdate`, in world space): the risers
and the steering line's toggle segment — because they attach to *moving bones*.

### The geometry — the canopy's origin is its NOSE, not its centre

This is the single most important thing to know before you touch this script.

`AttachLocal()` builds the chord as `z = -chord * chordFrac`, so the geometry runs from
`z = 0` (leading edge) back to `z = -chord` (trailing edge). The true chord centre is at
`z = -chord/2` — **not** at zero.

**So anything you centre on `z = 0` is centred on the pivot, not on the wing.** Half a chord
is about 1.1 m on this canopy, which is enough to put the slider corners ahead of the
leading edge, hang the wing behind the pilot, and — because the canopy rotates about its
own origin — swing it around him on every turn.

Two pieces of the script exist to handle this, and both should be left alone:

- **`_slCz`** carries the chord centre, and every slider corner derives from it. `BuildSlider`
  reads the same `_slCz` rather than recomputing `-chord*0.5f` independently — one source,
  so the two can't disagree.
- **`LateUpdate`** offsets the canopy by the *rotated* chord-centre vector, so the centre
  lands on the follow point and the wing banks about itself. The centre holds 0.00 m from
  the pilot at every yaw.

**If the geometry ever looks off-centre, check `_slCz` and that `LateUpdate` offset first.**
Don't reach for an X offset on the mesh's local position — that treats the symptom and
fights the two mechanisms above.

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
| `sliderDropFrac` | `0.30` | How far below the canopy the slider sits, as a fraction of span. Larger = lower. **Set this by eye against Dr. Clarke's rig diagram**, which shows the slider low, just above the risers — where it ends up on a fully-open canopy. It can't be computed offline: the Inspector-assigned bones are the flat non-moving copies, and the real skinned `" 1"` bones only exist at runtime (see below). |
| `sliderHalfWidthFrac` | `0.18` | Slider half-width (fraction of span). |
| `sliderHalfDepthFrac` | `0.22` | Slider half-depth (fraction of chord). |
| `sliderChordCentreFrac` | `0.5` | Where the slider centres along the chord. `0.5` = the true chord centre, which is what you want — see the nose-vs-centre note above. |
| `steeringCascadeCount` | `4` | Trailing-edge attachment points per side. Real rigs use ~4. |

### ⚠️ The avatar has duplicate bones

The imported avatar contains **two copies of every arm/hand bone**: the real **skinned**
ones nested under `Hips` and suffixed `" 1"` by Unity (e.g. `LeftCarpus 1`), and flat
**non-moving** reference nodes at the rig root (`LeftCarpus`, `jLeftWrist`) that sit at the
torso and never move.

**Anything that attaches to a bone must resolve to the Hips-rooted `" 1"` copy.**
`ProceduralCanopy.FindSkinnedBone()` does this (it ignores a trailing `" N"` and prefers the
copy with a `Hips` ancestor), and `XsensUDPReceiver` mirrors the same logic. Use one of
those rather than writing a fresh lookup.

> A plain `transform.Find("LeftCarpus")` silently binds the dead copy. Everything looks
> correct — the object is found, packets arrive, no errors — and nothing moves, because
> you've attached to a node that sits at the torso and never animates. It is worth knowing
> the shape of this in advance, because nothing about it announces itself.

---

## 9. Camera, views, and controls

### Controls at a glance

| Action | Editor | Quest 2 |
|---|---|---|
| **Toggle first/third person** | **V** | **B** button (right controller) |
| **Restart the flight** | **R** | **A** button (right controller) |
| **Steering toggles** | — | **triggers** (left/right), when running the built-in C# physics |
| **Look around** | mouse-free; desktop fallback | head tracking (always on) |

Restart is `PlayerMovement.Restart()` — it resets the canopy and body to their start
positions and clears the `_landed` flag, so it works both mid-flight and after landing. The
toggle and restart are also callable from code (`CameraViewController.Instance.SetView(bool)`),
which is how a future Matlab UDP flag would drive the view.

> The triggers only steer when `PlayerMovement` is actually running. **While the simulator
> stream is live, `SimulatorReceiver` disables `PlayerMovement`**, so the toggles come from
> the stream's `delta_l`/`delta_r` instead and the physical triggers do nothing. That's
> correct — the lab owns the flight.

### View modes

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
those two entries if the Oculus package ever goes missing from the manifest, and without
them XR never starts.

**The canopy looks wrong after I changed something.**
You probably didn't re-bake, or you baked in Play mode. See
[§8](#8-the-canopy--how-its-built-and-how-to-change-it).

**`adb devices` shows `unauthorized`.** You missed the "Allow USB debugging?" prompt inside
the headset. Put it on and accept it.

**`adb devices` is empty.** Developer Mode is off, or the cable is charge-only.

> ⚠️ **Don't start rewriting the coordinate maths.** When motion looks wrong the instinct is
> that Unity and Matlab/XSens disagree on coordinates. **The conversion here is correct and
> verified on-device:** `Vector3(-py, pz, px)` · `Quaternion(qy, -qz, -qx, qw)`, matching the
> companion AR project. The same hypothesis was chased there, a conjugate-quaternion
> alternative was tried, and it had to be reverted — the cause was the **rig type**, not the
> algebra. **Check the Editor state first: rig type, bone bindings, connection.**

---

## 11. Permanent notes — read before changing anything

Each of these is a design decision that looks like an oversight from the outside. They are
here so the next person doesn't spend a day "correcting" something that is already right.

- **The XSens plugin cannot work in a build — this is by construction.** Live Capture is an
  Editor tool; players were never in scope. It is not fixable by configuration.
  `XsensUDPReceiver.cs` is the build path. See [§1](#1-how-the-system-works).
- **Never run the plugin and `XsensUDPReceiver` at once.** Both write the same bones and
  both want port 9763. `Mode = BuildsOnly` keeps them apart — leave it.
- **Re-bake in Edit mode, save, and verify with `git status`.** A bake that lives only in a
  Play-mode session or an unsaved Editor is a bake that doesn't exist. The APK reads the
  file.
- **`com.unity.xr.oculus` must stay in `manifest.json` — don't remove it.** It looks
  removable: `4.2.0` declares `unity: 2022.3`, two majors behind this editor, and it is
  awkward on Mac. **Without it there is no Quest build at all.** It resolves fine alongside
  `xr.management 4.6.0` (it only requires ≥ 4.4.0). If it ever needs replacing, the
  candidates are `com.unity.xr.oculus` 4.3.0+ or the Meta XR SDK — **replace it, don't just
  delete it**, and expect the XR preload entries to need restoring afterwards (see §10).
- **The flight comes from Dr. Clarke's team's simulator, over UDP 9764 — nothing else.**
  `EOM_Solver.dll` and `PlayerMovement`'s built-in physics are **legacy and not in use**;
  `SimulatorReceiver` disables them whenever the stream is live. Don't debug them, don't
  present their output as a result, and don't mistake them for the live path.
  See [§7 Native](#native).
- **Technion Wi-Fi blocks device-to-device traffic.** Use a hotspot.
  See [§6](#6-networking--the-part-that-catches-everyone).
- **Bone attachments must use the Hips-rooted `" 1"` bones.** See
  [§8](#8-the-canopy--how-its-built-and-how-to-change-it).
- **`m_Channels: 3` on `XsensDevice` is correct — don't "fix" it to `2`.** In the scene file
  it reads `m_Channels: 3` (`Position | Rotation`), while the companion AR project runs `2`
  (rotation-only) and everything here describes the pose path as rotation-only. The
  mismatch is not a problem: `3` is the **package's own default** (AR's `2` is the deliberate
  deviation), and with `applyRootMotion` off the plugin writes only the Pelvis's **local Y**
  (`XsensDevice.cs` ~line 418) — a local transform on a child bone. The Avatar **root**,
  which is what `SimulatorReceiver` positions, is never touched, so the authority split in
  [§1](#1-how-the-system-works) holds. It is **Editor-only** regardless: builds use
  `XsensUDPReceiver`. **Leave it as it is.**
- **If the avatar is invisible on the Quest, it's the materials — not the rig.** The XSens
  sample materials ship on the built-in **Standard** shader, which Unity **strips from
  Android URP builds**, so the avatar renders as nothing on device while looking fine in the
  Editor. Fix: **Tools → Fix Xsens Materials for URP** (`Assets/Editor/FixXsensMaterials.cs`)
  converts everything under `Assets/Samples/Xsens` to `Universal Render Pipeline/Lit`,
  preserving albedo. Already run — this note is here for the next time an avatar or material
  is added.
- **Quest head tracking is applied manually, on purpose.** Unity 6 no longer auto-applies
  the XR pose to a plain Main Camera. `VRCameraRig.cs` reads
  `InputDevices.GetDeviceAtXRNode(XRNode.Head)` every `LateUpdate` and writes the camera
  transform itself, using the camera's initial world position as a scene anchor plus the
  HMD's room-scale offset. **If head tracking ever "stops working", check that this script
  is alive before suspecting the headset** — and don't "simplify" it by deleting the manual
  write.
- **The canopy's heading and the avatar's heading come from different sources.** The canopy's
  yaw comes from the **simulator**; the avatar's comes from **XSens**. They are two
  independent reference frames with nothing tying them together, so in `.mvnx` replay they
  can sit ~180° apart. `SimulatorReceiver` exposes `headingOffsetDeg` and `invertHeading` to
  align the simulator side. **This is a frame-alignment knob, not an avatar problem** — with
  a live suit the avatar faces wherever the wearer faces. **Do not try to correct it by
  rotating the avatar root:** the plugin composes `inverseParent * absoluteOrientation`, so
  the parent cancels out exactly and rotating the root does nothing at all.
- **`SimulatorReceiver.positionZIsAltitude` is ON**, which makes the avatar descend
  correctly on the lab's example data. The example data reads `z` as altitude while `Vz` is
  down-positive, so the two conventions in it don't agree — the flag is what reconciles
  them. If absolute descent direction ever looks inverted against a new data source, this is
  the switch.
- **Keep every section in step.** This README is the only document here, and a fact recorded
  in one section while its contradiction stays live in another is worse than no note at all.
  **When something changes, update every section it touches in the same commit.**

---

## 12. Git workflow

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
XSens wiring, and every Inspector value — most of this project's real state lives in that
file rather than in code. A working state that exists only in one person's working tree
exists nowhere: every other machine, and every fresh clone, sees the old one.

**Don't commit build output.** `.gitignore` covers the APK and Unity's
`*_BackUpThisFolder_ButDontShipItWithYourGame/` (~645 MB) — both are far over GitHub's
100 MB per-file limit.

---

**This README is the only document in this repo, and it is meant to stay that way.** It
previously shared the job with a `HANDOFF.md` session log and a `TASKS.md` task list; both
were folded in here and removed on 2026-07-17. Three documents meant three homes for the
same fact, and they drifted apart. One document, kept current, is worth more than three that
disagree.

**Start here, and keep it here.**
