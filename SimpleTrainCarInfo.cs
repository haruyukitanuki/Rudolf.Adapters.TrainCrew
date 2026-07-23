// Classes and methods for storing basic train data to detect a change in consist.
// Changes are used to detect when a simulator profile cache is stale.
// This allows for refreshing the scenario ID when:
    // Restarting a scenario with a different consist
    // Coupling and decoupling

using System;
using System.Collections.Generic;
using System.Text;
using TrainCrew;

namespace Tanuden.Rudolf.Adapters.TrainCrew
{
    public sealed partial class TrainCrewRudolfAdapter
    {
        private class SimpleTrainCarInfo
        {
            public string CarModel;
            public bool HasConductorCab;
            public bool HasDriverCab;
            public bool HasMotor;
            public bool HasPantograph;

            public SimpleTrainCarInfo(CarState carState)
            {
                CarModel = carState.CarModel;
                HasConductorCab = carState.HasConductorCab;
                HasDriverCab = carState.HasDriverCab;
                HasMotor = carState.HasMotor;
                HasPantograph = carState.HasPantograph;
            }

            /// <summary>
            /// Equality by comparison of each field.
            /// </summary>
            public static bool operator ==(SimpleTrainCarInfo? a, SimpleTrainCarInfo? b)
            {
                if (a is null || b is null)
                {
                    return false;
                }

                return
                    a.CarModel == b.CarModel &&
                    a.HasConductorCab == b.HasConductorCab &&
                    a.HasDriverCab == b.HasDriverCab &&
                    a.HasMotor == b.HasMotor &&
                    a.HasPantograph == b.HasPantograph;
            }

            /// <summary>
            /// Inequality by comparison of each field.
            /// </summary>
            public static bool operator !=(SimpleTrainCarInfo? a, SimpleTrainCarInfo? b)
            {
                return !(a == b);
            }

            /// <summary>
            /// Equality by comparison of each field.
            /// </summary>
            public override bool Equals(object? obj)
            {
                SimpleTrainCarInfo? info = obj as SimpleTrainCarInfo;
                return this == info;
            }

            public override int GetHashCode()
            {
                return
                    CarModel.GetHashCode() +
                    8 * HasConductorCab.GetHashCode() +
                    4 * HasDriverCab.GetHashCode() +
                    2 * HasMotor.GetHashCode() +
                    1 * HasPantograph.GetHashCode();
            }
        }

        private List<SimpleTrainCarInfo> GetSimpleTrainInfo(List<CarState> carStates)
        {
            List<SimpleTrainCarInfo> carInfos = new List<SimpleTrainCarInfo>();

            if (carStates == null)
            {
                // empty list
                return carInfos;
            }

            foreach (CarState carState in carStates)
            {
                carInfos.Add(new SimpleTrainCarInfo(carState));
            }
            return carInfos;
        }

        private bool TrainInfosEqual(List<SimpleTrainCarInfo> a, List<SimpleTrainCarInfo> b)
        {
            if (a == null || b == null || a.Count != b.Count)
            {
                return false;
            }
            else
            {
                for (int i = 0; i < a.Count; i++)
                {
                    if (a[i] != b[i])
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        private static int? GetCarCount(TrainState trainState)
        {
            int? numCars = null;
            if (trainState.CarStates != null)
            {
                numCars = trainState.CarStates.Count;
            }
            return numCars;
        }
    }
}
