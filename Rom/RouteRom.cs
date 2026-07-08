using System.Collections.Generic;

namespace Tanuden.Rudolf.Adapters.TrainCrew.Rom;

/// <summary>
///     ROM for a single station
/// </summary>
internal sealed record RouteRomData
{
    /// <summary>Station name in Japanese; the lookup key against live station names.</summary>
    public required string JapaneseName { get; init; }

    /// <summary>True when the station is a scheduled timing point.</summary>
    public bool IsTimeTaken { get; init; }
}

/// <summary>
///     Route ROM data
/// </summary>
internal static class RouteRom
{
    public static readonly IReadOnlyList<RouteRomData> Stations =
    [
        new() { JapaneseName = "館浜", IsTimeTaken = true },
        new() { JapaneseName = "駒野", IsTimeTaken = true },
        new() { JapaneseName = "河原崎", IsTimeTaken = false },
        new() { JapaneseName = "海岸公園", IsTimeTaken = false },
        new() { JapaneseName = "虹ケ浜", IsTimeTaken = false },
        new() { JapaneseName = "津崎", IsTimeTaken = true },
        new() { JapaneseName = "浜園", IsTimeTaken = true },
        new() { JapaneseName = "羽衣橋", IsTimeTaken = false },
        new() { JapaneseName = "新井川", IsTimeTaken = false },
        new() { JapaneseName = "新野崎", IsTimeTaken = true },
        new() { JapaneseName = "江ノ原", IsTimeTaken = false },
        new() { JapaneseName = "江ノ原信号場", IsTimeTaken = true },
        new() { JapaneseName = "大道寺", IsTimeTaken = true },
        new() { JapaneseName = "藤江", IsTimeTaken = true },
        new() { JapaneseName = "水越", IsTimeTaken = true },
        new() { JapaneseName = "高見沢", IsTimeTaken = true },
        new() { JapaneseName = "日野森", IsTimeTaken = true },
        new() { JapaneseName = "奥峯口", IsTimeTaken = false },
        new() { JapaneseName = "西赤山", IsTimeTaken = true },
        new() { JapaneseName = "赤山町", IsTimeTaken = true }
    ];
}