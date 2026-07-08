using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Reactive.Disposables;
using System.Text.Json;
using System.Threading;
using Tanuden.Rudolf.Adapters.TrainCrew.Internal;
using Tanuden.Rudolf.Enums;
using Tanuden.Rudolf.Json;
using Websocket.Client;

namespace Tanuden.Rudolf.Adapters.TrainCrew;

public sealed partial class TrainCrewRudolfAdapter
{
    /// <inheritdoc cref="IRudolfAdapter.GetAdapterExtensions" />
    public IReadOnlyDictionary<string, JsonElement> GetAdapterExtensions()
    {
        return new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["status"] = JsonSerializer.SerializeToElement(GetApiStatus(), RudolfJson.Options)
        };
    }

    /// <summary>Current availability of the DLL and WebSocket channels.</summary>
    private ApiStatus GetApiStatus()
    {
        var ticks = Interlocked.Read(ref _wsLastFrameAtUtcTicks);
        return new ApiStatus
        {
            IsDllActive = _lastScreen is GameScreen.MainGame or GameScreen.Pause or GameScreen.Loading,
            IsWsConnected = _wsConnected && _wsSnapshot != null,
            WsLastFrameAt = ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero),
            WsLastError = _wsLastError
        };
    }

    /// <summary>
    ///     Start the WebSocket link (idempotent). The Websocket.Client package owns reconnection/backoff and
    ///     fragment reassembly; we just (re)send the DataRequest on each (re)connect and parse each message.
    ///     The link ends when the adapter is disposed or <paramref name="ct" /> fires.
    /// </summary>
    private void StartWebSocket(CancellationToken ct = default)
    {
        if (_wsClient != null) return; // already started

        var client = new WebsocketClient(new Uri(WsUri))
        {
            IsReconnectionEnabled = true,
            ReconnectTimeout = null, // no idle watchdog: TRAIN CREW may legitimately go quiet on pause/menu
            ErrorReconnectTimeout = WsErrorReconnect
        };

        // ReconnectionHappened fires on the initial connect and every reconnect, so it is the one place to
        // (re)send the DataRequest subscription.
        var recon = client.ReconnectionHappened.Subscribe(_ =>
        {
            _wsConnected = true;
            _wsLastError = null;
            client.Send(DataRequestMsg);
        });
        var disc = client.DisconnectionHappened.Subscribe(info =>
        {
            _wsConnected = false;
            _wsSnapshot = null; // preserves IsWsConnected == (_wsConnected && _wsSnapshot != null)
            _wsLastError = info.Exception?.Message ?? info.CloseStatusDescription;
        });
        var msg = client.MessageReceived.Subscribe(m =>
        {
            if (m.MessageType == WebSocketMessageType.Text && m.Text != null) ProcessFrame(m.Text);
        });

        _wsSubscriptions = new CompositeDisposable(recon, disc, msg);
        _wsClient = client;
        ct.Register(Dispose); // honour the Start(ct) contract: ct fires → tear down
        _ = client.Start();
    }

    private void ProcessFrame(string json)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<WsEnvelope>(json, WsReadOptions);
            if (envelope == null ||
                envelope.Type != "TrainCrewStateData") return; // ignore RecvBeaconStateData and anything else

            var data = envelope.Data.Deserialize<WsStateData>(WsReadOptions);
            if (data == null) return;

            _wsSnapshot = BuildSnapshot(data);
            Interlocked.Exchange(ref _wsLastFrameAtUtcTicks, DateTimeOffset.UtcNow.UtcTicks);
        }
        catch (JsonException ex)
        {
            _wsLastError = "frame parse: " + ex.Message;
        }
    }

    private static WsSnapshot BuildSnapshot(WsStateData data)
    {
        var km = data.MyTrainData?.KilometerPost ?? -1.0;
        var bitmask = data.MyTrainData?.AtsState;

        JsonElement? trackCircuits = data.TrackCircuitList == null
            ? null
            : JsonSerializer.SerializeToElement(
                new TcTrackCircuitsExtension
                {
                    List = data.TrackCircuitList
                        .Select(x => new TcTrackCircuit { On = x.On, Last = x.Last, Name = x.Name })
                        .ToList()
                },
                RudolfJson.Options);

        JsonElement? otherTrains = data.OtherTrainDataList == null
            ? null
            : JsonSerializer.SerializeToElement(
                new TcOtherTrainsExtension
                {
                    List = data.OtherTrainDataList.Select(x => new TcOtherTrain
                    {
                        Name = x.Name,
                        Class = x.Class,
                        BoundFor = x.For,
                        OnTrack = x.OnTrack,
                        AutoDriveEnable = x.AutoDriveEnable,
                        Speed = x.Speed,
                        SpeedTo = x.SpeedTo,
                        AllClose = x.AllClose,
                        TotalLength = x.TotalLength,
                        IsJieiR = x.IsJieiR,
                        DebugMsg = x.DebugMsg
                    }).ToList()
                },
                RudolfJson.Options);

        JsonElement? signals = data.SignalDataList == null
            ? null
            : JsonSerializer.SerializeToElement(
                new TcSignalsExtension
                {
                    List = data.SignalDataList.Select(x => new TcSignal { Name = x.Name, Phase = x.Phase }).ToList()
                },
                RudolfJson.Options);

        JsonElement? interlocking = data.InterlockDataList == null
            ? null
            : JsonSerializer.SerializeToElement(
                new TcInterlockingExtension
                {
                    List = data.InterlockDataList
                        .Select(x => new TcInterlock { Name = x.Name, Routes = x.Routes ?? Array.Empty<JsonElement>() })
                        .ToList()
                },
                RudolfJson.Options);

        return new WsSnapshot(km, bitmask, trackCircuits, otherTrains, signals, interlocking);
    }
}