# Modeled Threads for Autodesk Inventor 2027

Cuts a **real, modelled thread** into any cylindrical face — a shaft, a boss, a
through hole or the wall of a blind hole. There is no cosmetic thread to create
first and nothing to convert: pick the face, choose a size, and the thread is
cut into the solid.

![A modelled M10x1.5 thread on a shaft](docs/screw-thread.png)

<p align="center">
  <img src="docs/nut-thread.png" width="420" alt="A hex nut with a modelled M10 internal thread">
</p>

## What it does

* **External and internal.** A face outside a hole gets a screw thread; a face
  inside one gets a nut thread, with the profile the standard gives for each.
* **Through and blind.** The ends of the face are read from the geometry: a free
  end, a shoulder, a closed hole bottom, or an end that runs off into another
  feature. Each is treated differently, so a flange, a shoulder, a hole bottom
  or a sealing face keeps its shape and its area.
* **A lead-in and a relief.** An open end gets the 45° lead-in cone; a shoulder
  gets a one-pitch relief so the thread runs out before it.
* **Multi-start, left or right hand, with clearance.** 1 to 8 starts, a lead of
  pitch × starts, and a per-thread clearance that takes material off the flanks
  as well as the root.

Everything is a normal Inventor feature: the thread is a coil cut plus two
revolved cuts, all named, all editable, all visible in the browser.

<p align="center">
  <img src="docs/tube-thread.png" width="520" alt="One tube, threaded M20 outside and M10 inside">
</p>

<p align="center"><i>One part, threaded twice: M20 × 2.5 on the outside and
M10 × 1.5 in the hole, with the wall between them untouched.</i></p>

## Requirements

| | |
|---|---|
| Inventor | 2027 (the interop is referenced from `refs/2027`) |
| Windows | 10 or 11, 64-bit |
| Runtime | .NET Framework 4.8 — already part of Windows, nothing to install |

## Installing

Three ways, all of them per user with no administrator rights:

1. **Double-click `install.bat`.** It copies `bundle\ModeledThreads.bundle` into
   `%APPDATA%\Autodesk\ApplicationPlugins`, where Inventor looks for add-ins.
2. **`powershell -ExecutionPolicy Bypass -File install.ps1`** — the same thing,
   with a hash printed at the end. Add `-Uninstall` to remove it.
3. **`ModeledThreads-Setup.exe`** — compile `installer.iss` with Inno Setup
   (`iscc installer.iss`) for a signed-able, one-click installer.

Inventor must be closed while installing: it holds the DLL open while it runs.

### The first run

Inventor asks once about any add-in it has not seen before:

> **Add-in Manager Security Alert** → *Launch Add-in Manager* → select
> **Modeled Threads** → **Unblock** → OK.

