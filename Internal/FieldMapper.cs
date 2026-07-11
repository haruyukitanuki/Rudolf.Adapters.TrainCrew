using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Tanuden.Rudolf.Enums;
using Tanuden.Rudolf.Sections;
using TrainCrew;
using DriveMode = Tanuden.Rudolf.Enums.DriveMode;
using GameScreen = Tanuden.Rudolf.Enums.GameScreen;
using StopType = Tanuden.Rudolf.Enums.StopType;

namespace Tanuden.Rudolf.Adapters.TrainCrew.Internal;

/// <summary>Pure mapping helpers between TRAIN CREW SDK values and Rudolf vocabulary.</summary>
internal static class FieldMapper
{
    internal static string FormatClock(TimeSpan ts)
    {
        return string.Format(CultureInfo.InvariantCulture, "{0:D2}:{1:D2}:{2:D2}", (int)ts.TotalHours, ts.Minutes,
            ts.Seconds);
    }

    internal static int MapSignalPhase(string? phase)
    {
        switch ((phase ?? string.Empty).Trim().ToUpperInvariant())
        {
            case "R": return (int)SignalPhase.R;        // 1
            case "YY": return (int)SignalPhase.YY;      // 2
            case "Y": return (int)SignalPhase.Y;        // 3
            case "YG": return (int)SignalPhase.YG;      // 4
            case "YGF": return (int)SignalPhase.YGF;    // 5
            case "G": return (int)SignalPhase.G;        // 6
            case "GG": return (int)SignalPhase.GG;      // 7
            default: return (int)SignalPhase.Disabled;  // 0
        }
    }

