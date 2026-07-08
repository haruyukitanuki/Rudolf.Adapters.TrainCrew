using System.Collections.Generic;
using System.Linq;
using Tanuden.Rudolf.Enums;
using Tanuden.Rudolf.Profile;

namespace Tanuden.Rudolf.Adapters.TrainCrew.Rom;

/// <summary>
///     Joins the static ROM datasets (<see cref="RollingStockRom" />, <see cref="RouteRom" />) onto live
///     TRAIN CREW values. Every lookup returns <c>null</c> when the key is absent, so an unknown model or
///     station degrades to the spec's "sim doesn't model this" value rather than wrong data.
/// </summary>
internal static class RomLookup
{
    /// <summary>Live station name → timing-point flag, built once from <see cref="RouteRom.Stations" />.</summary>
    private static readonly IReadOnlyDictionary<string, bool> TimingPoints =
        RouteRom.Stations.ToDictionary(station => station.JapaneseName, station => station.IsTimeTaken);

    /// <summary>Consist-wide control hardware for <paramref name="model" />; <c>null</c> when unknown.</summary>
    internal static VehicleCapabilities? Capabilities(string? model)
    {
        return Lookup(model)?.Capabilities;
    }

    /// <summary>Pantograph mounting style for <paramref name="model" />; <c>null</c> when unknown.</summary>
    internal static PantographType? PantographStyle(string? model)
    {
        return Lookup(model) is { } stock ? MapPantographType(stock.PantographType) : null;
    }

    /// <summary>
    ///     Per-car pantograph facings (TIMS-relative) for <paramref name="model" /> at
    ///     <paramref name="consistLength" />; <c>null</c> when the model or that length is unknown.
    /// </summary>
    internal static IReadOnlyList<PantographDirection?>? PantographLayout(string? model, int consistLength)
    {
        return Lookup(model) is { } stock && stock.PantographDirection.TryGetValue(consistLength, out var layout)
            ? layout
            : null;
    }

    /// <summary>Timing-point flag for a live station name; <c>null</c> when the name has no ROM entry.</summary>
    internal static bool? IsTimeTaken(string? stationName)
    {
        return stationName != null && TimingPoints.TryGetValue(stationName, out var isTimeTaken) ? isTimeTaken : null;
    }

    private static RollingStockRomData? Lookup(string? model)
    {
        return model != null && RollingStockRom.Models.TryGetValue(model, out var stock) ? stock : null;
    }

    private static PantographType MapPantographType(RomPantographType type)
    {
        return type == RomPantographType.ScissorArm ? PantographType.Scissor : PantographType.SingleArm;
    }
}