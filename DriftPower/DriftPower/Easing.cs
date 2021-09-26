using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DriftPower
{
    public static class Easing
    {
        public static float EaseOutCubic01(float t)
        {
            return (--t) * t * t + 1;
        }

        public static float EaseInCubic01(float t)
        {
            return t * t * t;
        }

        public static float EaseInSine01(float t)
        {
            return -1f * (float)System.Math.Cos(t / 1f * (System.Math.PI * 0.5f)) + 1f;
        }

        public static float EaseOutSine01(float t)
        {
            return (float)System.Math.Sin(t / 1 * (System.Math.PI * 0.5f));
        }

        public static float EaseOutCirc01(float t)
        {
            return (float)System.Math.Sqrt(1f - (t = t - 1f) * t);
        }
    }
}
