using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;
using System;
using System.Globalization;
using System.IO;
using System.Threading;

namespace InverseTorque
{

    public class InverseTorqueSP : Script
    {
        string ScriptName = "";
        string ScriptVer = "0.1";
        new ScriptSettings Settings;

        float InitialTractionCurveMax;
        float InitialTractionCurveMin;
        float InitialCamberStiffness;
        float InitialTractionBiasFront;

        float InitialRearTraction = 0.0f;
        float InitialGrip = 0.0f;

        BezierCurve curveMinTraction;
        BezierCurve curveMaxTraction;
        BezierCurve curveTorqueMult;
        BezierCurve curvePowerMult;

        float prevFact = 0f;
        float prevFactVelo = 0f;
        float prevFactAngle = 0f;
        float longLastingFact = 0f;
        float longLastingFactVelo = 0f;
        float longLastingFactAngle = 0f;

        Vehicle lastV = null;
        int lastVTyreF;
        int lastVTyreR;
        int lastVSusp;

        public InverseTorqueSP()
        {
#if DEBUG
            InverseTorqueDebug = true;
            DebugText = new GTA.UI.TextElement(" ", new System.Drawing.PointF(10f, 10f), 0.50f, System.Drawing.Color.White, Font.ChaletComprimeCologne, Alignment.Left, false, true);
#endif

            Tick += OnTick;
            Aborted += OnAborted;

            Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            Settings = ScriptSettings.Load(@"scripts\InverseTorque\Options.ini");
            Scaler = Settings.GetValue<float>("SETTINGS", "Scaler", 2f);

            curveMinTraction = new BezierCurve(new Point(0f, 0f), new Point(0.45f, 0.10f), new Point(0.7f, 0.7f), new Point(1f, 1f));
            curveMaxTraction = new BezierCurve(new Point(0f, 0f), new Point(0.45f, 0.12f), new Point(0.66f, 0.8f), new Point(1f, 1f));
            curveTorqueMult = new BezierCurve(new Point(0f, 0f), new Point(0.45f, 0.12f), new Point(0.75f, 0.875f), new Point(1f, 1f));
            curvePowerMult = new BezierCurve(new Point(0f, 0f), new Point(0.12f, 0.05f), new Point(0.75f, 0.875f), new Point(1f, 1f));
        }

