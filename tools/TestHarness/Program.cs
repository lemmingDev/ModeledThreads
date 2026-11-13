using System.Runtime.InteropServices;
using Inventor;
using ModeledThreads.Core;

// An automated check of the modelled threads, run inside Inventor.
//
// It builds test parts, cuts threads in them with the add-in's own code, and
// then measures what came out: the material removed, the faces that must not
// have changed, and the shape of the thread itself.

int failures = 0;
int checks = 0;

void Check(string what, bool ok, string detail = "")
{
    checks++;
    if (!ok)
    {
        failures++;
    }

    Console.WriteLine($"{(ok ? "ok  " : "FAIL")}  {what}{(detail.Length > 0 ? "   " + detail : "")}");
}

void Near(string what, double got, double want, double tolerance, string unit = "")
{
    Check(what, Math.Abs(got - want) <= tolerance,
        $"got {got:0.####}{unit}, want {want:0.####}{unit} (+/-{tolerance:0.####})");
}

// ---- Inventor --------------------------------------------------------------
Inventor.Application? app = Connect();
if (app is null)
{
    Console.WriteLine("FAIL  Inventor could not be reached.");
    return 1;
}

TransientGeometry geometry = app.TransientGeometry;

Console.WriteLine("== an external thread on a free shaft ==");
ExternalShaft(app, geometry);

Console.WriteLine();
Console.WriteLine("== an external thread running into a shoulder ==");
ExternalShoulder(app, geometry);

Console.WriteLine();
Console.WriteLine("== an internal thread in a through hole ==");
InternalThrough(app, geometry);

Console.WriteLine();
Console.WriteLine("== an internal thread in a blind hole ==");
InternalBlind(app, geometry);

Console.WriteLine();
Console.WriteLine("== internal threads with the lead-in ==");
InternalLeadIns(app, geometry);

Console.WriteLine();
Console.WriteLine("== the lead-in and the relief ==");
EndTreatments(app, geometry);

Console.WriteLine();
Console.WriteLine("== clearance takes more material away ==");
Clearance(app, geometry);

Console.WriteLine();
Console.WriteLine("== a tube: threaded outside, then inside ==");
Tube(app, geometry);

Console.WriteLine();
Console.WriteLine("== a nut for the eye ==");
DemoNut(app, geometry);

Console.WriteLine();
Console.WriteLine("== a bolt for the eye ==");
Demo(app, geometry);

Console.WriteLine();
Console.WriteLine(failures == 0
    ? $"ALL {checks} CHECKS PASSED"
    : $"{failures} of {checks} CHECKS FAILED");

return failures == 0 ? 0 : 1;

// ---- the tests -------------------------------------------------------------

void ExternalShaft(Inventor.Application app, TransientGeometry geometry)
{
    PartDocument doc = NewPart(app);
    PartComponentDefinition def = doc.ComponentDefinition;

    // A 10 mm shaft, 20 mm long, along Z.
    ExtrudeCircle(def, geometry, 5.0, 20.0, PartFeatureOperationEnum.kJoinOperation);
    double before = Volume(def);

    Face? face = FindCylinder(def, 5.0);
    if (face is null)
    {
        Check("the shaft's cylindrical face was found", false);
        doc.Close(true);
        return;
    }

    FaceProbe? probe = FaceProbeReader.Probe(doc, face, geometry);
    if (probe is null)
    {
        Check("the shaft's face was read", false);
        doc.Close(true);
        return;
    }

    Check("the shaft's face is outside a hole", !probe.Internal);
    Check("both ends of the shaft are free",
        probe.LowEnd == ThreadEndKind.Free && probe.HighEnd == ThreadEndKind.Free,
        $"{probe.LowEnd}/{probe.HighEnd}");

    ThreadPlan plan = Plan(probe, "M10x1.5", internalThread: false, lengthMm: 0,
        clearanceMm: 0, chamferStart: false, chamferEnd: false);

    // The groove takes a turn's worth of material out of every pitch of length.
    double predicted = plan.TurnVolumeMm3 / plan.PitchMm * 20.0;

    Cut(doc, probe, plan);

    double removed = before - Volume(def);
    Near("the material removed matches the thread", removed, predicted, predicted * 0.10, " mm3");

    DumpPart(def, "after the thread");

    // The thread must not change the shape of the part.
    (double minX, double maxX, double minZ, double maxZ) = Extent(def);
    Near("the shaft's length is unchanged", maxZ - minZ, 20.0, 0.05, " mm");
    Near("the shaft's width is unchanged", maxX - minX, 10.0, 0.05, " mm");

    // The end faces are still there; the thread's groove scallops their rims,
    // so they are a little smaller than a whole disk.
    Face? top = FindPlaneAt(def, 20.0);
    Face? bottom = FindPlaneAt(def, 0.0);
    Check("the top face is still there", top is not null);
    Check("the bottom face is still there", bottom is not null);
    if (top is not null)
    {
        double area = top.Evaluator.Area * 100;
        Check("the top face is only trimmed by the thread", area > Math.PI * 25.0 * 0.8,
            $"{area:0.##} of {Math.PI * 25.0:0.##} mm2");
    }

    if (bottom is not null)
    {
        double area = bottom.Evaluator.Area * 100;
        Check("the bottom face is only trimmed by the thread", area > Math.PI * 25.0 * 0.8,
            $"{area:0.##} of {Math.PI * 25.0:0.##} mm2");
    }

    // The thread itself: a swept face that is not the plain cylinder.
    Check("the thread's own faces were made", ThreadFaces(def) > 0, $"{ThreadFaces(def)} thread faces");

    doc.Close(true);
}

