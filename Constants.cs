using System.Reflection;

namespace Tanuden.Rudolf.Adapters.TrainCrew;

/// <summary>
///     Adapter constants
/// </summary>
public static class Constants
{
    private static readonly Assembly Asm = Assembly.GetExecutingAssembly();

    internal static readonly string AdapterName =
        Asm.GetName().Name!;

    internal static readonly string AdapterVersion = Asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion ?? "0.0.0";
}