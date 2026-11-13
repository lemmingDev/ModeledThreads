using System.Reflection;

// Dumps the members of the Inventor interop the add-in uses, so the code can be
// written against the real signatures instead of guesses.

string path = args.Length > 0
    ? args[0]
    : "../../refs/2027/Autodesk.Inventor.Interop.dll";

Assembly assembly = Assembly.LoadFrom(Path.GetFullPath(path));

string[] names =
{
    "Inventor.UserInterfaceManager",
    "Inventor.Ribbons",
    "Inventor.RibbonTabs",
    "Inventor.RibbonPanels",
    "Inventor.ControlDefinitions",
    "Inventor.ButtonDefinition",
    "Inventor.CommandControl",
    "Inventor.CommandControls",
    "Inventor.CommandManager",
    "Inventor.Face",
    "Inventor.FaceEvaluator",
    "Inventor.SurfaceEvaluator",
    "Inventor.Cylinder",
    "Inventor.Plane",
    "Inventor.Edge",
    "Inventor.Vertex",
    "Inventor.ApplicationAddInServer",
    "Inventor.Transaction",
    "Inventor.CoilFeatures",
    "Inventor.RevolveFeatures",
    "Inventor.PartComponentDefinition",
    "Inventor.PlanarSketches",
    "Inventor.SketchLines",
    "Inventor.Profiles",
    "Inventor.SurfaceBody",
    "Inventor.ObjectCollection",
    "Inventor.Ribbon",
    "Inventor.RibbonTab",
    "Inventor.RibbonPanel",
    "Inventor.ControlDefinition",
    "Inventor.SurfaceBody",
    "Inventor.PartFeatures",
    "Inventor.ExtrudeFeatures",
    "Inventor.HoleFeatures",
    "Inventor.RotateFeatures",
    "Inventor.WorkPlanes",
    "Inventor.Sketch",
    "Inventor.Edges",
    "Inventor.Vertices"
};

foreach (string name in names)
{
    Type? type = assembly.GetType(name);
    Console.WriteLine($"=== {name} ===");
    if (type is null)
    {
        Console.WriteLine("   (not found)");
        continue;
    }

    foreach (MemberInfo member in type.GetMembers(
                 BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                 .OrderBy(m => m.Name))
    {
        switch (member)
        {
            case MethodInfo method when !method.IsSpecialName:
                Console.WriteLine($"   M {method.Name}({string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}{(p.IsOut ? " out" : p.ParameterType.IsByRef ? " ref" : "")}"))}) -> {method.ReturnType.Name}");
                break;

            case PropertyInfo property:
                string[] indexers = property.GetIndexParameters()
                    .Select(p => $"{p.ParameterType.Name} {p.Name}").ToArray();
                string index = indexers.Length > 0 ? $"[{string.Join(", ", indexers)}]" : string.Empty;
                Console.WriteLine($"   P {property.Name}{index} : {property.PropertyType.Name} get={property.CanRead} set={property.CanWrite}");
                break;

            case EventInfo evt:
                Console.WriteLine($"   E {evt.Name} : {evt.EventHandlerType?.Name}");
                break;
        }
    }

    Console.WriteLine();
}

Console.WriteLine("=== Inventor.CommandTypesEnum ===");
Type? cte = assembly.GetType("Inventor.CommandTypesEnum");
if (cte is not null)
{
    foreach (object? value in Enum.GetValues(cte))
    {
        Console.WriteLine($"   {value}");
    }
}

Console.WriteLine();
Console.WriteLine("=== Inventor.SelectionFilterEnum (face) ===");
Type? sfe = assembly.GetType("Inventor.SelectionFilterEnum");
if (sfe is not null)
{
    foreach (object? value in Enum.GetValues(sfe))
    {
        string n = value!.ToString()!;
        if (n.Contains("Face") || n.Contains("All"))
        {
            Console.WriteLine($"   {n}");
        }
    }
}