    internal static double? ParseAtsSpeed(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var trimmed = raw!.Trim();
        if (trimmed == "F") return -1.0;

        if (trimmed == "無表示") return null;

        if (!double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return null;
        
        // ATS Speed 3000 is output by traincrew if it is in free mode
        return v >= 3000.0 ? -1.0 : v;
    }

    internal static AtsRichState? MapAtsRichState(int? bitmask)
    {
        if (bitmask is null or 0) return null;

        var mask = bitmask.Value;
        var codes = new List<string>();
        var names = new List<string>();
        var severity = new List<int>();
        var types = new List<AtsRichStateType>();

        void Add(int bit, string code, string name, int sev, AtsRichStateType type)
        {
            if ((mask & bit) != 0)
            {
                codes.Add(code);
                names.Add(name);
                severity.Add(sev);
                types.Add(type);
            }
        }

        Add(1, "P", "P", 0, AtsRichStateType.SpeedCheck);
        Add(1 << 1, "P_APPROACH", "P接近", 1, AtsRichStateType.PApproach);
        Add(1 << 2, "B_APPLICATION", "B動作", 1, AtsRichStateType.BApplication);
        Add(1 << 3, "EB", "EB", 2, AtsRichStateType.EbApplication);
        Add(1 << 4, "TERMINAL_P", "終端P", 0, AtsRichStateType.TerminalP);
        Add(1 << 5, "STOP_P", "停P", 0, AtsRichStateType.StopP);
        // bit 6 (1 << 6) = OFF: no rich-state entry.

        if (codes.Count == 0) return null;

        return new AtsRichState
        {
            Code = codes.ToArray(),
            Name = names.ToArray(),
            Severity = severity.ToArray(),
            Type = types.ToArray()
        };
    }

    internal static SignalType DetermineSignalType(string? name)
    {
        var n = name ?? string.Empty;
        if (n.Contains("入換")) return SignalType.Shunt;

        if (n.Contains("場内")) return SignalType.Home;

        if (n.Contains("出発")) return SignalType.Departure;

        return SignalType.Block;
    }

    internal static StopType MapStopType(global::TrainCrew.StopType stopType)
    {
        return stopType switch
        {
            global::TrainCrew.StopType.StopForPassenger => StopType.PassengerStop,
            global::TrainCrew.StopType.StopForOperation => StopType.OperationStop,
            global::TrainCrew.StopType.Passing => StopType.Passing,
            _ => StopType.PassengerStop
        };
    }

    internal static GameScreen MapGameScreen(global::TrainCrew.GameScreen screen)
    {
        return screen switch
        {
            global::TrainCrew.GameScreen.MainGame => GameScreen.MainGame,
            global::TrainCrew.GameScreen.MainGame_Pause => GameScreen.Pause,
            global::TrainCrew.GameScreen.MainGame_Loading => GameScreen.Loading,
            global::TrainCrew.GameScreen.Menu => GameScreen.Menu,
            global::TrainCrew.GameScreen.Result => GameScreen.Result,
            global::TrainCrew.GameScreen.Title => GameScreen.Title,
            global::TrainCrew.GameScreen.NotRunningGame => GameScreen.NotRunning,
            _ => GameScreen.Other
        };
    }

    internal static CrewRole? MapCrewRole(CrewType crew)
    {
        return crew switch
        {
            CrewType.Driver => CrewRole.Driver,
            CrewType.Conductor => CrewRole.Conductor,
            _ => null
        };
    }

    internal static DriveMode? MapDriveMode(global::TrainCrew.DriveMode mode)
    {
        return mode switch
        {
            global::TrainCrew.DriveMode.Normal => DriveMode.Scored,
            global::TrainCrew.DriveMode.Free => DriveMode.Unscored,
            global::TrainCrew.DriveMode.RTA => DriveMode.Other,
            _ => null
        };
    }

    internal static LineDirection DetermineLineDirection(string? diaName)
    {
        var digits = new string((diaName ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length == 0) digits = "0";

        var lastDigit = digits[digits.Length - 1] - '0';
        return lastDigit % 2 == 0 ? LineDirection.Upbound : LineDirection.Downbound;
    }

    internal static string? DetermineRunNumber(string? diaName)
    {
        if (string.IsNullOrEmpty(diaName)) return null;

        var runNumber = diaName!;

        if (char.IsLetter(runNumber[runNumber.Length - 1])) runNumber = runNumber.Substring(0, runNumber.Length - 1);

        if (runNumber.Length > 0 && char.IsLetter(runNumber[runNumber.Length - 1]))
            runNumber = runNumber.Substring(0, runNumber.Length - 1);

        if (runNumber.Length > 0 && !char.IsDigit(runNumber[0])) runNumber = runNumber.Substring(1);

        if (runNumber.Length < 2 || !runNumber.All(char.IsDigit)) return null;

        var lastTwoDigits = runNumber.Substring(runNumber.Length - 2);
        var runNumberInt = int.Parse(lastTwoDigits, CultureInfo.InvariantCulture);

        switch (int.Parse(runNumber, CultureInfo.InvariantCulture))
        {
            case >= 6000:
                runNumberInt += 200;
                break;
            case >= 3000:
                runNumberInt += 100;
                break;
        }

        if (runNumberInt % 2 != 0) runNumberInt -= 1;

        return runNumberInt.ToString(CultureInfo.InvariantCulture);
    }

    internal static InputAction MapAction(string action)
    {
        return action switch
        {
            // VehicleAction (physical cab/train controls).
            "EBReset" => InputAction.EBReset,
            "GradientStart" => InputAction.GradientStart,
            "SafetyBrake" => InputAction.SafetyBrakeSw,
            "SnowBrake" => InputAction.SnowBrake,
            "HornAir" => InputAction.HornAir,
            "HornElectric" => InputAction.HornEle,
            "Buzzer" => InputAction.Buzzer,
            "DoorOpenLeft" => InputAction.DoorOpnLeft,
            "DoorCloseLeft" => InputAction.DoorClsLeft,
            "DoorOpenRight" => InputAction.DoorOpnRight,
            "DoorCloseRight" => InputAction.DoorClsRight,
            "DoorReopen" => InputAction.ReOpenSW,
            "DoorKey" => InputAction.DoorKey,
            "PartialDoor" => InputAction.door34sw,
            "DoorCut" => InputAction.doorCutSw,
            "BoardingPrompt" => InputAction.JoukouSokusin,
            "InCarBroadcast" => InputAction.Housou,
            "HeadLightLow" => InputAction.LightLow,
            "HeadLight" => InputAction.HeadLightSw,
            "CabinLight" => InputAction.roomLightSw,
            "CrewRoomLight" => InputAction.CrewRoomLightSw,
            "InstrumentLight" => InputAction.MeterLightSw,
            // GameAction (camera/view/UI/sim-meta).
            "ExteriorView" => InputAction.ViewChange,
            "DriverAlternateView" => InputAction.DriverViewB,
            "ConductorAlternateView" => InputAction.ConductorViewB,
            "LeftWindowView" => InputAction.LeftWindow,
            "RightWindowView" => InputAction.RightWindow,
            "TogglePauseMenu" => InputAction.PauseMenu,
            "ToggleDiagramDisplay" => InputAction.ViewDiagram,
            "ToggleGUI" => InputAction.ViewUserInterface,
            "ToggleCrewDoor" => InputAction.CrewDoor,
            "ToggleCrewWindow" => InputAction.CrewWindow,
            // Custom/non-spec passthrough: accept any SDK-native member name verbatim.
            _ => Enum.TryParse<InputAction>(action, out var native)
                ? native
                : throw new NotSupportedException($"Action '{action}' is not supported.")
        };
    }

    internal static int MapWiper(Wiper state)
    {
        return state switch
        {
            Wiper.Off => 0,
            Wiper.Intermittent => 1,
            Wiper.Low => 2,
            Wiper.High => 3,
            _ => 0
        };
    }
}