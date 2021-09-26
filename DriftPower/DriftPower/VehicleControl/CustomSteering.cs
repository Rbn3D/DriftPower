using GTA;
using GTA.Math;
using DriftPower.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;
using System.Runtime;
using GTA.Native;

namespace DriftPower.VehicleControl
{
    public class CustomSteering
    {
        private const float PI = 3.14159265359f;

        private float mSteerPrev = 0f;
        private Vehicle vehicle;

        public CustomSteering(Vehicle vehicle)
        {
            this.vehicle = vehicle;
        }

        public float CalculateCustomSteeringRatio(float steerInputNormalized, float maxSteeringAngle, float maxDeltaSteer, float reductionMultiplier, float maxCounterSteerAngle)
        {
            float reduction = CalculateReduction(reductionMultiplier);

            float desiredHeading = MathUtils.Rad2Degs(CalculateDesiredHeadingRadians(this.vehicle, MathUtils.Deg2Rads(maxSteeringAngle), steerInputNormalized, reduction, maxCounterSteerAngle));

            // clamp steering speed to 1 rot/s 
            // typically side-to-side in 0.2 seconds with 36 degrees steering angle
            float deltaT = GTA.Game.LastFrameTime;
            //float maxDeltaSteer = 3.14f * 2.0f; // 2 turns/s
            float newSteer = desiredHeading;
            if ( (System.Math.Abs(mSteerPrev - desiredHeading) / deltaT)  >  maxDeltaSteer)
            {
                newSteer = mSteerPrev - System.Math.Sign(mSteerPrev - desiredHeading) * maxDeltaSteer * deltaT;
            }

            mSteerPrev = newSteer;

            return newSteer;
        }

        private float CalculateReduction(float reductionMultiplier)
        {
            float mult = 1;
            Vector3 vel = vehicle.RotationVelocity;
            Vector3 pos = vehicle.Position;
            Vector3 motion = Function.Call<Vector3>(Hash.GET_OFFSET_FROM_ENTITY_GIVEN_WORLD_COORDS, vehicle, pos.X + vel.X, pos.Y + vel.Y, pos.Z + vel.Z);

            if (motion.Y > 3)
            {
                mult = 0.15f + MathUtils.Powf(0.9f, MathUtils.Absf(motion.Y) - 7.2f);
                if (mult != 0) { mult = MathUtils.Floorf(mult * 1000f) / 1000f; }
                if (mult > 1) { mult = 1; }
}
            mult = (1 + (mult - 1) * reductionMultiplier);
            return mult;
        }

        private float CalculateDesiredHeadingRadians(Vehicle veh, float maxSteeringAngleRnd, float steerInputNormalized, float reduction, float maxCounterSteerAngle)
        {
            // Scale input with both reduction and steering limit
            steerInputNormalized = steerInputNormalized * reduction * maxSteeringAngleRnd;
            float correction = steerInputNormalized;

            Vector3 speedVector = veh.RotationVelocity;
            if (System.Math.Abs(speedVector.Y) > 3.0f)
            {
                Vector3 target = speedVector.Normalized;
                float travelDir = (float)(System.Math.Atan2(target.Y, target.X) - PI / 2.0f);
                if (travelDir > PI / 2.0f)
                {
                    travelDir -= PI;
                }
                if (travelDir < -PI / 2.0f)
                {
                    travelDir += PI;
                }
                // Correct for reverse
                travelDir *= System.Math.Sign(speedVector.Y);

                float absMaxCounterSteerRnd = MathUtils.Deg2Rads(maxCounterSteerAngle);
                // clamp auto correction to countersteer limit
                travelDir = MathUtils.Clamp(travelDir, -absMaxCounterSteerRnd, absMaxCounterSteerRnd);

                correction = travelDir + steerInputNormalized;
            }

            return MathUtils.Clamp(correction, -maxSteeringAngleRnd, maxSteeringAngleRnd);
        }
    }
}