        private void OnAborted(object sender, EventArgs e)
        {
            try
            {
                if (v != null)
                {
                    RestoreVehicleFromInitialData(v);
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Screen.ShowSubtitle("~y~Error in InversTorqueSP: " + ex.ToString());
            }
        }

        float Scaler = 1f;
        bool InverseTorqueDebug = false;
        private Vehicle v;

        private TextElement DebugText;

        void OnTick(object sender, EventArgs e)
        {
            if (WasCheatStringJustEntered("itdebug"))
            {
                if (!Settings.GetValue<bool>("SETTINGS", "Enabled", true)) GTA.UI.Screen.ShowSubtitle("~y~Inverse Torque is disabled in Options.ini.");
                else InverseTorqueDebug = !InverseTorqueDebug;

                if(InverseTorqueDebug && DebugText == null)
                    DebugText = new GTA.UI.TextElement(" ", new System.Drawing.PointF(10f, 10f), 0.50f, System.Drawing.Color.White, Font.ChaletComprimeCologne, Alignment.Left, false, true);
            }
            if (WasCheatStringJustEntered("itscale"))
            {
                if (!Settings.GetValue<bool>("SETTINGS", "Enabled", true)) GTA.UI.Screen.ShowSubtitle("~y~Inverse Torque is disabled in Options.ini.");
                else
                {
                    GTA.UI.Screen.ShowSubtitle("~y~Inverse Torque~w~~n~Current Scaler: ~b~x" + Scaler);
                    string m = Game.GetUserInput(5.ToString());
                    if (float.TryParse(m, out Scaler))
                    {
                        GTA.UI.Screen.ShowSubtitle("~y~Inverse Torque~w~~n~Scaler set: ~b~x" + Scaler + " ~w~ at 90º");
                    }
                    else GTA.UI.Screen.ShowSubtitle("~y~Inverse Torque~w~~n~Invalid value: ~o~" + m);
                }

            }

            if (!Settings.GetValue<bool>("SETTINGS", "Enabled", true)) return;

            v = Game.Player?.Character?.CurrentVehicle;

            if (CanWeUse(v) && v.Driver == Game.Player.Character && v.Model.IsCar)
            {
                if (lastV == null || v.Handle != lastV.Handle || lastVTyreF != GetModIndex(v, VehicleModType.FrontWheel) || lastVTyreR != GetModIndex(v, VehicleModType.RearWheel) || lastVSusp != GetModIndex(v, VehicleModType.Suspension))
                {
                    // restore previous data if neccesary (after exit vehicle)
                    if (lastV != null)
                    {
                        RestoreVehicleFromInitialData(lastV);

                        if (InverseTorqueDebug)
                        {
                            DebugText.Caption = " ";
                        }
                    }

                    // Fetch relevant initial handling data
                    FetchVehicleInitialData(v);
                }

                if (v.CurrentGear > 0)
                {

                    float angle = Vector3.Angle(v.ForwardVector, v.Velocity.Normalized);
                    //float multGrip = 0f;
                    //float tracFact = 0f;
                    //if (angle < 90)
                    //{
                    //multGrip = map(angle, 4f, 90, 1f, 3.0f, true);
                    //tracFact = map(angle, 0f, 90, 0f, 1.0f, true);
                    //}
                    //else
                    //{
                    //    multGrip = Scaler * InitialGrip; //(float)Math.Round(map(angle, 180f, 90f, 1f, Scaler * grip, true), 2);
                    //}

                    Vector3 vehAngularVelocity = GTA.Native.Function.Call<Vector3>(Hash.GET_ENTITY_ROTATION_VELOCITY, v);

                    float veolcityFactor = Easing.EaseOutCubic01(map(vehAngularVelocity.Length(), 0f, 5f, 0f, 1f, true));
                    float angleFactor = Easing.EaseOutCubic01(map(angle, 2.5f, 90f, 0f, 1f, true));

                    veolcityFactor *= map(v.Velocity.Length(), 0, 4, 0, 1, true);
                    angleFactor *= map(v.Velocity.Length(), 0, 4, 0, 1, true);

                    float finalFactor = Math.Max(veolcityFactor, angleFactor);

                    float auxVelo = map((finalFactor - prevFactVelo), 0f, 0.1f, 1f, 100f, true);
                    float auxAngle = map((finalFactor - prevFactAngle), 0f, 0.1f, 1f, 100f, true);

                    longLastingFactVelo = Lerp(longLastingFactVelo, veolcityFactor, auxVelo * Function.Call<float>(Hash.GET_FRAME_TIME));
                    longLastingFactAngle = Lerp(longLastingFactAngle, veolcityFactor, auxAngle * Function.Call<float>(Hash.GET_FRAME_TIME));

                    longLastingFact = Math.Max(longLastingFactVelo, longLastingFactAngle);

                    float SqLongLastingFact = (float)Math.Pow(longLastingFact, 2.0f);

                    var VHandling = v.HandlingData;

                    float InputTrhottle = GTA.Game.GetControlValueNormalized(Control.VehicleAccelerate);
                    float realThrottle = v.ThrottlePower;

                    if (true) // TODO Add config flag
                    {

                    }


                    //VHandling.TractionCurveMax = InitialTractionCurveMax * 1.005f;
                    VHandling.TractionCurveMax = Lerp(InitialTractionCurveMax * 0.996f, InitialTractionCurveMax * 0.9865f, SqLongLastingFact);
                    VHandling.TractionCurveMin = Lerp(InitialTractionCurveMin * 0.996f, InitialTractionCurveMin * 0.9865f, SqLongLastingFact);
                    VHandling.CamberStiffness = Lerp(InitialCamberStiffness + (0.0155f * InitialRearTraction), InitialCamberStiffness - (0.0325f * InitialRearTraction), SqLongLastingFact * Math.Min(0.30f, Math.Max((float)Math.Pow(v.ThrottlePower, 2f), v.BrakePower)));

                    //float easedThrotle = Easing.EaseOutCubic01(v.ThrottlePower);

                    //v.EngineTorqueMultiplier = 1f + ((float)curveTorqueMult.GetY(longLastingFact));
                    //v.EnginePowerMultiplier = 1f + ((float)curvePowerMult.GetY(longLastingFact));
                    v.EngineTorqueMultiplier = 1f + (SqLongLastingFact * 0.75f);
                    v.EnginePowerMultiplier = 1f + (SqLongLastingFact * 0.75f);

                    if (InverseTorqueDebug)
                    {
                        GTA.UI.Screen.ShowSubtitle("~n~~w~x" + Math.Round(finalFactor, 2).ToString() + " " + "~n~~w~x" + Math.Round(longLastingFact, 2).ToString(), 500);

                        DebugText.Caption = $"InputTrhottle: {InputTrhottle}\nRealThrottle: {realThrottle}";

                        DebugText.Draw();
                    }

                    prevFact = finalFactor;
                    prevFactVelo = veolcityFactor;
                    prevFactAngle = angleFactor;
                }
                else
                {
                    var VHandling = v.HandlingData;

                    VHandling.TractionCurveMax = InitialTractionCurveMax;
                    VHandling.TractionCurveMin = InitialTractionCurveMin;
                    VHandling.CamberStiffness = InitialCamberStiffness;

                    v.EngineTorqueMultiplier = 1f;
                    v.EnginePowerMultiplier = 1f;
                }

                lastVTyreF = GetModIndex(v, VehicleModType.FrontWheel);
                lastVTyreR = GetModIndex(v, VehicleModType.RearWheel);
                lastVSusp = GetModIndex(v, VehicleModType.Suspension);
            }
            else
            {
                prevFact = 0f;
                prevFactVelo = 0f;
                prevFactAngle = 0f;
                longLastingFact = 0f;
                longLastingFactVelo = 0f;
                longLastingFactAngle = 0f;
            }

            lastV = Game.Player.Character.CurrentVehicle;
        }

        private int GetModIndex(Vehicle veh, VehicleModType modType)
        {
            return veh.Mods.Contains(modType) ? veh.Mods[modType].Index : -1;
        }


        private void RestoreVehicleFromInitialData(Vehicle veh)
        {
            var lastVHandling = veh.HandlingData;

            lastVHandling.TractionCurveMax = InitialTractionCurveMax;
            lastVHandling.TractionCurveMin = InitialTractionCurveMin;
            lastVHandling.CamberStiffness = InitialCamberStiffness;
            lastVHandling.TractionBiasFront = InitialTractionBiasFront;
        }

        private void FetchVehicleInitialData(Vehicle veh)
        {
            var VHandling = veh.HandlingData;

            InitialTractionCurveMax = VHandling.TractionCurveMax;
            InitialTractionCurveMin = VHandling.TractionCurveMin;
            InitialCamberStiffness = VHandling.CamberStiffness;
            InitialTractionBiasFront = VHandling.TractionBiasFront;

            InitialRearTraction = map(InitialTractionBiasFront, 0.01f, 0.99f, 1.0f, 0.0f, true);
            InitialGrip = Function.Call<float>((Hash)0xA132FB5370554DB0, veh);
        }

        public static float map(float x, float in_min, float in_max, float out_min, float out_max, bool clamp = false)
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

        public static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }



        /// TOOLS ///
        void LoadSettings()
        {
            if (File.Exists(@"scripts\\SCRIPTNAME.ini"))
            {

                ScriptSettings config = ScriptSettings.Load(@"scripts\SCRIPTNAME.ini");
                // = config.GetValue<bool>("GENERAL_SETTINGS", "NAME", true);
            }
            else
            {
                WarnPlayer(ScriptName + " " + ScriptVer, "SCRIPT RESET", "~g~Towing Service has been cleaned and reset succesfully.");
            }
        }

        void WarnPlayer(string script_name, string title, string message)
        {
            Function.Call(Hash.BEGIN_TEXT_COMMAND_THEFEED_POST, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, message);
            Function.Call(Hash.END_TEXT_COMMAND_THEFEED_POST_MESSAGETEXT, "CHAR_SOCIAL_CLUB", "CHAR_SOCIAL_CLUB", true, 0, title, "~b~" + script_name);
        }

        public static bool CanWeUse(Entity entity)
        {
            return entity != null && entity.Exists();
        }

        void DisplayHelpTextThisFrame(string text)
        {
            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_HELP, "STRING");
            Function.Call(Hash.BEGIN_TEXT_COMMAND_SCALEFORM_STRING, text);
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_HELP, 0, false, true, -1);
        }


        public static bool WasCheatStringJustEntered(string cheat)
        {
            return Function.Call<bool>(Hash._HAS_CHEAT_STRING_JUST_BEEN_ENTERED, Game.GenerateHash(cheat));
        }

    }
}