using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GTA;
using GTA.Math;
using GTA.Native;

namespace InverseTorque.Extension
{
    public static class VehicleExtensions
    {
        public static float GetVehicleRealThrotle(this Vehicle vehicle)
        {
            var address = vehicle.MemoryAddress;

            if (address == IntPtr.Zero || NativeMemoryEx.RealThrottlePowerOffset == 0)
            {
                return 0.0f;
            }

            return NativeMemoryEx.ReadFloat(vehicle.MemoryAddress + NativeMemoryEx.RealThrottlePowerOffset);
        }

        public static void SetVehicleRealThrotle(this Vehicle vehicle, float value)
        {
            var address = vehicle.MemoryAddress;

            if (address == IntPtr.Zero || NativeMemoryEx.RealThrottlePowerOffset == 0)
            {
                return;
            }

            NativeMemoryEx.WriteFloat(vehicle.MemoryAddress + NativeMemoryEx.RealThrottlePowerOffset, value);
        }

        private static Vector3 previousVelocity = new Vector3();

        private static List<Vector3> previousAccelerationVectors = new List<Vector3>();

        private static int movingAverageWindow = 1;

        public static Vector3 MeasureGForce(Vehicle vehicle)
        {
            ValueTuple<Vector3, Vector3> accelerationVector = GetAccelerationVector(vehicle, previousVelocity);
            Vector3 item = accelerationVector.Item1;
            Vector3 item2 = accelerationVector.Item2;
            previousAccelerationVectors.Add(item);
            while (previousAccelerationVectors.Count > movingAverageWindow)
            {
                previousAccelerationVectors.RemoveAt(0);
            }
            Vector3 accelerationVector2 = Average(previousAccelerationVectors);
            previousVelocity = item2;

            Vector3 accelInG = Vector3.Divide(accelerationVector2, 9.8f);
            return accelInG;
        }

        public static void ClearGForceBuffer()
        {
            previousVelocity = new Vector3();
        }

        private static ValueTuple<Vector3, Vector3> GetAccelerationVector(Vehicle vehicle, Vector3 previousVelocity)
        {
            Vector3 left = vehicle.Velocity - previousVelocity;
            Vector3 vector = new Vector3
            {
                X = -Vector3.Dot(left, vehicle.RightVector),
                Y = Vector3.Dot(left, vehicle.ForwardVector),
                Z = Vector3.Dot(left, vehicle.UpVector)
            };
            return new ValueTuple<Vector3, Vector3>(vector * (1f / Game.LastFrameTime), vehicle.Velocity);
        }

        public static Vector3 Average(List<Vector3> values)
        {
            Vector3 value = default(Vector3);
            foreach (Vector3 vector in values)
            {
                value.X += vector.X;
                value.Y += vector.Y;
                value.Z += vector.Z;
            }
            return Vector3.Divide(value, (float)values.Count);
        }
    }
}