void ExternalShoulder(Inventor.Application app, TransientGeometry geometry)
{
    PartDocument doc = NewPart(app);
    PartComponentDefinition def = doc.ComponentDefinition;

    // A 10 mm shaft with a 20 mm flange on top.
    ExtrudeCircle(def, geometry, 5.0, 20.0, PartFeatureOperationEnum.kJoinOperation);
    ExtrudeCircle(def, geometry, 10.0, 5.0, PartFeatureOperationEnum.kJoinOperation, atZ: 20.0);

    double before = Volume(def);

    Face? face = FindCylinder(def, 5.0);
    if (face is null)
    {
        Check("the shaft's cylindrical face was found", false);
        doc.Close(true);
        return;
    }

    FaceProbe? probe = FaceProbeReader.Probe(doc, face, geometry);
    if (probe is null)
    {
        Check("the shaft's face was read", false);
        doc.Close(true);
        return;
    }

    Check("the shaft's face is outside a hole", !probe.Internal);
    Check("the lower end is free and the upper one a shoulder",
        probe.LowEnd == ThreadEndKind.Free && probe.HighEnd == ThreadEndKind.Shoulder,
        $"{probe.LowEnd}/{probe.HighEnd}");

    ThreadPlan plan = Plan(probe, "M10x1.5", internalThread: false, lengthMm: 0,
        clearanceMm: 0, chamferStart: false, chamferEnd: true);
    Check("a relief is planned at the shoulder", plan.Relief is not null);

    Cut(doc, probe, plan);

    double after = Volume(def);
    Check("the thread took material away", after < before, $"{before:0.##} -> {after:0.##} mm3");

    DumpPart(def, "after the thread");

    // The flange must be untouched: its underside is still the whole annulus
    // between the shaft and its rim, and its top is still a whole disk.
    Face? underside = FindPlaneAt(def, 20.0);
    Check("the flange's underside is still there", underside is not null);
    if (underside is not null)
    {
        // The relief's mouth opens onto the same plane, so the face is the
        // flange's annulus plus that opening: it must not have lost anything.
        double area = underside.Evaluator.Area * 100;
        Check("the flange's underside is whole", area >= (Math.PI * (100.0 - 25.0)) - 0.5,
            $"{area:0.##} mm2, the annulus alone is {Math.PI * 75.0:0.##} mm2");
    }

    Face? top = FindPlaneAt(def, 25.0);
    Check("the flange's top is still there", top is not null);
    if (top is not null)
    {
        Near("the flange's top is unchanged", top.Evaluator.Area * 100, Math.PI * 100.0, 0.5, " mm2");
    }

    (double minX, double maxX, double minZ, double maxZ) = Extent(def);
    Near("the part's overall height is unchanged", maxZ - minZ, 25.0, 0.05, " mm");
    Near("the flange's width is unchanged", maxX - minX, 20.0, 0.05, " mm");

    doc.Close(true);
}

void InternalThrough(Inventor.Application app, TransientGeometry geometry)
{
    PartDocument doc = NewPart(app);
    PartComponentDefinition def = doc.ComponentDefinition;

    // A 20 mm block with an 8.5 mm hole straight through.
    ExtrudeRectangle(def, geometry, 20.0, 20.0, 20.0);
    ExtrudeCircle(def, geometry, 4.25, 25.0, PartFeatureOperationEnum.kCutOperation, atZ: 20.0, downwards: true);

    double before = Volume(def);

    Face? face = FindCylinder(def, 4.25);
    if (face is null)
    {
        Check("the hole's cylindrical face was found", false);
        doc.Close(true);
        return;
    }

    FaceProbe? probe = FaceProbeReader.Probe(doc, face, geometry);
    if (probe is null)
    {
        Check("the hole's face was read", false);
        doc.Close(true);
        return;
    }

    Check("the hole's face is inside a hole", probe.Internal);
    Check("both ends of the hole are free",
        probe.LowEnd == ThreadEndKind.Free && probe.HighEnd == ThreadEndKind.Free,
        $"{probe.LowEnd}/{probe.HighEnd}");

    ThreadPlan plan = Plan(probe, "M10x1.5", internalThread: true, lengthMm: 0,
        clearanceMm: 0, chamferStart: false, chamferEnd: false);

    double predicted = plan.TurnVolumeMm3 / plan.PitchMm * 20.0;

    Cut(doc, probe, plan);

    double removed = before - Volume(def);
    Near("the material removed matches the thread", removed, predicted, predicted * 0.12, " mm3");

    // The block's outside must be untouched.
    (double blockMinX, double blockMaxX, double blockMinZ, double blockMaxZ) = Extent(def);
    Near("the block's width is unchanged", blockMaxX - blockMinX, 20.0, 0.05, " mm");

    // The hole's mouth must still be clean: the top face is the block's own
    // face with the hole through it, its rim scalloped by the thread.
    Face? mouth = FindPlaneAt(def, 20.0);
    Check("the hole's mouth is still there", mouth is not null);
    if (mouth is not null)
    {
        double full = (20.0 * 20.0) - (Math.PI * 4.25 * 4.25);
        double area = mouth.Evaluator.Area * 100;
        Check("the top face is only trimmed by the thread", area > full * 0.95,
            $"{area:0.##} of {full:0.##} mm2");
    }

    // The hole's own wall: the thread has to open it out to the major
    // diameter, and it has to do it at both mouths of a through hole.
    double crest = MaxRadiusInBand(def, 0.0, 20.0);
    Check("the hole was opened out to the major diameter", crest >= 4.9 && crest <= 5.15,
        $"the widest point of the hole is {crest:0.###} mm from the axis");

    double atTop = MaxRadiusInBand(def, 18.0, 20.0);
    double atBottom = MaxRadiusInBand(def, 0.0, 2.0);
    Check("the thread reaches the top mouth", atTop >= 4.9, $"{atTop:0.###} mm");
    Check("the thread reaches the bottom mouth", atBottom >= 4.9, $"{atBottom:0.###} mm");

    // One lead past an open end, and no more: the block's own faces stay where
    // they are.
    (double innerLow, double innerHigh, double _) = InnerReach(def, 6.0);
    Check("the thread runs no more than a pitch past the mouths",
        innerLow > -2.0 && innerHigh < 22.0, $"{innerLow:0.##}..{innerHigh:0.##} mm");

    DumpPart(def, "after the thread");

    doc.Close(true);
}

