using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using DriftPower.Extension;
using DriftPower.Math;
using DriftPower.VehicleControl;

namespace DriftPower
{
    public class DriftPower : Script
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

        CustomSteering customSteering;

        public DriftPower()
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            Settings = ScriptSettings.Load(@"scripts\DriftPower\Settings.ini");

            CustomTractionCurveMax = Settings.GetValue<float>("SETTINGS", "CustomTractionCurveMax", 2.26f);
            CustomTractionCurveMin = Settings.GetValue<float>("SETTINGS", "CustomTractionCurveMin", 2.02f);
            CustomTractionMaxBias  = Settings.GetValue<float>("SETTINGS", "CustomTractionMaxBias",  1f);
            CustomTractionMinBias  = Settings.GetValue<float>("SETTINGS", "CustomTractionMinBias",  1f);
            PowerSlideScale     = Settings.GetValue<float>("SETTINGS", "PowerSlideScale",     0.80f);
            TorqueSlideScale    = Settings.GetValue<float>("SETTINGS", "TorqueSlideScale",    2.5f);
            CustomSteeringLock  = Settings.GetValue<float>("SETTINGS", "CustomSteeringLock",  1f);
            CustomCamberStiffness  = Settings.GetValue<float>("SETTINGS", "CustomCamberStiffness", 0f);
            RotationZSpeedMultiplier = Settings.GetValue<float>("SETTINGS", "AngularRotationMultiplier", 0.985f);
            SteeringSpeedMultiplier = Settings.GetValue<float>("SETTINGS", "SteeringSpeedMultiplier", 1f);
            MaxSteeringAngle = Settings.GetValue<float>("SETTINGS", "MaxSteeringAngle", 35f);
            SteeringReductionMultiplier = Settings.GetValue<float>("SETTINGS", "SteeringReductionMultiplier", 0.25f);
            MaxAutoCounterSteerAngle = Settings.GetValue<float>("SETTINGS", "MaxAutoCounterSteerAngle", 12.5f);

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
                GTA.UI.Screen.ShowSubtitle("~y~Error in DriftPowerSP: " + ex.ToString());
            }
        }

        float CustomTractionCurveMax;
        float CustomTractionCurveMin;
        float CustomTractionMaxBias;
        float CustomTractionMinBias;
        float PowerSlideScale;
        float TorqueSlideScale;
        float CustomSteeringLock;
        float CustomCamberStiffness;
        float RotationZSpeedMultiplier;
        float SteeringSpeedMultiplier;
        float MaxSteeringAngle;
        float SteeringReductionMultiplier;
        float MaxAutoCounterSteerAngle;


        bool DriftPowerDebug = false;
        private Vehicle v;
        private VehicleWheel wheelFL;
        private VehicleWheel wheelFR;

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
                if (!Settings.GetValue<bool>("SETTINGS", "Enabled", true)) GTA.UI.Screen.ShowSubtitle("~y~DriftPower is disabled in Options.ini.");
                else DriftPowerDebug = !DriftPowerDebug;
            }
            if (WasCheatStringJustEntered("itscale"))
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

                if (!Settings.GetValue<bool>("SETTINGS", "Enabled", true)) GTA.UI.Screen.ShowSubtitle("~y~DriftPower is disabled in Options.ini.");
                else
                {
                    // Power/Torque
                    PowerSlideScale = PromptUserFloat("Power Slide Scale", PowerSlideScale);
                    TorqueSlideScale = PromptUserFloat("Torque Slide Scale", TorqueSlideScale);
                    // Traction
                    CustomTractionCurveMax = PromptUserFloat("Custom Traction Curve (Max)",  CustomTractionCurveMax);
                    CustomTractionCurveMin    = PromptUserFloat("Custom Traction Curve (Min)",  CustomTractionCurveMin);
                    CustomTractionMaxBias     = PromptUserFloat("Custom Traction Bias (Max)", CustomTractionMaxBias);
                    CustomTractionMinBias     = PromptUserFloat("Custom Traction Bias (Min)", CustomTractionMinBias);
                    // Steering
                    SteeringSpeedMultiplier = PromptUserFloat("Steering Speed Multiplier", SteeringSpeedMultiplier);
                    SteeringReductionMultiplier = PromptUserFloat("Steering Reduction Multiplier", SteeringReductionMultiplier);
                    MaxSteeringAngle = PromptUserFloat("Max Steering Angle", MaxSteeringAngle);
                    MaxAutoCounterSteerAngle = PromptUserFloat("Max Auto Counter Steer Angle", MaxAutoCounterSteerAngle);
                    // Other
                    RotationZSpeedMultiplier = PromptUserFloat("Rotation Z Speed Multiplier", RotationZSpeedMultiplier);
                    CustomSteeringLock    = PromptUserFloat("Custom Steering Lock",   CustomSteeringLock);
                    CustomCamberStiffness = PromptUserFloat("Custom Camber Stiffness", CustomCamberStiffness);
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

                    float absLatF  = System.Math.Abs(lateralForce);
                    float absLongF  = System.Math.Abs(longitudinalForce);

                    float expFact = MathUtils.Clamp01(MathUtils.Map(longitudinalForce, -1.3f, 1.3f, 0.3f, 0.7f, true) + Easing.EaseOutCirc01(MathUtils.Map(absLatF, 0f, 1.30f, 0f, 0.70f)));
                    float latFactAlt = Easing.EaseOutCirc01(MathUtils.Map(absLatF, 0f, 1.50f, 0f, 1f, true));
                    float fullFactAlt2   = MathUtils.Clamp01(MathUtils.Map(absLongF, 0f, 1.30f, 0f, 0.15f, true) + Easing.EaseOutCirc01(MathUtils.Map(absLatF, 0f, 1.50f, 0f, 1f)));
                    
                    var VHandling = v.HandlingData;

                    if (DriftPowerDebug)
                    {
                        //GTA.UI.Screen.ShowSubtitle("~n~~w~ X" + System.Math.Round(gForce.X, 2).ToString() + " " + "~n~~w~ Y" + System.Math.Round(gForce.Y, 2).ToString(), 500);
                        GTA.UI.Screen.ShowSubtitle("~n~~w~" + System.Math.Round(InitialTractionBiasFront, 2).ToString());
                    }

                    float exp = 1.791f;
                    float expLowTraction = 1.79315f;

                    float tractionFactor = MathUtils.Powf(fullFactAlt2, MathUtils.Lerp(exp, expLowTraction, expFact));
                    float latTractionFactor = MathUtils.Powf(latFactAlt, MathUtils.Lerp(exp, expLowTraction, expFact));

                    VHandling.TractionCurveMax = MathUtils.Lerp(InitialTractionCurveMax, CustomTractionCurveMax, CustomTractionMaxBias);
                    VHandling.TractionCurveMin = MathUtils.Lerp(InitialTractionCurveMin, CustomTractionCurveMin, CustomTractionMinBias);

                    VHandling.CamberStiffness = CustomCamberStiffness;

                    v.EnginePowerMultiplier  = MathUtils.Lerp(1f, PowerSlideScale, tractionFactor);
                    v.EngineTorqueMultiplier = MathUtils.Lerp(1f, TorqueSlideScale, tractionFactor);

                    VHandling.SteeringLock = CustomSteeringLock;
                    VHandling.SetVehicleLowSpeedTractionMult(0f);

                    VHandling.TractionBiasFront = MathUtils.Lerp(1.0025f, 0.9975f, latTractionFactor); // 0.00 | 2.00 range

                    VHandling.SetVehicleTractionCurveLateral(22.5f);

                    __currInertiaMultiplier.Z = InitialInertiaMultiplier.Z * RotationZSpeedMultiplier;

                    VHandling.InertiaMultiplier = __currInertiaMultiplier;

                    float customSteer = customSteering.CalculateCustomSteeringRatio(GTA.Game.GetDisabledControlValueNormalized(Control.VehicleMoveLeftRight), MaxSteeringAngle, 6.28f * SteeringSpeedMultiplier, 1f, MaxAutoCounterSteerAngle);
                    v.SteeringAngle = customSteer;
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
            GTA.UI.Screen.ShowSubtitle($"~y~DriftPower~w~~n~{valueName}: ~b~x" + value);
            string m = Game.GetUserInput(value.ToString()).Replace(",", ".");
            if (float.TryParse(m, out value))
            {
                GTA.UI.Screen.ShowSubtitle($"~y~DriftPower~w~~n~{valueName} set: ~b~x" + value + " ~w~");
            }
            else GTA.UI.Screen.ShowSubtitle("~y~DriftPower~w~~n~Invalid value: ~o~" + m);

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

            customSteering = new CustomSteering(veh);

            //for (int i = 0; i < veh.Wheels.Count; i++)
            //{
            //    VehicleWheel curWheel = veh.Wheels[i];
            //}
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