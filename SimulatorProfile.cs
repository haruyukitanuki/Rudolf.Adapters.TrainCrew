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

        var scenarioId = ResolveScenarioId(trainState.diaName);
        if (_cachedProfile != null) return _cachedProfile;

        _cachedProfile = BuildProfile(trainState, gameState, scenarioId);
        return _cachedProfile;
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
            Vocabularies = new Vocabularies()
        };
    }

    private static VehicleInfo BuildVehicle(IReadOnlyList<CarState> orderedCars, LineDirection direction)
    {
        var cabDirections = DetermineCabDirections(orderedCars);

        var models = orderedCars.Select(state => state.CarModel).Distinct().ToList();
        var name = models.Select(model => model != null ? model + '形' : string.Empty).Aggregate((x, y) => $"{x}+{y}");
        var model = models.Select(model => model ?? string.Empty).Aggregate((x, y) => $"{x}+{y}");

        // ROM data is keyed per model. A TRAIN CREW consist is single-model, so resolve one key only when
        // it's unambiguous; a mixed/unknown consist yields null and emits null rather than misapplying one
        // model's layout to another car. Capabilities/pantograph are then looked up once for the consist.
        var nonNullModels = models.Where(m => m != null).ToList();
        var consistModel = nonNullModels.Count == 1 ? nonNullModels[0] : null;
        var pantographStyle = RomLookup.PantographStyle(consistModel);
        var pantographLayout = RomLookup.PantographLayout(consistModel, orderedCars.Count);

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
                HasPantograph = c.HasPantograph,
                CabDirection = cabDirections[i],
                // Pantograph type/orientation come from the static rolling-stock ROM, gated on the SDK's
                // HasPantograph (the authority for presence); null when absent from ROM or no pantograph.
                PantographType = c.HasPantograph ? pantographStyle : null,
                PantographDirection = c.HasPantograph && pantographLayout != null && i < pantographLayout.Count
                    ? pantographLayout[i]
                    : null,
                Length = 20
            });
        }

        return vehicle;
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
}