using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InverseTorque
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
    }
}