void InternalBlind(Inventor.Application app, TransientGeometry geometry)
{
    PartDocument doc = NewPart(app);
    PartComponentDefinition def = doc.ComponentDefinition;

    // A 20 mm block with an 8.5 mm hole 15 mm deep from the top.
    ExtrudeRectangle(def, geometry, 20.0, 20.0, 20.0);
    ExtrudeCircle(def, geometry, 4.25, 15.0, PartFeatureOperationEnum.kCutOperation, atZ: 20.0, downwards: true);

    // A copy with no thread on it, for trying the dialog on a blind hole.
    try
    {
        doc.SaveAs(@"C:\Temp\demo-blind-plain.ipt", true);
    }
    catch (Exception ex)
    {
        Console.WriteLine("      the plain blind hole could not be saved: " + ex.Message);
    }

    double before = Volume(def);

    Face? face = FindCylinder(def, 4.25);
    if (face is null)
    {
        Check("the hole's cylindrical face was found", false);
        doc.Close(true);
        return;
    }

    FaceProbe? probe = FaceProbeReader.Probe(doc, face, geometry);
    if (probe is null)
    {
        Check("the hole's face was read", false);
        doc.Close(true);
        return;
    }

    Check("the hole's face is inside a hole", probe.Internal);

    // The closed end is the bottom (5 mm up); the mouth is the free one.
    double lowZ = EndZ(probe, low: true);
    double highZ = EndZ(probe, low: false);
    bool bottomClosed = (Math.Abs(lowZ - 5.0) < 0.1 && probe.LowEnd == ThreadEndKind.Closed)
                        || (Math.Abs(highZ - 5.0) < 0.1 && probe.HighEnd == ThreadEndKind.Closed);
    bool mouthFree = (Math.Abs(lowZ - 20.0) < 0.1 && probe.LowEnd == ThreadEndKind.Free)
                     || (Math.Abs(highZ - 20.0) < 0.1 && probe.HighEnd == ThreadEndKind.Free);

    Check("the bottom of the hole is closed", bottomClosed,
        $"low {lowZ:0.##} {probe.LowEnd}, high {highZ:0.##} {probe.HighEnd}");
    Check("the mouth of the hole is free", mouthFree);

    ThreadPlan plan = Plan(probe, "M10x1.5", internalThread: true, lengthMm: 0,
        clearanceMm: 0, chamferStart: false, chamferEnd: false);

    // The thread must stop clear of the bottom: the groove's last flank ends a
    // whole pitch above it, so a plain band of wall stays there. The sweep
    // starts a pitch before the mouth, so the closed end is a pitch and the
    // face's own length past the sweep's start.
    double bottomMm = plan.SweepStartMm + plan.PitchMm + 15.0;
    Near("the sweep stops one pitch short of the bottom",
        plan.SweepEndMm + plan.WidthHiMm, bottomMm - plan.PitchMm, 0.02, " mm");

    // The first turn of the sweep lies in the air beyond the mouth and cuts
    // nothing; the rest is the material's own.
    double predicted = plan.TurnVolumeMm3 * (plan.Turns - 1.0);

    Cut(doc, probe, plan);

    double after = Volume(def);
    Check("the thread took material away", after < before);
    Near("the material removed matches the thread", before - after, predicted,
        predicted * 0.12, " mm3");

    // The bottom of the hole must be whole and in the same place.
    Face? bottom = FindPlaneAt(def, 5.0);
    Check("the bottom of the hole is still there", bottom is not null);
    if (bottom is not null)
    {
        Near("the bottom of the hole is a whole disk",
            bottom.Evaluator.Area * 100, Math.PI * 4.25 * 4.25, 0.5, " mm2");
    }

    Check("the thread's own faces were made", ThreadFaces(def) > 0);

    // The mouth end is threaded out to the major diameter ...
    double atMouth = MaxRadiusInBand(def, 18.0, 20.0);
    Check("the hole was opened out to the major diameter at the mouth", atMouth >= 4.9,
        $"{atMouth:0.###} mm from the axis");

    // ... and the wall just above the bottom is still the drill's own, so the
    // thread runs out before it and the bottom keeps its shoulder.
    double aboveBottom = MaxRadiusInBand(def, 5.0, 5.6);
    Check("the wall just above the bottom is still the drill size", aboveBottom <= 4.35,
        $"{aboveBottom:0.###} mm from the axis");

    (double threadLow, double threadHigh) = ThreadFaceRange(def);
    Check("the thread stops clear of the bottom", threadLow > 5.4,
        $"the lowest thread face is at {threadLow:0.###} mm, the bottom at 5 mm");
    Check("the thread reaches the mouth", threadHigh > 19.0, $"{threadHigh:0.###} mm");

    doc.Close(true);
}

