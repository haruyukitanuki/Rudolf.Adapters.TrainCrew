using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using Tanuden.Rudolf.Adapters.TrainCrew.Internal;
using Tanuden.Rudolf.Enums;
using TrainCrew;
using Websocket.Client;
using GameScreen = Tanuden.Rudolf.Enums.GameScreen;

namespace Tanuden.Rudolf.Adapters.TrainCrew;

/// <summary>
///     <see cref="IRudolfAdapter" /> for TRAIN CREW: reads state and dispatches input IO 1 and
///     additional rich info is supplemented by IO 2 via WS (<c>ws://localhost:50300/</c>)
/// </summary>
public sealed partial class TrainCrewRudolfAdapter : IRudolfAdapter
{
    private const string WsUri = "ws://localhost:50300/";
    private static readonly TimeSpan WsErrorReconnect = TimeSpan.FromSeconds(10);

    private static readonly JsonSerializerOptions WsReadOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly string DataRequestMsg =
        JsonSerializer.Serialize(new WsCommand("DataRequest", ["all"]));

    private SimulatorProfile? _cachedProfile;

    // Last-seen diagram name + train info + game screen, tracked across frames for scenario-id rotation and IsReady.
    private string? _lastDiaName;
    private List<SimpleTrainCarInfo> _lastTrainInfo = new List<SimpleTrainCarInfo>();
    private volatile GameScreen _lastScreen = GameScreen.NotRunning;

    // Scenario identity — owned here so callers don't need to manage it.
    private string _scenarioId = Guid.NewGuid().ToString();
    private IWebsocketClient? _wsClient;
    private volatile bool _wsConnected;
    private volatile string? _wsLastError;
    private long _wsLastFrameAtUtcTicks; // 0 = never; written/read via Interlocked

    private volatile WsSnapshot? _wsSnapshot;
    private IDisposable? _wsSubscriptions;

    /// <inheritdoc cref="IRudolfAdapter.IsReady" />
    public bool IsReady => _lastScreen is GameScreen.MainGame or GameScreen.Pause or GameScreen.Loading;

    /// <inheritdoc cref="IRudolfAdapter.Start" />
    public void Start(CancellationToken ct = default)
    {
        StartWebSocket(ct);
    }

    /// <inheritdoc cref="IDisposable.Dispose" />
    public void Dispose()
    {
        _wsSubscriptions?.Dispose();
        _wsSubscriptions = null;

        try
        {
            _wsClient?.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Intentionally empty. Do nothing
        }

        _wsClient = null;
        _wsConnected = false;
        _wsSnapshot = null;
    }

    // Rotates _scenarioId when the effective diaName or consist changes and invalidates the profile cache.
    private string ResolveScenarioId(string? rawDiaName, List<CarState> carStates)
    {
        // Build new train info from trainState
        List<SimpleTrainCarInfo> trainInfo = GetSimpleTrainInfo(carStates);

        if (rawDiaName != _lastDiaName || !TrainInfosEqual(trainInfo, _lastTrainInfo))
        {
            _scenarioId = Guid.NewGuid().ToString();
            _lastDiaName = rawDiaName;
            _lastTrainInfo = new List<SimpleTrainCarInfo>(trainInfo); // copy contents to new list
            _cachedProfile = null;
        }

        return _scenarioId;
    }

    private static List<CarState> OrderCars(TrainState trainState, LineDirection direction)
    {
        var cars = new List<CarState>(trainState.CarStates ?? new List<CarState>());

        // TC stores cars Otebashi-first regardless of run direction; reverse for inbound so the list
        // is always left-to-right visual order.
        if (direction == LineDirection.Upbound) cars.Reverse();

        return cars;
    }

    private static string? NullIfEmpty(string? value)
    {
        return string.IsNullOrEmpty(value) ? null : value;
    }

    // Typed handshake, serialized once, so the wire keys ("command"/"args") are compiler-checked and
    // greppable rather than a bare string literal.
    private sealed record WsCommand(
        [property: JsonPropertyName("command")]
        string Command,
        [property: JsonPropertyName("args")] string[] Args);
}