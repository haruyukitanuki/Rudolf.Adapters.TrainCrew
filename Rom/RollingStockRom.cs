using System.Collections.Generic;
using Tanuden.Rudolf.Enums;
using Tanuden.Rudolf.Profile;

namespace Tanuden.Rudolf.Adapters.TrainCrew.Rom;

/// <summary>Pantograph mounting style, in the TRAIN CREW ROM's own vocabulary.</summary>
internal enum RomPantographType
{
    SingleArm,
    ScissorArm
}

/// <summary>
///     ROM for a single rolling-stock model
/// </summary>
internal sealed record RollingStockRomData
{
    public required RomPantographType PantographType { get; init; }
    public required IReadOnlyDictionary<int, IReadOnlyList<PantographDirection?>> PantographDirection { get; init; }

    public required VehicleCapabilities Capabilities { get; init; }
}

/// <summary>
///     Rolling stock ROM data
/// </summary>
internal static class RollingStockRom
{
    private const RomPantographType SingleArm = RomPantographType.SingleArm;
    private const RomPantographType ScissorArm = RomPantographType.ScissorArm;

    private const PantographDirection Left = PantographDirection.Left;
    private const PantographDirection Right = PantographDirection.Right;
    private const PantographDirection Both = PantographDirection.Both;

    public static readonly IReadOnlyDictionary<string, RollingStockRomData> Models =
        new Dictionary<string, RollingStockRomData>
        {
            ["5320"] = Stock(SingleArm,
                [
                    Consist(4, Right, null, null, Left)
                ], new VehicleCapabilities
                {
                    MasconType = MasconType.OneHandle,
                    MasconBrakeType = MasconBrakeType.Notched,
                    PowerNotches = 5,
                    BrakeNotches = 7,
                    EbNotch = -8,
                    HoldingBrakeNotches = 1,
                    CpStartPressure = 640,
                    CpStopPressure = 780
                }
            ),
            ["5300"] = Stock(SingleArm,
            [
                Consist(2, Both, null),
                Consist(4, null, Both, null, null)
            ], new VehicleCapabilities
            {
                MasconType = MasconType.OneHandle,
                MasconBrakeType = MasconBrakeType.Notched,
                PowerNotches = 5,
                BrakeNotches = 7,
                EbNotch = -8,
                HoldingBrakeNotches = 1,
                CpStartPressure = 640,
                CpStopPressure = 780
            }),
            ["4300"] = Stock(SingleArm,
            [
                Consist(2, Both, null),
                Consist(4, null, Both, null, null)
            ], new VehicleCapabilities
            {
                MasconType = MasconType.OneHandle,
                MasconBrakeType = MasconBrakeType.Notched,
                PowerNotches = 5,
                BrakeNotches = 7,
                EbNotch = -8,
                HoldingBrakeNotches = 1,
                CpStartPressure = 640,
                CpStopPressure = 780
            }),
            ["4321"] = Stock(SingleArm,
            [
                Consist(4, null, Both, null, null)
            ], new VehicleCapabilities
            {
                MasconType = MasconType.OneHandle,
                MasconBrakeType = MasconBrakeType.Notched,
                PowerNotches = 5,
                BrakeNotches = 7,
                EbNotch = -8,
                HoldingBrakeNotches = 1,
                CpStartPressure = 640,
                CpStopPressure = 780
            }),
            ["4000"] = Stock(ScissorArm,
            [
                Consist(6, Right, null, Left, null, null, Left)
            ], new VehicleCapabilities
            {
                MasconType = MasconType.TwoHandle,
                MasconBrakeType = MasconBrakeType.Notched,
                PowerNotches = 5,
                BrakeNotches = 7,
                EbNotch = -8,
                HoldingBrakeNotches = 3,
                CpStartPressure = 640,
                CpStopPressure = 780
            }),
            ["4000R"] = Stock(SingleArm,
            [
                Consist(6, Right, null, Left, null, null, Left)
            ], new VehicleCapabilities
            {
                MasconType = MasconType.TwoHandle,
                MasconBrakeType = MasconBrakeType.Notched,
                PowerNotches = 5,
                BrakeNotches = 7,
                EbNotch = -8,
                HoldingBrakeNotches = 3,
                CpStartPressure = 640,
                CpStopPressure = 780
            }),
            ["3300V"] = Stock(ScissorArm,
            [
                Consist(3, Left, null, Right) // Inverted on purpose.
            ], new VehicleCapabilities
            {
                MasconType = MasconType.OneHandle,
                MasconBrakeType = MasconBrakeType.Notched,
                PowerNotches = 5,
                BrakeNotches = 7,
                EbNotch = -8,
                HoldingBrakeNotches = 1,
                CpStartPressure = 640,
                CpStopPressure = 780
            }),
            ["3020"] = Stock(ScissorArm,
            [
                Consist(6, Right, null, Right, null, null, Left)
            ], new VehicleCapabilities
            {
                MasconType = MasconType.TwoHandle,
                MasconBrakeType = MasconBrakeType.Continuous,
                PowerNotches = 5,
                BrakeNotches = 8,
                EbNotch = -9,
                HoldingBrakeNotches = 0,
                CpStartPressure = 640,
                CpStopPressure = 780
            }),
            ["3000"] = Stock(ScissorArm,
            [
                Consist(6, Right, null, null, Left, null, Left)
            ], new VehicleCapabilities
            {
                MasconType = MasconType.TwoHandle,
                MasconBrakeType = MasconBrakeType.Continuous,
                PowerNotches = 5,
                BrakeNotches = 8,
                EbNotch = -9,
                HoldingBrakeNotches = 0,
                CpStartPressure = 640,
                CpStopPressure = 780
            }),
            ["5600"] = Stock(SingleArm,
            [
                Consist(2, Both, null)
            ], new VehicleCapabilities
            {
                MasconType = MasconType.OneHandle,
                MasconBrakeType = MasconBrakeType.Notched,
                PowerNotches = 5,
                BrakeNotches = 7,
                EbNotch = -8,
                HoldingBrakeNotches = 1,
                CpStartPressure = 640,
                CpStopPressure = 780
            }),
            ["4600"] = Stock(SingleArm,
            [
                Consist(2, Both, null),
                Consist(4, null, Both, null, null)
            ], new VehicleCapabilities
            {
                MasconType = MasconType.OneHandle,
                MasconBrakeType = MasconBrakeType.Notched,
                PowerNotches = 5,
                BrakeNotches = 7,
                EbNotch = -8,
                HoldingBrakeNotches = 1,
                CpStartPressure = 640,
                CpStopPressure = 780
            }),
            ["50000"] = Stock(SingleArm,
            [
                Consist(6, null, Left, Right, null, Both, null)
            ], new VehicleCapabilities
            {
                MasconType = MasconType.OneHandle,
                MasconBrakeType = MasconBrakeType.Notched,
                PowerNotches = 5,
                BrakeNotches = 7,
                EbNotch = -8,
                HoldingBrakeNotches = 1,
                CpStartPressure = 640,
                CpStopPressure = 780
            })
        };

    private static RollingStockRomData Stock(
        RomPantographType pantographType,
        KeyValuePair<int, IReadOnlyList<PantographDirection?>>[] consists,
        VehicleCapabilities capabilities
    )

    {
        var byLength = new Dictionary<int, IReadOnlyList<PantographDirection?>>(consists.Length);
        foreach (var consist in consists) byLength[consist.Key] = consist.Value;
        return new RollingStockRomData
        {
            PantographType = pantographType,
            PantographDirection = byLength,
            Capabilities = capabilities
        };
    }

    private static KeyValuePair<int, IReadOnlyList<PantographDirection?>> Consist(
        int length, params PantographDirection?[] perCar)
    {
        return new KeyValuePair<int, IReadOnlyList<PantographDirection?>>(length, perCar);
    }
}