/// <summary>
/// The same two holes, threaded the way the dialog does it by default: with
/// the 45° lead-in cone at the mouth.
/// </summary>
void InternalLeadIns(Inventor.Application app, TransientGeometry geometry)
{
    foreach ((string name, bool blind) in new[] { ("through hole", false), ("blind hole", true) })
    {
        PartDocument doc = NewPart(app);
        PartComponentDefinition def = doc.ComponentDefinition;

        ExtrudeRectangle(def, geometry, 20.0, 20.0, 20.0);
        ExtrudeCircle(def, geometry, 4.25, blind ? 15.0 : 25.0,
            PartFeatureOperationEnum.kCutOperation, atZ: 20.0, downwards: true);

        double before = Volume(def);

        Face? face = FindCylinder(def, 4.25);
        FaceProbe? probe = face is null ? null : FaceProbeReader.Probe(doc, face, geometry);
        if (probe is null)
        {
            Check($"the {name}'s face was read", false);
            doc.Close(true);
            continue;
        }

        ThreadPlan plan = Plan(probe, "M10x1.5", internalThread: true, lengthMm: 0,
            clearanceMm: 0, chamferStart: true, chamferEnd: true);

        Check($"the {name} is given a lead-in", plan.Tip is not null && plan.Tip.Value.Internal);

        Cut(doc, probe, plan);

        Check($"the {name}'s thread took material away", Volume(def) < before);

        // The cone opens the mouth past the thread's major diameter and closes
        // back onto the hole within a pitch.
        double atMouth = MaxRadiusInBand(def, 19.8, 20.0);
        Check($"the {name}'s mouth is chamfered past the major diameter", atMouth > 5.05,
            $"{atMouth:0.###} mm from the axis");

        Check($"the {name}'s cone is shallower than a pitch",
            plan.Tip!.Value.DepthMm > 0.3 && plan.Tip.Value.DepthMm < plan.PitchMm,
            $"{plan.Tip.Value.DepthMm:0.###} mm");

        if (blind)
        {
            Face? bottom = FindPlaneAt(def, 5.0);
            Check("the blind hole's bottom is still whole",
                bottom is not null
                && Math.Abs(bottom.Evaluator.Area * 100 - Math.PI * 4.25 * 4.25) < 0.5,
                bottom is null ? "no bottom face" : $"{bottom.Evaluator.Area * 100:0.##} mm2");
        }
        else
        {
            Check("the through hole's far mouth is still there", FindPlaneAt(def, 0.0) is not null);
        }

        doc.Close(true);
    }
}

void EndTreatments(Inventor.Application app, TransientGeometry geometry)
{
    // The 45 degree lead-in at a free end, and the relief at a shoulder.
    PartDocument doc = NewPart(app);
    PartComponentDefinition def = doc.ComponentDefinition;

    ExtrudeCircle(def, geometry, 5.0, 20.0, PartFeatureOperationEnum.kJoinOperation);

    Face? face = FindCylinder(def, 5.0);
    if (face is null)
    {
        Check("the shaft's cylindrical face was found", false);
        doc.Close(true);
        return;
    }

    FaceProbe? probe = FaceProbeReader.Probe(doc, face, geometry);
    if (probe is null)
    {
        Check("the shaft's face was read", false);
        doc.Close(true);
        return;
    }

    ThreadPlan plan = Plan(probe, "M10x1.5", internalThread: false, lengthMm: 0,
        clearanceMm: 0, chamferStart: true, chamferEnd: false);
    Check("a lead-in is planned", plan.Tip is not null);

    Cut(doc, probe, plan);

    // The lead-in leaves the end a smaller disk.
    Face? end = FindPlaneAt(def, 0.0);
    Check("the end of the shaft is still there", end is not null);
    if (end is not null)
    {
        double radius = Math.Sqrt(end.Evaluator.Area * 100 / Math.PI);
        Check("the lead-in took the end of the shaft away", radius < 4.9,
            $"the end is {radius:0.###} mm across");
    }

    doc.Close(true);
}

/// <summary>
/// Builds a bolt and leaves it open, so the thread can be looked at.
/// </summary>
void Demo(Inventor.Application app, TransientGeometry geometry)
{
    PartDocument doc = NewPart(app);
    PartComponentDefinition def = doc.ComponentDefinition;

    // A 10 mm shaft 30 mm long with an 18 mm head on top.
    ExtrudeCircle(def, geometry, 5.0, 30.0, PartFeatureOperationEnum.kJoinOperation);
    ExtrudeCircle(def, geometry, 9.0, 8.0, PartFeatureOperationEnum.kJoinOperation, atZ: 30.0);

    Face? face = FindCylinder(def, 5.0);
    if (face is null)
    {
        Check("the demo bolt's face was found", false);
        return;
    }

    FaceProbe? probe = FaceProbeReader.Probe(doc, face, geometry);
    if (probe is null)
    {
        Check("the demo bolt's face was read", false);
        return;
    }

    ThreadPlan plan = Plan(probe, "M10x1.5", internalThread: false, lengthMm: 24,
        clearanceMm: 0, chamferStart: true, chamferEnd: true);

    // A copy with no thread on it, for trying the dialog on a clean face.
    try
    {
        doc.SaveAs(@"C:\Temp\demo-plain.ipt", true);
    }
    catch (Exception ex)
    {
        Console.WriteLine("      the plain copy could not be saved: " + ex.Message);
    }

    Cut(doc, probe, plan);

    try
    {
        doc.SaveAs(@"C:\Temp\demo-bolt.ipt", true);
        app.ActiveView.Fit();
        Console.WriteLine("      saved C:\\Temp\\demo-bolt.ipt and left it open");
    }
    catch (Exception ex)
    {
        Console.WriteLine("      the demo bolt could not be saved: " + ex.Message);
    }
}

