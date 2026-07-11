using System;
using Tanuden.Rudolf.Adapters.TrainCrew.Internal;
using Tanuden.Rudolf.Enums;
using Tanuden.Rudolf.Input;
using TrainCrew;

namespace Tanuden.Rudolf.Adapters.TrainCrew;

public sealed partial class TrainCrewRudolfAdapter
{
    /// <inheritdoc cref="IRudolfAdapter.Dispatch" />
    public void Dispatch(Command command)
    {
        DispatchInputCommand(command);
    }

    /// <summary>Forward a Rudolf input command to TRAIN CREW via the in-process DLL.</summary>
    private static void DispatchInputCommand(Command command)
    {
        switch (command)
        {
            case SetNotchCommand c:
                if (c.Value <= SetNotchCommand.EB)
                    TrainCrewInput.SetButton(InputAction.NotchEB, true); // EB snap, train-agnostic.
                else if (c.Relative)
                    StepNotch(c.Value);
                else
                    TrainCrewInput.SetNotch(c.Value);
                break;
            case SetPowerNotchCommand c:
                TrainCrewInput.SetPowerNotch(c.Value);
                break;
            case SetBrakeNotchCommand c:
                TrainCrewInput.SetBrakeNotch(c.Value);
                break;
            case SetBrakeSAPCommand c:
                TrainCrewInput.SetBrakeSAP((float)c.KPa);
                break;
            case SetReverserCommand c:
                TrainCrewInput.SetReverser((int)c.Value);
                break;
            case SetButtonCommand c:
                TrainCrewInput.SetButton(FieldMapper.MapAction(c.Action), c.State);
                break;
            case SetWiperCommand c:
                TrainCrewInput.SetWiper(FieldMapper.MapWiper(c.State));
                break;
            case SetAtoNotchCommand c:
                TrainCrewInput.SetATO_Notch(c.Value);
                break;
            case SetDeadmanCommand c:
                TrainCrewInput.SetDeadman(DeadmanChannel(c.Method), c.Holding);
                break;
            default:
                throw new NotSupportedException($"Command {command.GetType().Name} is not supported.");
        }
    }
    
    /// <summary>For setting the notch relatively</summary>
    private static void StepNotch(int steps)
    {
        var action = steps > 0 ? InputAction.NotchUp : InputAction.NotchDw;
        for (var i = 0; i < Math.Abs(steps); i++)
        {
            TrainCrewInput.SetButton(action, true);
            TrainCrewInput.SetButton(action, false);
        }
    }

    private static int DeadmanChannel(EBDeadmanMethod method)
    {
        return method switch
        {
            EBDeadmanMethod.Hand => 0,
            EBDeadmanMethod.Foot => 1,
            _ => throw new NotSupportedException("The TRAIN CREW SDK has no EB deadman channel (Hand/Foot only).")
        };
    }
}