That is a one-off per user. Signing `ModeledThreads.dll` with a trusted
certificate removes the prompt (see [Signing](#signing)).

## Using it

1. Open a part and either **select a cylindrical face** or press **Create
   Thread** on the *Modeled Threads* tab (Inventor then asks for the face).
2. The dialog fills itself in for the face: whether it is a shaft or a hole, its
   radius, its length and what each end is. The suggested size is the one that
   fits the face.
3. Choose the standard and size, then any of:

| Field | What it does |
|---|---|
| Standard | ISO metric, trapezoidal, Acme, buttress, BSPP (G) or Rp |
| Designation | The size; type it or pick it. `M10`, `M10x1.25`, `Tr20x4`, `1/2-10 ACME`, `G 1/2`, `Rp 1/2` |
| Length | How much of the face to thread, in mm. Empty means all of it |
| Clearance | Extra material cut from the thread, in mm |
| Starts | 1 to 8; the lead becomes pitch × starts |
| Hand | Right or left |
| Lead-in | The 45° cone at the open end the thread starts at |
| Shoulder | The one-pitch relief at a shoulder |
| Direction | Start at the other end of the face |

4. **Create Thread** cuts it and closes the dialog.

If the size cannot be cut — a thread longer than the face, say — the dialog says
so and stays open. Nothing is cut until it can be cut properly.

<p align="center">
  <img src="docs/dialog.png" width="420" alt="The Create Thread dialog, filled in for a hole">
</p>

## What gets built

A thread is three features:

| Feature | What it is |
|---|---|
| `Modeled Thread <size>` | The groove, as a profile of the standard's own shape swept along a helix (a coil cut) |
| `Modeled Thread Lead-in <size>` | The 45° cone at the open end, so the first teeth engage (ISO 4753) |
| `Modeled Thread Relief <size>` | A one-pitch revolve at a shoulder, so the thread runs out cleanly |

The planes and axes the cuts are built on are made as **construction** features,
so they never appear in the browser or across the part: the only things the
thread leaves behind are its own features.

The profile is the standard's, not an approximation: for ISO metric it is the
ISO 68-1 basic profile (H = P√3/2, depth 5H/8, flat P/8 at the crest and P/4 at
the root), and the resulting minor diameter matches the ISO 261/262 tables. The
cut volume agrees with the analytic groove volume to better than 0.01%.

Clearance moves the whole groove sideways rather than only deepening it, so both
the flanks and the root make room: a shaft's teeth get thinner and its root
deeper, a hole's teeth get thinner and the hole wider. That is what makes a
printed or a worn thread actually fit.

### The ends

| End | What happens |
|---|---|
| Free (open end) | Lead-in cone if asked for; the thread runs one pitch past the face into air |
| Shoulder | The one-pitch relief, so the last turn ends before the face |
| Closed (hole bottom) | The thread stops one pitch short of the bottom, so a plain band of wall is left there |
| Inside (runs into another feature) | Treated as a shoulder |

## Thread standards

| Standard | Designation | Notes |
|---|---|---|
| ISO metric | `M10`, `M10x1.25` | ISO 68-1 profile, ISO 261/262 sizes, coarse and fine |
| ISO metric trapezoidal | `Tr20x4`, `Tr40x14(P7)` | ISO 2904 profile, ISO 2902 sizes |
| Acme | `1/2-10 ACME`, `1 1/8-5 ACME` | ASME B1.5 |
| Buttress (metric) | `S20x4` | DIN 513 profile, 3°/30° |
| Buttress (ASME) | — | ASME B1.9 profile only; no size table yet, so it is not offered |
| BSPP, parallel | `G 1/2` | ISO 228-1 |
| Rp, parallel internal | `Rp 1/2` | ISO 7-1 |

The tables and the parsing live in `Core/ThreadCatalog.cs`; the profiles in
`Core/ThreadForms.cs`. Neither depends on Inventor's cosmetic-thread XML — the
add-in carries its own numbers.

## Limits worth knowing

* Straight, parallel threads only. Tapered (NPT, BSPT) and trumpet threads are
  not cut.
* The thread ends one pitch past a free end. That is deliberate: it leaves a
  complete turn rather than a partial one, and the lead-in cone takes the edge.
* Very fine threads on large diameters make a small coil: the add-in stops at
  400 turns (Inventor's own ceiling for a coil is higher, but a thread that long
  is not a thread), so use a coarser pitch on a long face.
* The add-in does not change Inventor's own Thread and Hole commands; those still
  put cosmetic threads on. This is the other thing, for when the thread has to be
  in the model.

## Logging

Everything the add-in does is written to
`%LOCALAPPDATA%\ModeledThreads\modeled-threads.log`. If a thread will not cut,
the reason is in there.

## Building

```
./build.ps1                 # Release, Inventor 2027, add-in and harness
./build.ps1 -SkipHarness    # the add-in alone
```

Needs a .NET SDK (6.0 or newer) on the build machine. The add-in targets
**.NET Framework 4.8**, which is why nothing has to be installed on the machine
that runs Inventor; the SDK is only needed to compile.

The build copies `ModeledThreads.dll` into `bundle/ModeledThreads.bundle/Contents`,
so that folder is always the thing to install or zip.

## Testing

Two harnesses, both worth running after any change to the profile or the plan:

**`tools/ThreadMath`** — the maths, with no Inventor in sight, so it runs
anywhere:

```
dotnet run --project tools/ThreadMath -c Release
```

It checks every designation in every table parses and reads back as itself, the
ISO 2904 trapezoidal numbers against the standard's tables, the plan's turn
volume, where a thread stops at each kind of end (a pitch into the air at a free
one, a pitch clear of a blind hole's bottom, the one-pitch relief at a shoulder),
and that the size offered for a 10 mm shaft is an M10.

**`tools/TestHarness`** — real geometry in Inventor, through the same code the
add-in uses:

```
tools\TestHarness\bin\Release\ThreadHarness.exe
```

It builds parts, cuts threads and measures what came out: the volume removed
against the plan, the length and the end faces of the part, the flange and hole
mouths that must not change, the lead-in, the relief, and that clearance removes
more material. Both internal threads are checked in full: a through hole has to
open out to the major diameter at both mouths, and a blind hole has to be opened
out at its mouth while its bottom stays a whole disk with a plain band of wall
above it. A tube is threaded twice, outside and then inside, and the checks hold
the wall between the two cuts.

70 checks, and it saves `C:\Temp\demo-plain.ipt` (a clean shaft),
`C:\Temp\demo-nut-plain.ipt` (a clean hole), `C:\Temp\demo-blind-plain.ipt` (a
clean blind hole), `C:\Temp\demo-tube-plain.ipt` (a clean tube),
`C:\Temp\demo-bolt.ipt` (a threaded shaft), `C:\Temp\demo-nut.ipt` (a threaded
nut) and `C:\Temp\demo-tube.ipt` (threaded inside and out) so the result can be
looked at.

`tools/vm` holds the scripts used to drive all of this inside a Windows VM from
the host: running the harness in the user's desktop session, opening a part,
pressing buttons through UI Automation, and screenshotting the result.

## Signing

To remove the first-run security prompt, sign the DLL with a certificate
Windows trusts:

```
signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 ^
    /f mycert.pfx /p <password> bundle\ModeledThreads.bundle\Contents\ModeledThreads.dll
```

Sign after `build.ps1` has copied the DLL into the bundle, and before making the
installer.

## Code layout

| Path | What is in it |
|---|---|
| `Core/ThreadForms.cs` | The standards' profiles and the groove geometry |
| `Core/ThreadCatalog.cs` | Size tables, designation parsing, tap drills |
| `Core/ThreadPlan.cs` | Ends, lead-in, relief, turn volume — the plan for one thread |
| `Core/FaceProbe.cs` | Reading a face: axis, radius, extent, inside or outside, its ends |
| `Core/ThreadBuilder.cs` | The Inventor features: sketch, coil cut, revolve cuts |
| `UI/CreateThreadForm.cs` | The dialog |
| `StandardAddInServer.cs` | Ribbon, face picking, command entry point |
| `Compatibility/CompilerFeatures.cs` | The few newer C# features .NET Framework 4.8 lacks |

The split matters: everything above `ThreadBuilder.cs` is plain maths with no
Inventor types, which is what makes `tools/ThreadMath` possible.