/// <summary>
/// One part with a hole through it, threaded on the outside and then on the
/// inside, so the two threads share the same wall.
/// </summary>
void Tube(Inventor.Application app, TransientGeometry geometry)
{
    PartDocument doc = NewPart(app);
    PartComponentDefinition def = doc.ComponentDefinition;

    // A 20 mm shaft 30 mm long with an 8.5 mm hole right through: a tube.
    ExtrudeCircle(def, geometry, 10.0, 30.0, PartFeatureOperationEnum.kJoinOperation);
    ExtrudeCircle(def, geometry, 4.25, 40.0, PartFeatureOperationEnum.kCutOperation,
        atZ: 30.0, downwards: true);

    try
    {
        doc.SaveAs(@"C:\Temp\demo-tube-plain.ipt", true);
    }
    catch (Exception ex)
    {
        Console.WriteLine("      the plain tube could not be saved: " + ex.Message);
    }

    double plain = Volume(def);

    // ---- outside: M20 on the shaft -------------------------------------
    Face? outerFace = FindCylinder(def, 10.0);
    FaceProbe? outer = outerFace is null ? null : FaceProbeReader.Probe(doc, outerFace, geometry);

    Check("the tube's outer face was found", outer is not null);
    if (outer is null)
    {
        doc.Close(true);
        return;
    }

    Check("the tube's outer face is outside a hole", !outer.Internal);

    ThreadPlan outside = Plan(outer, "M20x2.5", internalThread: false, lengthMm: 0,
        clearanceMm: 0, chamferStart: true, chamferEnd: true);

    Cut(doc, outer, outside);

    double afterOutside = Volume(def);
    Check("the outside thread took material away", afterOutside < plain,
        $"{plain:0.#} -> {afterOutside:0.#} mm3");

    // The crest is the face the thread was cut on, so the tube is still 20 mm
    // across, and the thread's root cannot have reached the hole.
    (double minX, double maxX, double _, double _) = Extent(def);
    Near("the tube is still 20 mm across", maxX - minX, 20.0, 0.05, " mm");

    double hole = MaxRadiusInBand(def, 0.0, 30.0);
    Near("the hole is still the drill size after the outside thread", hole, 4.25, 0.05, " mm");

    int outerThreadFaces = ThreadFaces(def);

    // ---- inside: M10 in the same hole ----------------------------------
    Face? innerFace = FindCylinder(def, 4.25);
    FaceProbe? inner = innerFace is null ? null : FaceProbeReader.Probe(doc, innerFace, geometry);

    Check("the hole's face was still found after the outside thread", inner is not null);
    if (inner is null)
    {
        doc.Close(true);
        return;
    }

    Check("the hole's face is inside a hole", inner.Internal);

    ThreadPlan inside = Plan(inner, "M10x1.5", internalThread: true, lengthMm: 0,
        clearanceMm: 0, chamferStart: true, chamferEnd: true);

    Cut(doc, inner, inside);

    double afterInside = Volume(def);
    Check("the inside thread took material away too", afterInside < afterOutside,
        $"{afterOutside:0.#} -> {afterInside:0.#} mm3");

    // Both ends of the hole are opened out to the major diameter ...
    double atTop = MaxRadiusInBand(def, 28.0, 30.0);
    double atBottom = MaxRadiusInBand(def, 0.0, 2.0);
    Check("the inside thread reaches the top mouth", atTop >= 4.9, $"{atTop:0.###} mm");
    Check("the inside thread reaches the bottom mouth", atBottom >= 4.9, $"{atBottom:0.###} mm");

    // ... and the outside thread is still on the part.
    Check("both threads are on the part",
        ThreadFaces(def) > outerThreadFaces, $"{ThreadFaces(def)} thread faces");

    (double minX2, double maxX2, double _, double _) = Extent(def);
    Near("the outside thread is still there", maxX2 - minX2, 20.0, 0.05, " mm");

    // The wall between the two threads is untouched: the outside thread's root
    // is the closest the outer groove comes to the axis, and it must stay well
    // outside the hole's thread.
    double outerRoot = MinRadiusAbove(def, 5.6);
    Check("the two threads do not break into each other", outerRoot > 8.0,
        $"the outside thread's root is {outerRoot:0.###} mm from the axis, the hole's thread reaches {inside.CrestRadiusMm:0.###} mm");

    try
    {
        doc.SaveAs(@"C:\Temp\demo-tube.ipt", true);
        Console.WriteLine("      saved C:\\Temp\\demo-tube.ipt");
    }
    catch (Exception ex)
    {
        Console.WriteLine("      the demo tube could not be saved: " + ex.Message);
    }

    doc.Close(true);
}

