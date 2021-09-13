using GTA.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InverseTorque.Extension
{
    public static class Vector3Extensions
    {
        public static float DistanceOnAxis(Vector3 A, Vector3 B, Vector3 axis)
        {
            Vector3 axisNorm = axis.Normalized;

            float ADistanceAlongAxis = Vector3.Dot(axisNorm, A);
            float BDistanceAlongAxis = Vector3.Dot(axisNorm, B);

            return BDistanceAlongAxis - ADistanceAlongAxis;
        }
    }
}
