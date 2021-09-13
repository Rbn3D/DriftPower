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
        public const float TARGET_FPS = 105f;

        string ScriptName = "";
        string ScriptVer = "0.1";
        new ScriptSettings Settings;

        float InitialTractionCurveMax;
        float InitialTractionCurveMin;
        float InitialCamberStiffness;
        float InitialSteeringLock;
        Vector3 InitialInertiaMultiplier;
        float InitialTractionBiasFront;

        Vector3 __currInertiaMultiplier;

        float InitialGrip = 0.0f;

        Vehicle lastV = null;
        int lastVTyreF;
        int lastVTyreR;
        int lastVSusp;

        public InverseTorqueSP()
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            Settings = ScriptSettings.Load(@"scripts\InverseTorque\Options.ini");

            CustomTractionCurveMax = Settings.GetValue<float>("SETTINGS", "CustomTractionCurveMax", 2.26f);
            CustomTractionCurveMin = Settings.GetValue<float>("SETTINGS", "CustomTractionCurveMin", 2.02f);
            CustomTractionMaxBias  = Settings.GetValue<float>("SETTINGS", "CustomTractionMaxBias",  0.9f);
            CustomTractionMinBias  = Settings.GetValue<float>("SETTINGS", "CustomTractionMinBias",  0.9f);
            PowerSlideScale     = Settings.GetValue<float>("SETTINGS", "PowerSlideScale",     1.6f);
            TorqueSlideScale    = Settings.GetValue<float>("SETTINGS", "TorqueSlideScale",    1.6f);
            //OversteerIncrement  = Settings.GetValue<float>("SETTINGS", "OversteerIncrement",  0f);
            //UndersteerIncrement = Settings.GetValue<float>("SETTINGS", "UndersteerIncrement", 0f);
            CustomSteeringLock  = Settings.GetValue<float>("SETTINGS", "CustomSteeringLock",  1f);
            CustomCamberStiffness  = Settings.GetValue<float>("SETTINGS", "CustomCamberStiffness", 0f);
            //AdditionalOversteerForce = Settings.GetValue<float>("SETTINGS", "AdditionalOversteerForce", /* 0.1779f */ 0.0f);
            //AdditionalOversteerTorque = Settings.GetValue<float>("SETTINGS", "AdditionalOversteerTorque",  0f);
            AngularRotationIntensityOffsetIdle = Settings.GetValue<float>("SETTINGS", "AngularRotationIntensityOffsetIdle", 0f);
            AngularRotationIntensityOffsetSlide = Settings.GetValue<float>("SETTINGS", "AngularRotationIntensityOffsetSlide", 0f);
            VelocityIntensityOffsetSlide = Settings.GetValue<float>("SETTINGS", "VelocityMultiplierSlide", 0f);

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

        float CustomTractionCurveMax;
        float CustomTractionCurveMin;
        float CustomTractionMaxBias;
        float CustomTractionMinBias;
        float PowerSlideScale;
        float TorqueSlideScale;
        //float OversteerIncrement; 
        //float UndersteerIncrement;
        float CustomSteeringLock;
        float CustomCamberStiffness;
        float AngularRotationIntensityOffsetIdle;
        float AngularRotationIntensityOffsetSlide;
        float VelocityIntensityOffsetSlide;
        //float AdditionalOversteerForce;
        //float AdditionalOversteerTorque;


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
                    CustomTractionCurveMax    = PromptUserFloat("Custom Traction Curve (Max)",  CustomTractionCurveMax);
                    CustomTractionCurveMin    = PromptUserFloat("Custom Traction Curve (Min)",  CustomTractionCurveMin);
                    CustomTractionMaxBias   = PromptUserFloat("Custom Traction Bias (Max)", CustomTractionMaxBias);
                    CustomTractionMinBias   = PromptUserFloat("Custom Traction Bias (Min)", CustomTractionMinBias);
                    PowerSlideScale     = PromptUserFloat("Power Slide Scale",      PowerSlideScale);
                    TorqueSlideScale    = PromptUserFloat("Torque Slide Scale",     TorqueSlideScale);
                    //OversteerIncrement  = PromptUserFloat("Oversteer Increment",    OversteerIncrement);
                    //UndersteerIncrement = PromptUserFloat("Understeer Increment",   UndersteerIncrement);
                    CustomSteeringLock  = PromptUserFloat("Custom Steering Lock",   CustomSteeringLock);
                    CustomCamberStiffness = PromptUserFloat("Custom Camber Stiffness", CustomCamberStiffness);
                    //AdditionalOversteerForce  = PromptUserFloat("Additional Oversteer Force",  AdditionalOversteerForce);
                    //AdditionalOversteerTorque = PromptUserFloat("Additional Oversteer Torque", AdditionalOversteerTorque);
                    AngularRotationIntensityOffsetIdle = PromptUserFloat("Angular Rotation Intensity Offset (Idle)", AngularRotationIntensityOffsetIdle);
                    AngularRotationIntensityOffsetSlide = PromptUserFloat("Angular Rotation Intensity Offset (Slide)", AngularRotationIntensityOffsetSlide);
                    VelocityIntensityOffsetSlide = PromptUserFloat("Velocity Multiplier (Slide)", VelocityIntensityOffsetSlide);
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

                    float absLatF  = Math.Abs(lateralForce);
                    float absLongF  = Math.Abs(longitudinalForce);

                    float auxForwAc = Math.Max(0f, longitudinalForce);
                    float auxForwAc03 = Clamp(longitudinalForce, 0f, 0.4f);
                    float auxForwAc06 = Clamp(longitudinalForce, 0f, 0.7f);
                    float auxBackAc = map(longitudinalForce, 0f, -1f, 0f, 1f, true);
                    float combCustom = Math.Max(auxForwAc, absLatF) * (1f - auxBackAc);

                    float combCustom2  = Math.Max(auxForwAc, absLatF) * map(1f - auxBackAc, 0f, 1f, 0.5f, 1f);
                    float combCustom2L = Math.Max(auxForwAc03, absLatF);
                    float combCustom3L = Math.Max(auxForwAc06, absLatF);
                    float combCustom3Lm = Math.Max(auxForwAc06, absLatF) * (1f - auxBackAc);

                    float aux1 = Math.Max(0f, longitudinalForce);
                    float longForwOrLatF = Math.Max(aux1, absLatF);
                    float longOrLatF = Math.Max(longitudinalForce, absLatF);

                    //float camberFact = Clamp01(map(longitudinalForce, -1.3f, 1.3f, 0.25f, 0.75f, true) + Easing.EaseOutCirc01(map(absLatF, 0f, 1.30f, 0f, 0.25f)));
                    float expFact = Clamp01(map(longitudinalForce, -1.3f, 1.3f, 0.3f, 0.7f, true) + Easing.EaseOutCirc01(map(absLatF, 0f, 1.30f, 0f, 0.70f)));
                    float altFact    = Clamp01(map(longitudinalForce, 0f, 1.30f, 0f, 1f, true) + map(absLatF, 0f, 1.30f, 0f, 1f));
                    float fullFact   = Clamp01(map(absLongF, 0f, 1.30f, 0f, 1f, true) + map(absLatF, 0f, 1.30f, 0f, 1f));
                    float fullFactAlt   = Clamp01(map(absLongF, 0f, 1.30f, 0f, 0.5f, true) + map(absLatF, 0f, 1.30f, 0f, 1f));
                    float latFactAlt = Easing.EaseOutCirc01(map(absLatF, 0f, 1.50f, 0f, 1f, true));
                    float fullFactAlt2   = Clamp01(map(absLongF, 0f, 1.30f, 0f, 0.15f, true) + Easing.EaseOutCirc01(map(absLatF, 0f, 1.50f, 0f, 1f)));
                    

                    float forwardness = Clamp01(Vector3.Dot(v.Velocity.Normalized, v.ForwardVector));

                    //float torqueInc = Easing.EaseOutCirc01(map(absLatF, 0f, 0.50f, 0f, 1f, true));


                    var VHandling = v.HandlingData;

                    if (InverseTorqueDebug)
                    {
                        //GTA.UI.Screen.ShowSubtitle("~n~~w~ X" + Math.Round(gForce.X, 2).ToString() + " " + "~n~~w~ Y" + Math.Round(gForce.Y, 2).ToString(), 500);
                        GTA.UI.Screen.ShowSubtitle("~n~~w~" + Math.Round(InitialTractionBiasFront, 2).ToString());
                    }

                    float exp = 1.791f;
                    float expLowTraction = 1.79315f;

                    float tractionFactor = Powf(fullFactAlt2, Lerp(exp, expLowTraction, expFact));
                    float latTractionFactor = Powf(latFactAlt, Lerp(exp, expLowTraction, expFact));
                    //float tractionMinFactor = Powf(fullFactAlt2, 1.79f);

                    //VHandling.TractionCurveMax = Lerp(InitialTractionCurveMax * CustomTractionCurveMax, InitialTractionCurveMax * CustomTractionMaxBias, tractionFactor);
                    //VHandling.TractionCurveMin = Lerp(InitialTractionCurveMin * CustomTractionCurveMin, InitialTractionCurveMin * CustomTractionMinBias, tractionFactor);

                    VHandling.TractionCurveMax = Lerp(InitialTractionCurveMax, CustomTractionCurveMax, CustomTractionMaxBias);
                    VHandling.TractionCurveMin = Lerp(InitialTractionCurveMin, CustomTractionCurveMin, CustomTractionMinBias);

                    VHandling.CamberStiffness = CustomCamberStiffness;

                    v.EnginePowerMultiplier  = Lerp(1f, PowerSlideScale, tractionFactor);
                    v.EngineTorqueMultiplier = Lerp(1f, TorqueSlideScale, tractionFactor);

                    VHandling.SteeringLock = CustomSteeringLock;
                    VHandling.SetVehicleLowSpeedTractionMult(0f);
                    //VHandling.TractionBiasFront = 0.5045f;
                    VHandling.TractionBiasFront = Lerp(1.0025f, 0.9975f, latTractionFactor); // 0.00 | 2.00 range

                    VHandling.SetVehicleTractionCurveLateral(22.5f);

                    __currInertiaMultiplier.Z = InitialInertiaMultiplier.Z * 1.079f;

                    VHandling.InertiaMultiplier = __currInertiaMultiplier;

                    //float rotStabilizeForceMin = AngularRotationIntensityOffsetIdle;
                    //float rotStabilizeForceMax = AngularRotationIntensityOffsetSlide;

                    //float stabilizeTorque = map(tractionFactor, 0f, 1f, rotStabilizeForceMin * Math.Sign(lateralForce), rotStabilizeForceMax * Math.Sign(lateralForce));

                    //v.ApplyForceRelative(default, new Vector3(0f, 0f, stabilizeTorque * TARGET_FPS * GTA.Game.LastFrameTime), ForceType.MaxForceRot);
                    Vector3 proccesedVeloForce = v.Velocity - (v.ForwardVector * Vector3.Dot(v.ForwardVector, v.Velocity));
                    v.ApplyForce(proccesedVeloForce * VelocityIntensityOffsetSlide * latTractionFactor * TARGET_FPS * GTA.Game.LastFrameTime, default, ForceType.MinForce);
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
            lastVHandling.SteeringLock = InitialSteeringLock;
            lastVHandling.InertiaMultiplier = InitialInertiaMultiplier;
            lastVHandling.TractionBiasFront = InitialTractionBiasFront;
        }

        private void FetchVehicleInitialData(Vehicle veh)
        {
            var VHandling = veh.HandlingData;

            InitialTractionCurveMax = VHandling.TractionCurveMax;
            InitialTractionCurveMin = VHandling.TractionCurveMin;
            InitialCamberStiffness = VHandling.CamberStiffness;
            InitialSteeringLock = VHandling.SteeringLock;
            InitialInertiaMultiplier = VHandling.InertiaMultiplier;
            InitialTractionBiasFront = VHandling.TractionBiasFront;
            __currInertiaMultiplier = InitialInertiaMultiplier;

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