/// <summary>
/// Builds a nut and saves it, so an internal thread can be looked at.
/// </summary>
void DemoNut(Inventor.Application app, TransientGeometry geometry)
{
    PartDocument doc = NewPart(app);
    PartComponentDefinition def = doc.ComponentDefinition;

    // A 16 mm hex, 8 mm high, tapped M10 right through with the mouth
    // chamfered, the way the dialog does it by default.
    ExtrudePolygon(def, geometry, 6, 8.0, 8.0);
    ExtrudeCircle(def, geometry, 4.25, 12.0, PartFeatureOperationEnum.kCutOperation,
        atZ: 8.0, downwards: true);

    Face? face = FindCylinder(def, 4.25);
    FaceProbe? probe = face is null ? null : FaceProbeReader.Probe(doc, face, geometry);
    if (probe is null)
    {
        Check("the demo nut's hole was read", false);
        doc.Close(true);
        return;
    }

    ThreadPlan plan = Plan(probe, "M10x1.5", internalThread: true, lengthMm: 0,
        clearanceMm: 0, chamferStart: true, chamferEnd: true);

    // A copy with no thread on it, for trying the dialog on a hole.
    try
    {
        doc.SaveAs(@"C:\Temp\demo-nut-plain.ipt", true);
    }
    catch (Exception ex)
    {
        Console.WriteLine("      the plain nut could not be saved: " + ex.Message);
    }

    Cut(doc, probe, plan);

    try
    {
        doc.SaveAs(@"C:\Temp\demo-nut.ipt", true);
        Console.WriteLine("      saved C:\\Temp\\demo-nut.ipt");
    }
    catch (Exception ex)
    {
        Console.WriteLine("      the demo nut could not be saved: " + ex.Message);
    }
}

void Clearance(Inventor.Application app, TransientGeometry geometry)
{
    double[] removed = new double[2];

    for (int i = 0; i < 2; i++)
    {
        PartDocument doc = NewPart(app);
        PartComponentDefinition def = doc.ComponentDefinition;

        ExtrudeCircle(def, geometry, 5.0, 20.0, PartFeatureOperationEnum.kJoinOperation);
        double before = Volume(def);

        Face? face = FindCylinder(def, 5.0);
        if (face is null)
        {
            Check("the shaft's cylindrical face was found", false);
            doc.Close(true);
            return;
        }

        FaceProbe? probe = FaceProbeReader.Probe(doc, face, geometry);
        if (probe is null)
        {
            Check("the shaft's face was read", false);
            doc.Close(true);
            return;
        }

        ThreadPlan plan = Plan(probe, "M10x1.5", internalThread: false, lengthMm: 0,
            clearanceMm: i == 0 ? 0 : 0.3, chamferStart: false, chamferEnd: false);
        Cut(doc, probe, plan);

        removed[i] = before - Volume(def);
        doc.Close(true);
    }

    Check("clearance cuts more material away", removed[1] > removed[0],
        $"{removed[0]:0.##} -> {removed[1]:0.##} mm3");
}

// ---- helpers ---------------------------------------------------------------

ThreadPlan Plan(
    FaceProbe probe,
    string designation,
    bool internalThread,
    double lengthMm,
    double clearanceMm,
    bool chamferStart,
    bool chamferEnd)
{
    if (!ThreadCatalog.TryParse(designation, out ThreadSize size, out string error))
    {
        throw new InvalidOperationException(error);
    }

    var options = new ThreadPlanOptions
    {
        Form = size.Form,
        Designation = designation,
        MajorMm = size.MajorMm,
        PitchMm = size.PitchMm,
        LeadMm = size.PitchMm,
        Internal = internalThread,
        ClearanceMm = clearanceMm,
        RightHanded = true,
        Starts = 1,
        FaceRadiusMm = probe.RadiusMm,
        FaceLowMm = probe.LowCm * 10.0,
        FaceHighMm = probe.HighCm * 10.0,
        StartSide = ChooseStartSide(probe),
        LengthMm = lengthMm,
        LowEndKind = probe.LowEnd,
        HighEndKind = probe.HighEnd,
        ChamferStart = chamferStart,
        ChamferEnd = chamferEnd
    };

    return ThreadPlanBuilder.Build(options);
}

/// <summary>The free end, as the dialog does.</summary>
int ChooseStartSide(FaceProbe probe)
{
    if (probe.LowEnd == ThreadEndKind.Free && probe.HighEnd != ThreadEndKind.Free)
    {
        return 0;
    }

    if (probe.HighEnd == ThreadEndKind.Free && probe.LowEnd != ThreadEndKind.Free)
    {
        return 1;
    }

    return 0;
}

/// <summary>The world height of one of the probe's ends.</summary>
double EndZ(FaceProbe probe, bool low)
{
    double t = low ? probe.LowCm : probe.HighCm;
    return (probe.BasePoint.Z + probe.Axis.Z * t) * 10.0;
}

void Cut(PartDocument doc, FaceProbe probe, ThreadPlan plan)
{
    Transaction transaction = app.TransactionManager.StartTransaction(
        doc.InventorDocument, $"Test thread {plan.Designation}");

    try
    {
        var builder = new ThreadBuilder(app, doc, probe);
        builder.Build(plan);

        try
        {
            transaction.End();
        }
        catch (Exception ex)
        {
            Check($"the thread '{plan.Designation}' rebuilt", false, "the rebuild failed: " + ex.Message);
            try
            {
                transaction.Abort();
            }
            catch
            {
                // the message above is the useful one
            }

            return;
        }
    }
    catch (Exception ex)
    {
        try
        {
            transaction.Abort();
        }
        catch
        {
            // the message below is the useful one
        }

        Check($"the thread '{plan.Designation}' was cut", false, ex.ToString());
    }
}

PartDocument NewPart(Inventor.Application app)
{
    Document? document = app.Documents.Add(DocumentTypeEnum.kPartDocumentObject, string.Empty, true);
    return document as PartDocument
           ?? throw new InvalidOperationException("A part could not be made.");
}

