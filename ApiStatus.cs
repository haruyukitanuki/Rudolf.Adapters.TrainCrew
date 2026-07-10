using System;

namespace Tanuden.Rudolf.Adapters.TrainCrew;

/// <summary>
///     Snapshot of which channels the adapter currently has available + other statuses
/// </summary>
internal sealed class ApiStatus
{
    /// <summary>True when the DLL is reporting an in-game screen (MainGame / Pause / Loading).</summary>
    public bool IsDllActive { get; init; }

    /// <summary>True when the WebSocket link is established and has produced at least one frame.</summary>
    public bool IsWsConnected { get; init; }

    /// <summary>Timestamp of the last WebSocket frame received; null when none yet.</summary>
    public DateTimeOffset? WsLastFrameAt { get; init; }

    /// <summary>Most recent WebSocket error message; null when the link is healthy or never started.</summary>
    public string? WsLastError { get; init; }
}