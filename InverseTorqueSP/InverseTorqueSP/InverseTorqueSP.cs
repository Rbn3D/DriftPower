using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using InverseTorque.Extension;

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

        //float InitialRearTraction = 0.0f;
        float InitialGrip = 0.0f;

        //BezierCurve curveMinTraction;
        //BezierCurve curveMaxTraction;
        //BezierCurve curveTorqueMult;
        //BezierCurve curvePowerMult;

        //float prevFact = 0f;
        //float prevFactVelo = 0f;
        //float prevFactAngle = 0f;
        //float longLastingFact = 0f;
        //float longLastingFactVelo = 0f;
        //float longLastingFactAngle = 0f;

        Vehicle lastV = null;
        int lastVTyreF;
        int lastVTyreR;
        int lastVSusp;

        public InverseTorqueSP()
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            Settings = ScriptSettings.Load(@"scripts\InverseTorque\Options.ini");

            GripIdleScale       = Settings.GetValue<float>("SETTINGS", "GripIdleScale",       1.00f);
            GripSlideScaleMax   = Settings.GetValue<float>("SETTINGS", "GripSlideScaleMax",   0.985f);
            GripSlideScaleMin   = Settings.GetValue<float>("SETTINGS", "GripSlideScaleMin",   0.955f);
            PowerSlideScale     = Settings.GetValue<float>("SETTINGS", "PowerSlideScale",     1.60f);
            TorqueSlideScale    = Settings.GetValue<float>("SETTINGS", "TorqueSlideScale",    1.60f);
            OversteerIncrement  = Settings.GetValue<float>("SETTINGS", "OversteerIncrement",  0.20f);
            UndersteerIncrement = Settings.GetValue<float>("SETTINGS", "UndersteerIncrement", 0.10f);

            //curveMinTraction = new BezierCurve(new Point(0f, 0f), new Point(0.45f, 0.10f), new Point(0.7f, 0.7f), new Point(1f, 1f));
            //curveMaxTraction = new BezierCurve(new Point(0f, 0f), new Point(0.45f, 0.12f), new Point(0.66f, 0.8f), new Point(1f, 1f));
            //curveTorqueMult = new BezierCurve(new Point(0f, 0f), new Point(0.45f, 0.12f), new Point(0.75f, 0.875f), new Point(1f, 1f));
            //curvePowerMult = new BezierCurve(new Point(0f, 0f), new Point(0.12f, 0.05f), new Point(0.75f, 0.875f), new Point(1f, 1f));

            Tick += OnTick;
            Aborted += OnAborted;
        }

        private void OnAborted(object sender, EventArgs e)
        {
            TryClearActiveVehicleCustomizations();
        }

        private void TryClearActiveVehicleCustomizations()
        {
            try
            {
                if (v != null)
                {
                    RestoreVehicleFromInitialData(v);

                    lastV = null;
                    v = null;
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Screen.ShowSubtitle("~y~Error in InverseTorqueSP: " + ex.ToString());
            }
        }

        float GripIdleScale;
        float GripSlideScaleMax;
        float GripSlideScaleMin;
        float PowerSlideScale;
        float TorqueSlideScale;
        float OversteerIncrement; 
        float UndersteerIncrement;

        bool InverseTorqueDebug = false;
        private Vehicle v;

        void OnTick(object sender, EventArgs e)
        {
            if (WasCheatStringJustEntered("itenable"))
            {
                Settings.SetValue("SETTINGS", "Enabled", true);
            }

            if (WasCheatStringJustEntered("itdisable"))
            {
                Settings.SetValue("SETTINGS", "Enabled", false);

                TryClearActiveVehicleCustomizations();
            }

            if (WasCheatStringJustEntered("itdebug"))
            {
                if (!Settings.GetValue<bool>("SETTINGS", "Enabled", true)) GTA.UI.Screen.ShowSubtitle("~y~Inverse Torque is disabled in Options.ini.");
                else InverseTorqueDebug = !InverseTorqueDebug;
            }
            if (WasCheatStringJustEntered("itscale"))
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

                if (!Settings.GetValue<bool>("SETTINGS", "Enabled", true)) GTA.UI.Screen.ShowSubtitle("~y~Inverse Torque is disabled in Options.ini.");
                else
                {
                    GripIdleScale       = PromptUserFloat("Grip Idle Scale",        GripIdleScale);
                    GripSlideScaleMax   = PromptUserFloat("Grip Slide Scale (Max)", GripSlideScaleMax);
                    GripSlideScaleMin   = PromptUserFloat("Grip Slide Scale (Min)", GripSlideScaleMin);
                    PowerSlideScale     = PromptUserFloat("Power Slide Scale",      PowerSlideScale);
                    TorqueSlideScale    = PromptUserFloat("Torque Slide Scale",     TorqueSlideScale);
                    OversteerIncrement  = PromptUserFloat("Oversteer Increment",    OversteerIncrement);
                    UndersteerIncrement = PromptUserFloat("Understeer Increment",   UndersteerIncrement);
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
                        VehicleExtensions.ClearGForceBuffer();
                    }

                    // Fetch relevant initial handling data
                    FetchVehicleInitialData(v);
                }

                Vector3 gForce = VehicleExtensions.MeasureGForce(v);

                if (v.CurrentGear > 0)
                {

                    float longitudinalForce = gForce.Y;
                    float lateralForce = gForce.X;

                    //float absLongF = Math.Abs(longitudinalForce);
                    float absLatF  = Math.Abs(lateralForce);
                    //float absLatFsSq  = (float)Math.Pow(absLatF, 2f);

                    //float combPositiveF = Math.Max(Clamp(longitudinalForce, 0f, 1f), Clamp(absLatF, 0f, 1f));
                    //float combPositiveF2 = Math.Max(Clamp(longitudinalForce * 0.5f, 0f, 0.5f), Clamp(absLatF, 0f, 1f));


                    float auxForwAc = Math.Max(0f, longitudinalForce);
                    float auxForwAc03 = Clamp(longitudinalForce, 0f, 0.4f);
                    float auxForwAc06 = Clamp(longitudinalForce, 0f, 0.7f);
                    float auxBackAc = map(longitudinalForce, 0f, -1f, 0f, 1f, true);
                    float combCustom = Math.Max(auxForwAc, absLatF) * (1f - auxBackAc);

                    float combCustom2  = Math.Max(auxForwAc, absLatF) * map(1f - auxBackAc, 0f, 1f, 0.5f, 1f);
                    float combCustom2L = Math.Max(auxForwAc03, absLatF);
                    float combCustom3L = Math.Max(auxForwAc06, absLatF);
                    float combCustom3Lm = Math.Max(auxForwAc06, absLatF) * (1f - auxBackAc);

                    var VHandling = v.HandlingData;

                    if (InverseTorqueDebug)
                    {
                        GTA.UI.Screen.ShowSubtitle("~n~~w~ X" + Math.Round(gForce.X, 2).ToString() + " " + "~n~~w~ Y" + Math.Round(gForce.Y, 2).ToString(), 500);
                    }

                    VHandling.TractionCurveMax = Lerp(InitialTractionCurveMax * GripIdleScale, InitialTractionCurveMax * GripSlideScaleMax, Powf(combCustom2, 1.5f));
                    VHandling.TractionCurveMin = Lerp(InitialTractionCurveMin * GripIdleScale, InitialTractionCurveMin * GripSlideScaleMin, Powf(combCustom, 1.5f));

                    float auxSlideCamberStiffness = Lerp(InitialCamberStiffness, -0.10f, OversteerIncrement);
                    float auxIdleCamberStiffness  = Lerp(InitialCamberStiffness,  0.10f, UndersteerIncrement);

                    VHandling.CamberStiffness = Lerp(auxIdleCamberStiffness, auxSlideCamberStiffness, combCustom3Lm);

                    v.EnginePowerMultiplier  = Lerp(1f, PowerSlideScale, Powf(combCustom2L, 1.7f));
                    v.EngineTorqueMultiplier = Lerp(1f, TorqueSlideScale, Powf(combCustom3L, 1.7f));
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
                VehicleExtensions.ClearGForceBuffer();
            }

            lastV = Game.Player.Character.CurrentVehicle;
        }

        private float PromptUserFloat(string valueName, float value)
        {
            GTA.UI.Screen.ShowSubtitle($"~y~Inverse Torque~w~~n~{valueName}: ~b~x" + value);
            string m = Game.GetUserInput(value.ToString()).Replace(",", ".");
            if (float.TryParse(m, out value))
            {
                GTA.UI.Screen.ShowSubtitle($"~y~Inverse Torque~w~~n~{valueName} set: ~b~x" + value + " ~w~");
            }
            else GTA.UI.Screen.ShowSubtitle("~y~Inverse Torque~w~~n~Invalid value: ~o~" + m);

            return value;
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

            //InitialRearTraction = map(InitialTractionBiasFront, 0.01f, 0.99f, 1.0f, 0.5f, true);
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

        public static float Powf(float value, float exp)
        {
            return (float)Math.Pow(value, exp);
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