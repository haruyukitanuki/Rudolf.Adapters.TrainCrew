using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Tanuden.Rudolf.Adapters.TrainCrew.Internal;
using Tanuden.Rudolf.Adapters.TrainCrew.Rom;
using Tanuden.Rudolf.Enums;
using Tanuden.Rudolf.Sections;
using TrainCrew;
using GameState = TrainCrew.GameState;
using Station = TrainCrew.Station;

namespace Tanuden.Rudolf.Adapters.TrainCrew;

public sealed partial class TrainCrewRudolfAdapter
{
    /// <inheritdoc cref="IRudolfAdapter.GetCurrentFrame" />
    public OutputDataFrame? GetCurrentFrame()
    {
        TrainState trainState;
        try
        {
            trainState = TrainCrewInput.GetTrainState();
        }
        catch
        {
            return null;
        }

        var gameState = TrainCrewInput.gameState;

        TrainCrewInput.RequestData(DataRequest.Signal | DataRequest.Switch);
        var signals = TrainCrewInput.signals;
        var switches = TrainCrewInput.trainSwitch;

        if (trainState.stationList.Count == 0) TrainCrewInput.RequestStaData();

        var scenarioId = ResolveScenarioId(trainState.diaName, trainState.CarStates.Count);

        return BuildFrame(
            trainState, signals, switches, gameState, scenarioId,
            trainState.NowTime.TotalSeconds, DateTimeOffset.UtcNow.Ticks);
    }

    private OutputDataFrame BuildFrame(
        TrainState trainState,
        List<SignalInfo>? signals,
        TrainSwitch? switches,
        GameState gameState,
        string scenarioId,
        double elapsed,
        long tick)
    {
        var snapshot = _wsSnapshot;
        var lineDirection = FieldMapper.DetermineLineDirection(trainState.diaName);
        _lastScreen = FieldMapper.MapGameScreen(gameState.gameScreen);

        var orderedCars = OrderCars(trainState, lineDirection);
        var stationList = trainState.stationList ?? new List<Station>();

        var frame = new OutputDataFrame
        {
            ScenarioId = scenarioId,
            SentAt = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            Time = new Time
            {
                Sim = FieldMapper.FormatClock(trainState.NowTime),
                DateKnown = false,
                Elapsed = elapsed,
                Tick = tick
            },
            Diagram = BuildDiagram(trainState, lineDirection),
            Stations = BuildStations(stationList, trainState.nowStaIndex),
            Physics = new Physics
            {
                Speed = trainState.Speed,
                FromStartDistance = trainState.TotalLength,
                AbsoluteDistance = snapshot is { KilometerPost: > 0.0 } ? snapshot.KilometerPost : null,
                Gradient = trainState.gradient,
                MrPressure = trainState.MR_Press
            },
            Controllers = new Controllers
            {
                PowerNotch = trainState.Pnotch,
                BrakeNotch = trainState.Bnotch,
                Reverser = (Reverser)Math.Clamp(trainState.Reverser, -1, 1)
            },
            Doors = BuildDoors(trainState, orderedCars),
            Lamps = BuildLamps(trainState),
            Ats = new Ats
            {
                Class = NullIfEmpty(trainState.ATS_Class),
                Speed = FieldMapper.ParseAtsSpeed(trainState.ATS_Speed),
                State = NullIfEmpty(trainState.ATS_State),
                RichState = FieldMapper.MapAtsRichState(snapshot?.AtsStateBitmask)
            },
            Signals = BuildSignals(signals),
            SpeedLimit = BuildSpeedLimit(trainState, signals),
            Cars = BuildCars(orderedCars),
            Switches = BuildSwitches(switches),
            GameState = new Sections.GameState
            {
                Screen = _lastScreen,
                CrewRole = FieldMapper.MapCrewRole(gameState.crewType),
                DriveMode = FieldMapper.MapDriveMode(gameState.driveMode),
                IsOneman = trainState.isOneman
            }
        };

        var extensions = BuildExtensions(snapshot);
        if (extensions.Count > 0) frame.Extensions = extensions;

        return frame;
    }

    private static Diagram BuildDiagram(TrainState trainState, LineDirection lineDirection)
    {
        return new Diagram
        {
            TrainNumber = NullIfEmpty(trainState.diaName),
            BoundFor = NullIfEmpty(trainState.BoundFor),
            ServiceType = NullIfEmpty(trainState.Class),
            Direction = lineDirection,
            RunNumber = FieldMapper.DetermineRunNumber(trainState.diaName)
        };
    }

    private static Stations BuildStations(IReadOnlyList<Station> stationList, int nowStaIndex)
    {
        var stations = new Stations();
        for (var i = 0; i < stationList.Count; i++)
        {
            var sta = stationList[i];
            stations.List.Add(new Sections.Station
            {
                Index = i,
                Name = sta.Name ?? string.Empty,
                FromStartDistance = sta.TotalLength,
                AbsoluteDistance = null,
                DoorSide = sta.doorDir == DoorDir.LeftSide ? -1 : 1,
                StopType = FieldMapper.MapStopType(sta.stopType),
                Arrival = i == 0 ? null : FieldMapper.FormatClock(sta.ArvTime),
                Departure = i == stationList.Count - 1 ? null : FieldMapper.FormatClock(sta.DepTime),
                StopPositionName = NullIfEmpty(sta.StopPosName),
                // Timing-point flag comes from the static route ROM, matched on the live station name; null
                // when the name has no ROM entry. Stop-position car-counts aren't modelled in the ROM.
                IsTimeTaken = RomLookup.IsTimeTaken(sta.Name),
                StopPositions = null
            });
        }

        stations.NextIndex = nowStaIndex >= 0 && nowStaIndex < stationList.Count ? nowStaIndex : null;
        stations.CurrentIndex = null;
        return stations;
    }