ExtrudeFeature ExtrudeCircle(
    PartComponentDefinition def,
    TransientGeometry geometry,
    double radiusMm,
    double heightMm,
    PartFeatureOperationEnum operation,
    double atZ = 0,
    bool downwards = false)
{
    WorkPlane plane = def.WorkPlanes.AddFixed(
        geometry.CreatePoint(0, 0, atZ / 10.0),
        geometry.CreateUnitVector(1, 0, 0),
        geometry.CreateUnitVector(0, 1, 0),
        true);

    PlanarSketch sketch = def.Sketches.Add(plane, false);
    sketch.SketchCircles.AddByCenterRadius(geometry.CreatePoint2d(0, 0), radiusMm / 10.0);

    Profile profile = sketch.Profiles.AddForSolid();

    ExtrudeDefinition extrude = def.Features.ExtrudeFeatures.CreateExtrudeDefinition(profile, operation);
    extrude.SetDistanceExtent(
        heightMm / 10.0,
        downwards
            ? PartFeatureExtentDirectionEnum.kNegativeExtentDirection
            : PartFeatureExtentDirectionEnum.kPositiveExtentDirection);

    return def.Features.ExtrudeFeatures.Add(extrude);
}

ExtrudeFeature ExtrudeRectangle(
    PartComponentDefinition def,
    TransientGeometry geometry,
    double widthMm,
    double depthMm,
    double heightMm)
{
    WorkPlane plane = def.WorkPlanes.AddFixed(
        geometry.CreatePoint(0, 0, 0),
        geometry.CreateUnitVector(1, 0, 0),
        geometry.CreateUnitVector(0, 1, 0),
        true);

    PlanarSketch sketch = def.Sketches.Add(plane, false);

    double w = widthMm / 20.0;
    double d = depthMm / 20.0;

    SketchLines lines = sketch.SketchLines;
    SketchLine l1 = lines.AddByTwoPoints(geometry.CreatePoint2d(-w, -d), geometry.CreatePoint2d(w, -d));
    SketchLine l2 = lines.AddByTwoPoints(l1.EndSketchPoint, geometry.CreatePoint2d(w, d));
    SketchLine l3 = lines.AddByTwoPoints(l2.EndSketchPoint, geometry.CreatePoint2d(-w, d));
    lines.AddByTwoPoints(l3.EndSketchPoint, l1.StartSketchPoint);

    Profile profile = sketch.Profiles.AddForSolid();

    ExtrudeDefinition extrude = def.Features.ExtrudeFeatures.CreateExtrudeDefinition(
        profile, PartFeatureOperationEnum.kJoinOperation);
    extrude.SetDistanceExtent(heightMm / 10.0, PartFeatureExtentDirectionEnum.kPositiveExtentDirection);

    return def.Features.ExtrudeFeatures.Add(extrude);
}

/// <summary>An extruded regular polygon: a nut, for instance.</summary>
ExtrudeFeature ExtrudePolygon(
    PartComponentDefinition def,
    TransientGeometry geometry,
    int sides,
    double circumradiusMm,
    double heightMm)
{
    WorkPlane plane = def.WorkPlanes.AddFixed(
        geometry.CreatePoint(0, 0, 0),
        geometry.CreateUnitVector(1, 0, 0),
        geometry.CreateUnitVector(0, 1, 0),
        true);

    PlanarSketch sketch = def.Sketches.Add(plane, false);

    double r = circumradiusMm / 10.0;
    Point2d Corner(double turn) => geometry.CreatePoint2d(r * Math.Cos(turn), r * Math.Sin(turn));

    SketchLines lines = sketch.SketchLines;
    SketchLine first = lines.AddByTwoPoints(Corner(0), Corner(2 * Math.PI / sides));
    SketchLine last = first;

    for (int i = 2; i < sides; i++)
    {
        last = lines.AddByTwoPoints(last.EndSketchPoint, Corner(2 * Math.PI * i / sides));
    }

    lines.AddByTwoPoints(last.EndSketchPoint, first.StartSketchPoint);

    Profile profile = sketch.Profiles.AddForSolid();

    ExtrudeDefinition extrude = def.Features.ExtrudeFeatures.CreateExtrudeDefinition(
        profile, PartFeatureOperationEnum.kJoinOperation);
    extrude.SetDistanceExtent(heightMm / 10.0, PartFeatureExtentDirectionEnum.kPositiveExtentDirection);

    return def.Features.ExtrudeFeatures.Add(extrude);
}

Face? FindCylinder(PartComponentDefinition def, double radiusMm)
{
    foreach (SurfaceBody body in def.SurfaceBodies)
    {
        foreach (Face face in body.Faces)
        {
            if (face.SurfaceType == SurfaceTypeEnum.kCylinderSurface
                && face.Geometry is Cylinder cylinder
                && Math.Abs(cylinder.Radius * 10.0 - radiusMm) < 0.05)
            {
                return face;
            }
        }
    }

    return null;
}

Face? FindPlaneAt(PartComponentDefinition def, double zMm)
{
    foreach (SurfaceBody body in def.SurfaceBodies)
    {
        foreach (Face face in body.Faces)
        {
            if (face.SurfaceType != SurfaceTypeEnum.kPlaneSurface
                || face.Geometry is not Plane plane)
            {
                continue;
            }

            if (Math.Abs(plane.RootPoint.Z * 10.0 - zMm) < 0.05
                && Math.Abs(plane.Normal.Z) > 0.9)
            {
                return face;
            }
        }
    }

    return null;
}

/// <summary>The faces a thread is made of: the swept ones and the small flats.</summary>
int ThreadFaces(PartComponentDefinition def)
{
    int count = 0;
    foreach (SurfaceBody body in def.SurfaceBodies)
    {
        foreach (Face face in body.Faces)
        {
            if (face.SurfaceType == SurfaceTypeEnum.kBSplineSurface)
            {
                count++;
            }
        }
    }

    return count;
}

