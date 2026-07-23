using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Tanuden.Rudolf.Adapters.TrainCrew.Internal;
using Tanuden.Rudolf.Adapters.TrainCrew.Rom;
using Tanuden.Rudolf.Enums;
using Tanuden.Rudolf.Json;
using Tanuden.Rudolf.Profile;
using TrainCrew;

namespace Tanuden.Rudolf.Adapters.TrainCrew;

public sealed partial class TrainCrewRudolfAdapter
{
    /// <inheritdoc cref="IRudolfAdapter.GetProfile" />
    public SimulatorProfile? GetProfile()
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

        var scenarioId = ResolveScenarioId(trainState.diaName, trainState.CarStates);
        if (_cachedProfile is null)
        {
            _cachedProfile = BuildProfile(trainState, gameState, scenarioId);
            return _cachedProfile;
        }
        else
        {
            return _cachedProfile;
        }
    }

    private SimulatorProfile BuildProfile(TrainState trainState, GameState gameState, string scenarioId)
    {
        _lastScreen = FieldMapper.MapGameScreen(gameState.gameScreen);
        var direction = FieldMapper.DetermineLineDirection(trainState.diaName);
        var orderedCars = OrderCars(trainState, direction);

        return new SimulatorProfile
        {
            ScenarioId = scenarioId,
            SentAt = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            Sim = new SimInfo
            {
                Name = "TRAIN CREW",
                Version = string.Empty,
                AdapterName = Constants.AdapterName,
                AdapterVersion = Constants.AdapterVersion
            },
            Scenario = new ScenarioInfo
            {
                Title = trainState.diaName ?? string.Empty,
                Route = "館浜電鉄",
                Author = "acty（アクティー）",
                DiagramNumber = NullIfEmpty(trainState.diaName),
                BoundFor = NullIfEmpty(trainState.BoundFor),
                ServiceType = NullIfEmpty(trainState.Class)
            },
            Vehicle = BuildVehicle(orderedCars, direction),
            Capabilities = BuildCapabilities(),
            Vocabularies = new Vocabularies { SignalPhaseSpeed = BuildSignalPhaseSpeed() }
        };
    }

    private static VehicleInfo BuildVehicle(IReadOnlyList<CarState> orderedCars, LineDirection direction)
    {
        var cabDirections = DetermineCabDirections(orderedCars);

        var models = orderedCars.Select(state => state.CarModel).Distinct().ToList();
        var name = models.Select(model => model != null ? model + '形' : string.Empty).Aggregate((x, y) => $"{x}+{y}");
        var model = models.Select(model => model ?? string.Empty).Aggregate((x, y) => $"{x}+{y}");
        
        var nonNullModels = models.Where(m => m != null).ToList();
        var consistModel = nonNullModels.Count == 1 ? nonNullModels[0] : null;

        var pantographs = DeterminePantographs(orderedCars, cabDirections);

        var vehicle = new VehicleInfo
        {
            Name = name,
            Model = model,
            Operator = "TatehamaElectricRailway",
            Capabilities = RomLookup.Capabilities(consistModel) ?? new VehicleCapabilities(),
            LeadCar = orderedCars.Count == 0 ? 0 : direction == LineDirection.Downbound ? 1 : orderedCars.Count
        };

        for (var i = 0; i < orderedCars.Count; i++)
        {
            var c = orderedCars[i];
            vehicle.Cars.Add(new CarStaticInfo
            {
                CarNo = i + 1,
                Model = c.CarModel ?? string.Empty,
                HasDriverCab = c.HasDriverCab,
                HasConductorCab = c.HasConductorCab,
                HasMotor = c.HasMotor,
                HasPantograph = pantographs[i].Has,
                CabDirection = cabDirections[i],
                PantographType = pantographs[i].Type,
                PantographDirection = pantographs[i].Direction,
                Length = 20
            });
        }

        return vehicle;
    }

    /// <summary>
    ///     Determine Pantograph Directions based on consist and ROM
    /// </summary>
    private static (PantographDirection? Direction, PantographType? Type, bool Has)[] DeterminePantographs(
        IReadOnlyList<CarState> cars, Direction?[] cabDirections)
    {
        var n = cars.Count;
        var result = new (PantographDirection? Direction, PantographType? Type, bool Has)[n];

        var sectionStart = 0;
        for (var i = 0; i < n; i++)
        {
            var boundaryAfter = cars[i].HasDriverCab && cabDirections[i] == Direction.Right
                && i != 0 && i != n - 1;
            if (!boundaryAfter) continue;

            AssignPantographSection(cars, result, sectionStart, i);
            sectionStart = i + 1;
        }

        if (sectionStart < n) AssignPantographSection(cars, result, sectionStart, n - 1);

        return result;
    }

    private static void AssignPantographSection(
        IReadOnlyList<CarState> cars,
        (PantographDirection? Direction, PantographType? Type, bool Has)[] result,
        int start, int endInclusive)
    {
        var model = cars[start].CarModel;
        var length = endInclusive - start + 1;
        var layout = RomLookup.PantographLayout(model, length);
        var style = RomLookup.PantographStyle(model);

        for (var i = start; i <= endInclusive; i++)
        {
            var local = i - start;
            var dir = layout != null && local < layout.Count ? layout[local] : null;
            var has = dir != null;
            result[i] = (dir, has ? style : null, has);
        }
    }

    /// <summary>
    ///     Cab-direction heuristic ported from OpenTetsu's TrainCrewAdapter.
    /// </summary>
    private static Direction?[] DetermineCabDirections(IReadOnlyList<CarState> cars)
    {
        var n = cars.Count;
        var dir = new Direction?[n];

        for (var i = 0; i < n; i++)
        {
            if (!cars[i].HasDriverCab) continue;
            
            dir[i] = i == 0 ? Direction.Left : i == n - 1 ? Direction.Right : Direction.Left;
        }

        for (var i = 0; i < n; i++)
        {
            if (!cars[i].HasDriverCab || i == 0 || i == n - 1) continue;

            if (dir[i - 1] == dir[i])
                dir[i] = dir[i] == Direction.Right ? Direction.Left : Direction.Right;

            if (!cars[i - 1].HasDriverCab) dir[i] = Direction.Right;
        }

        return dir;
    }

    private static Capabilities BuildCapabilities()
    {
        JsonElement E(object value)
        {
            return JsonSerializer.SerializeToElement(value, RudolfJson.Options);
        }

        return new Capabilities
        {
            ["physics.gradient"] = E(true),
            ["physics.perCar"] = E("true"),
            ["ats.richState"] = E("rich"),
            // TRAIN CREW knows only the immediate next speed-limit change, not the full forward list.
            ["speedLimit.next"] = E("single"),
            ["input.command.SetNotch"] = E(true),
            ["input.command.SetPowerNotch"] = E(true),
            ["input.command.SetBrakeNotch"] = E(true),
            ["input.command.SetBrakeSAP"] = E(true),
            ["input.command.SetReverser"] = E(true),
            ["input.command.SetButton"] = E(true),
            ["input.command.SetWiper"] = E(true),
            ["input.command.SetAtoNotch"] = E(true),
            ["input.command.SetDeadman"] = E(true)
        };
    }

    private static Dictionary<string, double?> BuildSignalPhaseSpeed()
    {
        var dict = new Dictionary<string, double?>(StringComparer.Ordinal);
        foreach (var (phase, kmh) in SignalPhaseSpeeds.PhaseSpeedKmh)
            dict[phase.ToString(CultureInfo.InvariantCulture)] = kmh;
        return dict;
    }
}