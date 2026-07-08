using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tanuden.Rudolf.Adapters.TrainCrew.Internal;

internal sealed class WsEnvelope
{
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("data")] public JsonElement Data { get; set; }
}

internal sealed class WsStateData
{
    [JsonPropertyName("myTrainData")] public WsTrainState? MyTrainData { get; set; }
    [JsonPropertyName("trackCircuitList")] public WsTrackCircuit[]? TrackCircuitList { get; set; }

    [JsonPropertyName("otherTrainDataList")]
    public WsOtherTrain[]? OtherTrainDataList { get; set; }

    [JsonPropertyName("signalDataList")] public WsSignal[]? SignalDataList { get; set; }

    [JsonPropertyName("interlockDataList")]
    public WsInterlock[]? InterlockDataList { get; set; }
}

internal sealed class WsTrainState
{
    [JsonPropertyName("KilometerPost")] public double KilometerPost { get; set; } = -1.0;

    /// <summary>ATS state bitmask on the wire (int), distinct from the DLL's string ATS_State.</summary>
    [JsonPropertyName("ATS_State")]
    public int? AtsState { get; set; }
}

internal sealed class WsTrackCircuit
{
    public bool On { get; set; }
    public string Last { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

internal sealed class WsOtherTrain
{
    public string Name { get; set; } = string.Empty;
    public string Class { get; set; } = string.Empty;
    public string For { get; set; } = string.Empty; // wire field is "For" (destination)
    public bool OnTrack { get; set; }
    public bool AutoDriveEnable { get; set; }
    public double Speed { get; set; }
    public double SpeedTo { get; set; }
    public bool AllClose { get; set; }
    public double TotalLength { get; set; }
    public bool IsJieiR { get; set; }
    public string DebugMsg { get; set; } = string.Empty;
}

internal sealed class WsSignal
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Raw WS Phase ordinal (0=None,1=R,2=YY,3=Y,4=YG,5=G); preserved verbatim.</summary>
    public int Phase { get; set; }
}

internal sealed class WsInterlock
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Opaque passthrough (always [] in the capture; shape unverified live).</summary>
    public JsonElement[]? Routes { get; set; }
}

internal sealed class TcTrackCircuitsExtension
{
    public List<TcTrackCircuit> List = new();
}

internal sealed class TcTrackCircuit
{
    public string Last = string.Empty;
    public string Name = string.Empty;
    public bool On;
}

internal sealed class TcOtherTrainsExtension
{
    public List<TcOtherTrain> List = new();
}

internal sealed class TcOtherTrain
{
    public bool AllClose;
    public bool AutoDriveEnable;
    public string BoundFor = string.Empty; // normalized from WS "For"
    public string Class = string.Empty;
    public string DebugMsg = string.Empty;
    public bool IsJieiR;
    public string Name = string.Empty;
    public bool OnTrack;
    public double Speed;
    public double SpeedTo;
    public double TotalLength;
}

internal sealed class TcSignalsExtension
{
    public List<TcSignal> List = new();
}

internal sealed class TcSignal
{
    public string Name = string.Empty;
    public int Phase; // raw WS ordinal, verbatim
}

internal sealed class TcInterlockingExtension
{
    public List<TcInterlock> List = new();
}

internal sealed class TcInterlock
{
    public string Name = string.Empty;
    public JsonElement[] Routes = Array.Empty<JsonElement>();
}

/// <summary>
///     Immutable WS-derived snapshot, swapped atomically (volatile) by the background WS loop.
///     Holds the scalar supplements plus the four full-line extension blocks pre-serialized to
///     <see cref="JsonElement" /> (built once per frame, not per emit). Null block = absent on that frame.
/// </summary>
internal sealed record WsSnapshot(
    double KilometerPost,
    int? AtsStateBitmask,
    JsonElement? TrackCircuits,
    JsonElement? OtherTrains,
    JsonElement? Signals,
    JsonElement? Interlocking);