/// <summary>Points on a face's edges, as (distance from the axis, height).</summary>
IEnumerable<(double R, double Z)> EdgePoints(Face face)
{
    foreach (Edge edge in face.Edges)
    {
        foreach (Vertex vertex in new[] { edge.StartVertex, edge.StopVertex })
        {
            Inventor.Point point = vertex.Point;
            yield return (Math.Sqrt((point.X * point.X) + (point.Y * point.Y)) * 10.0, point.Z * 10.0);
        }
    }
}

/// <summary>
/// How far the part reaches inside a hole: the lowest and highest points of the
/// surfaces there, and the widest. Points well outside the hole are left out,
/// so the block's own corners do not count.
/// </summary>
(double Low, double High, double Widest) InnerReach(PartComponentDefinition def, double withinRadiusMm)
{
    double low = double.MaxValue;
    double high = double.MinValue;
    double widest = 0.0;

    foreach (SurfaceBody body in def.SurfaceBodies)
    {
        foreach (Face face in body.Faces)
        {
            foreach ((double r, double z) in EdgePoints(face))
            {
                if (r > withinRadiusMm)
                {
                    continue;
                }

                low = Math.Min(low, z);
                high = Math.Max(high, z);
                widest = Math.Max(widest, r);
            }
        }
    }

    return (low, high, widest);
}

/// <summary>The widest point of the hole between two heights, in millimetres.</summary>
double MaxRadiusInBand(PartComponentDefinition def, double fromZ, double toZ)
{
    double widest = 0.0;

    foreach (SurfaceBody body in def.SurfaceBodies)
    {
        foreach (Face face in body.Faces)
        {
            foreach ((double r, double z) in EdgePoints(face))
            {
                if (z < fromZ - 0.05 || z > toZ + 0.05 || r > 7.0)
                {
                    continue;
                }

                widest = Math.Max(widest, r);
            }
        }
    }

    return widest;
}

/// <summary>The closest the part comes to the axis outside a radius, in millimetres.</summary>
double MinRadiusAbove(PartComponentDefinition def, double aboveMm)
{
    double closest = double.MaxValue;

    foreach (SurfaceBody body in def.SurfaceBodies)
    {
        foreach (Face face in body.Faces)
        {
            foreach ((double r, double _) in EdgePoints(face))
            {
                if (r > aboveMm)
                {
                    closest = Math.Min(closest, r);
                }
            }
        }
    }

    return closest;
}

/// <summary>The heights the thread's own swept faces reach.</summary>
(double Low, double High) ThreadFaceRange(PartComponentDefinition def)
{
    double low = double.MaxValue;
    double high = double.MinValue;

    foreach (SurfaceBody body in def.SurfaceBodies)
    {
        foreach (Face face in body.Faces)
        {
            if (face.SurfaceType != SurfaceTypeEnum.kBSplineSurface)
            {
                continue;
            }

            foreach ((double _, double z) in EdgePoints(face))
            {
                low = Math.Min(low, z);
                high = Math.Max(high, z);
            }
        }
    }

    return (low, high);
}

/// <summary>The part's extent, in millimetres.</summary>
(double MinX, double MaxX, double MinZ, double MaxZ) Extent(PartComponentDefinition def)
{
    double minX = double.MaxValue;
    double maxX = double.MinValue;
    double minZ = double.MaxValue;
    double maxZ = double.MinValue;

    foreach (SurfaceBody body in def.SurfaceBodies)
    {
        Box box = body.PreciseRangeBox;
        minX = Math.Min(minX, box.MinPoint.X);
        maxX = Math.Max(maxX, box.MaxPoint.X);
        minZ = Math.Min(minZ, box.MinPoint.Z);
        maxZ = Math.Max(maxZ, box.MaxPoint.Z);
    }

    return (minX * 10.0, maxX * 10.0, minZ * 10.0, maxZ * 10.0);
}

/// <summary>
/// Prints what the part is made of, so a surprising measurement can be traced.
/// </summary>
void DumpPart(PartComponentDefinition def, string label)
{
    Console.WriteLine($"      -- the part {label} --");
    int index = 0;
    foreach (SurfaceBody body in def.SurfaceBodies)
    {
        index++;
        Box box = body.PreciseRangeBox;
        Console.WriteLine(
            $"      body {index}: {body.Faces.Count} faces, {body.Volume[1.0] * 1000.0:0.###} mm3, " +
            $"z {box.MinPoint.Z * 10.0:0.###}..{box.MaxPoint.Z * 10.0:0.###}, " +
            $"x {box.MinPoint.X * 10.0:0.###}..{box.MaxPoint.X * 10.0:0.###}");
    }

    Console.WriteLine($"      bodies: {def.SurfaceBodies.Count}");
}

/// <summary>The material's volume, in cubic millimetres.</summary>
double Volume(PartComponentDefinition def)
{
    double total = 0;
    foreach (SurfaceBody body in def.SurfaceBodies)
    {
        total += body.Volume[1.0];
    }

    return total * 1000.0;
}

static Inventor.Application? Connect()
{
    // Inventor is a single-instance COM server: starting it again hands back
    // the one that is already running.
    try
    {
        Type? type = Type.GetTypeFromProgID("Inventor.Application");
        return type is null ? null : Activator.CreateInstance(type) as Inventor.Application;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Inventor could not be started: {ex.Message}");
        return null;
    }
}