    private static Doors BuildDoors(TrainState trainState, IReadOnlyList<CarState> orderedCars)
    {
        var doors = new Doors { AllClosed = trainState.AllClose };
        for (var i = 0; i < orderedCars.Count; i++)
            doors.PerCar.Add(new CarDoorState
            {
                CarNo = i + 1,
                SideOpened = orderedCars[i].DoorClose ? (int)SideOpened.Closed : (int)SideOpened.OpenSideUnknown
            });

        return doors;
    }

    private static Lamps BuildLamps(TrainState trainState)
    {
        var lamps = new Lamps();
        var src = trainState.Lamps;
        if (src == null) return lamps;

        void Map(string key, PanelLamp lamp)
        {
            if (src.TryGetValue(lamp, out var on)) lamps.Values[key] = on ? 1 : 0;
        }

        Map("doorClose", PanelLamp.DoorClose);
        Map("atsReady", PanelLamp.ATS_Ready);
        Map("atsBrakeApply", PanelLamp.ATS_BrakeApply);
        Map("atsOpen", PanelLamp.ATS_Open);
        Map("regenerative", PanelLamp.RegenerativeBrake);
        Map("ebTimer", PanelLamp.EB_Timer);
        Map("emergencyBrake", PanelLamp.EmagencyBrake);
        Map("overload", PanelLamp.Overload);
        return lamps;
    }

    private static Signals BuildSignals(List<SignalInfo>? signals)
    {
        var result = new Signals();
        if (signals == null) return result;

        foreach (var s in signals)
        {
            var signal = new Signal
            {
                Name = s.name,
                Type = FieldMapper.DetermineSignalType(s.name),
                Phase = FieldMapper.MapSignalPhase(s.phase),
                Distance = s.distance
            };

            if (s.beacons != null)
                foreach (var b in s.beacons)
                    signal.Transponders.Add(new Transponder
                    {
                        Category = TransponderCategory.Signal,
                        Code = null,
                        SpeedLimit = b.speed,
                        Distance = b.distance
                    });

            result.List.Add(signal);
        }

        // Nearest-first (ascending distance) per the spec: list[0] is the closest upcoming signal.
        // The TRAIN CREW SDK doesn't guarantee this ordering, so sort explicitly.
        result.List.Sort((a, b) => a.Distance.CompareTo(b.Distance));

        return result;
    }

    private static SpeedLimit BuildSpeedLimit(TrainState trainState, List<SignalInfo>? signals)
    {
        bool HasTransponderNear(double distance)
        {
            if (signals == null) return false;

            foreach (var s in signals)
            {
                if (s.beacons == null) continue;

                foreach (var b in s.beacons)
                    if (Math.Abs(b.distance - distance) <= 1.0)
                        return true;
            }

            return false;
        }

        static bool IsSignalSpeed(double v)
        {
            return v == 0.0 || v == 25.0 || v == 55.0 || v == 80.0;
        }

        SpeedLimitType TypeFor(double limit, double distance, bool useDistance)
        {
            return IsSignalSpeed(limit) || (useDistance ? HasTransponderNear(distance) : HasTransponderNear(0.0))
                ? SpeedLimitType.Signal
                : SpeedLimitType.SpeedLimit;
        }

        var speedLimit = new SpeedLimit
        {
            Current = trainState.speedLimit,
            CurrentType = TypeFor(trainState.speedLimit, 0.0, false)
        };

        // TRAIN CREW exposes only the single immediate next change (capability: speedLimit.next="single").
        // Emit it as a one-element list; null when there's no upcoming change.
        if (trainState.nextSpeedLimit > 0.0)
            speedLimit.Next = new List<SpeedLimitNext>
            {
                new()
                {
                    Limit = trainState.nextSpeedLimit,
                    Distance = trainState.nextSpeedLimitDistance,
                    Type = TypeFor(trainState.nextSpeedLimit, trainState.nextSpeedLimitDistance, true)
                }
            };

        return speedLimit;
    }

    private static Cars BuildCars(IReadOnlyList<CarState> orderedCars)
    {
        var cars = new Cars();
        for (var i = 0; i < orderedCars.Count; i++)
        {
            var c = orderedCars[i];
            cars.List.Add(new Car
            {
                CarNo = i + 1,
                BcPressure = c.BC_Press,
                Amperage = c.Ampare,
                OccupancyRate = c.occupancyRate
            });
        }

        return cars;
    }

    private static Switches BuildSwitches(TrainSwitch? switches)
    {
        return new Switches
        {
            HornAir = switches?.Horn_Air ?? false,
            HornElectric = switches?.Horn_Electric ?? false,
            BuzzerDriver = switches?.buzzerM ?? false,
            BuzzerConductor = switches?.buzzerC ?? false,
            Headlights = false,
            HighBeam = switches?.highBeam ?? false,
            Wiper = null
        };
    }

    private static Dictionary<string, JsonElement> BuildExtensions(WsSnapshot? snapshot)
    {
        var extensions = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        if (snapshot == null) return extensions;

        if (snapshot.TrackCircuits is { } tc) extensions["traincrew:trackCircuits"] = tc;
        if (snapshot.OtherTrains is { } ot) extensions["traincrew:otherTrains"] = ot;
        if (snapshot.Signals is { } sg) extensions["traincrew:signals"] = sg;
        if (snapshot.Interlocking is { } il) extensions["traincrew:interlocking"] = il;

        return extensions;
    }
}