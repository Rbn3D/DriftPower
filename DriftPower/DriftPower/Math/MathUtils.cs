using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace DriftPower.Math
{
    public static class MathUtils
    {
        public static float Map(float x, float in_min, float in_max, float out_min, float out_max, bool clamp = false)
        {
            float r = (x - in_min) * (out_max - out_min) / (in_max - in_min) + out_min;
            if (clamp) r = Clamp(r, out_min, out_max);
            return r;
        }

        public static float Clamp(float val, float min, float max)
        {
            if (val.CompareTo(min) < 0) return min;
            else if (val.CompareTo(max) > 0) return max;
            else return val;
        }

        public static float Clamp01(float val)
        {
            return Clamp(val, 0f, 1f);
        }

        public static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        public static float Powf(float value, float exp)
        {
            return (float)System.Math.Pow(value, exp);
        }

        public static float Deg2Rads(float value)
{
            return value * 3.141592653589793238463f / 180f;
        }

        public static float Rad2Degs(float value)
        {
            return value * (180f / 3.141592653589793238463f);
        }

        public static float Absf(float value)
        {
            return (float)System.Math.Abs(value);
        }

        public static float Floorf(float value)
        {
            return (float)System.Math.Floor(value);
        }
    }
}
