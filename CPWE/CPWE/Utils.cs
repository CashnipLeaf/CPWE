using System;
using UnityEngine;

namespace CPWE
{
    internal static class Utils //this class contains a bunch of helper and utility functions
    {
        internal const string version = "v0.8.6-alpha";

        //------------------------------LOGGING FUNCTIONS--------------------------------
        internal static void LogInfo(string message) => Debug.Log("[CPWE][INFO] " + message); //General information
        internal static void LogAPI(string message) => Debug.Log("[CPWE][API] " + message); //API Logging
        internal static void LogWarning(string message) => Debug.LogWarning("[CPWE][WARNING] " + message); //If this appears in your log, it usually means a failsafe was tripped.
        internal static void LogError(string message) => Debug.LogError("[CPWE][ERROR] " + message); //Exceptions thrown by other sources.

        //------------------------------SETTINGS AND SETUP--------------------------------
        private static bool devMode = false;
        internal static bool DevMode => devMode;

        private static bool minsforcoords = false;
        internal static bool Minutesforcoords => minsforcoords;

        private static float globalWindSpeedMultiplier = 1.0f;
        internal static float GlobalWindSpeedMultiplier
        {
            get => globalWindSpeedMultiplier;
            set => globalWindSpeedMultiplier = Clamp(value, 0, float.MaxValue);
        }

        internal static bool FARConnected = false;

        //The game's path plus gamedata. Used to locate files in the gamedata folder.
        internal static string GameDataPath => KSPUtil.ApplicationRootPath + "GameData/";

        internal static void CheckSettings()
        {
            LogInfo("Loading Settings Config.");
            //read the settings node if it exists.
            try
            {
                bool debug = false;
                bool mins = false;
                float windspeedmult = 1.0f;
                ConfigNode[] settings = GameDatabase.Instance.GetConfigNodes("CPWE_SETTINGS");
                settings[0].TryGetValue("DeveloperMode", ref debug);
                settings[0].TryGetValue("UseMOAForCoords", ref mins);
                settings[0].TryGetValue("GlobalWindSpeedMultiplier", ref windspeedmult);

                if (debug)
                {
                    LogInfo("Developer Mode Enabled.");
                    devMode = true;
                }
                minsforcoords = mins;
                GlobalWindSpeedMultiplier = windspeedmult;
            }
            catch (Exception e) { LogError("An Exception occurred when loading the settings config. Exception thrown: " + e.ToString()); }
        }  

        //------------------------------MATH FUNCTIONS >:3-------------------------

        //conversion factors between radians and degrees
        internal const double degtorad = 0.017453292519943295; //Math.PI / 180.0
        internal const double radtodeg = 57.295779513082323402053960025448; //180.0 / Math.PI

        internal static float FloatCurveDerivative(FloatCurve fc,  float f) => (fc.Evaluate(f + 0.0001f) - fc.Evaluate(f - 0.0001f)) * 5000.0f; //thing / 0.0002f

        //Clamping functions
        internal static int Clamp(int value, int min, int max) => Math.Min(Math.Max(value, min), max);
        internal static float Clamp(float value, float min, float max) => Math.Min(Math.Max(value, min), max);
        internal static double Clamp(double value, double min, double max) => Math.Min(Math.Max(value, min), max);
        internal static float Clamp01(float value) => Math.Min(Math.Max(value, 0.0f), 1.0f);
        internal static double Clamp01(double value) => Math.Min(Math.Max(value, 0.0), 1.0);

        internal static bool InRange(double val, double v1, double v2) => (v2 >= v1) ? val >= v1 && val <= v2 : val >= v2 && val <= v1;

        //Lerping Functions
        internal static double Lerp(double first, double second, double by) => (first * (1.0 - by)) + (second * by);
        internal static float Lerp(float first, float second, float by) => (first * (1.0f - by)) + (second * by);
        internal static double LerpClamped(double first, double second, double by) => Lerp(first, second, Clamp01(by));
        internal static float LerpClamped(float first, float second, float by) => Lerp(first, second, Clamp01(by));

        internal static Vector3 BiLerp(Vector3 first1, Vector3 second1, Vector3 first2, Vector3 second2, float byX, float byY)
        {
            return Vector3.Lerp(Vector3.Lerp(first1, second1, byX), Vector3.Lerp(first2, second2, byX), byY);
        }

        //A class for faster approximations of select trigonometry functions
        internal static class FastTrig
        {
            private const double OneOverTwoPI = 0.15915494309189534278348322229291; //1.0 / (2.0 * Math.PI)
            internal static double Sin(double x)
            {
                return (double) SineLUT.lut[(int)(((x * OneOverTwoPI) + 1.0) * 1024.0) % 1024]; 
                //if you value not needing eye bleach, DO NOT open the SineLUT.cs file.
                //trust me, it is HIDEOUS

                /*
                if (x < -Math.PI) { x += 2 * Math.PI; }
                if (x > Math.PI) { x -= 2 * Math.PI; }

                double sinn = 1.27323954 * x + (x < 0 ? 0.405284735 : -0.405284735) * x * x;
                return 0.225 * (sinn * (sinn < 0 ? -sinn : sinn) - sinn) + sinn;
                */
            }

            private const double HalfPI = 1.57079632679489655; //Math.PI * 0.5
            internal static double Cos(double x) => Sin(x + HalfPI);
            internal static double Tan(double x) => Sin(x) / Cos(x);

            /*internal static double Acos(double x)
            {
                double r, s, u;
                u = 1.0 - ((x < 0) ? -x : x);
                s = Math.Sqrt(u + u);
                r = 0.10501094 * u * s + s;
                return (x < 0) ? PI - r : r;
            }*/
        }

        //------------------------------DIRECTION/DISTANCE FUNCTIONS-------------------------

        //Calculate the Great Circle angle between two points. Remember, planets are spherical. Mostly, anyways.
        internal static double GreatCircleAngle(double lon1, double lat1, double lon2, double lat2, bool radians = false)
        {
            //convert degrees to radians
            lon1 *= degtorad;
            lat1 *= degtorad;
            lon2 *= degtorad;
            lat2 *= degtorad;
            double angle = Math.Acos((FastTrig.Sin(lat1) * FastTrig.Sin(lat2)) + (FastTrig.Cos(lat1) * FastTrig.Cos(lat2) * FastTrig.Cos(Math.Abs(lon1 - lon2))));
            return radians ? angle : angle * radtodeg;
        }

        //Calculate the relative heading from point 1 to point 2, with 0 being north, 90 being east, 180 being south, and -90/270 being west.
        internal static double RelativeHeading(double lon1, double lat1, double lon2, double lat2, bool radians = false)
        {
            //default to north if the two points are exactly antipodal or exactly on top of each other.
            if((lat1 == lat2 && lon1 == lon2) || ((lat1 == (lat2 * -1.0)) && (lon1 + 180.0 == lon2 || lon1 - 180.0 == lon2))) { return 0.0; }

            //Compute the angle between the north pole and the second point relative to the first point using the spherical law of cosines.
            //Don't worry, this hurt my brain, too. 
            double sideA = GreatCircleAngle(lon1, lat1, 0.0, 90.0, true); //craft to north pole
            double sideB = GreatCircleAngle(lon2, lat2, 0.0, 90.0, true); //center of current to north pole
            double sideC = GreatCircleAngle(lon1, lat1, lon2, lat2, true); //craft to center of current

            double heading = Math.Acos((FastTrig.Cos(sideA) - (FastTrig.Cos(sideB) * FastTrig.Cos(sideC))) / (FastTrig.Cos(sideB) * FastTrig.Cos(sideC)));

            //The above function only computes the angle from 0 to 180 degrees, irrespective of east/west direction.
            //This line checks for that direction and modifies the heading accordingly.
            if (FastTrig.Sin((lon1 - lon2) * degtorad) < 0) { heading *= -1; }
            return radians ? heading : heading * radtodeg;
        }

        //------------------------------MISCELLANEOUS------------------------- 
        internal static bool IsVectorNaNOrInfinity(Vector3 v) => IsNaNOrInfinity(v.x) || IsNaNOrInfinity(v.y) || IsNaNOrInfinity(v.z);
        internal static bool IsVectorNaNOrInfinity(Vector3d vd) => IsNaNOrInfinity(vd.x) || IsNaNOrInfinity(vd.y) || IsNaNOrInfinity(vd.z);
        internal static bool IsNaNOrInfinity(float f) => float.IsInfinity(f) || float.IsNaN(f);
        internal static bool IsNaNOrInfinity(double d) => double.IsInfinity(d) || double.IsNaN(d);

        internal static float GetValAtLoopTime(FloatCurve curve, double time) => curve.Evaluate(((float)time + curve.maxTime) % curve.maxTime);

        internal static bool CheckFAR() //check if FAR is installed
        {
            try
            {
                Type FARAtm = null;
                foreach (var assembly in AssemblyLoader.loadedAssemblies)
                {
                    if (assembly.name == "FerramAerospaceResearch")
                    {
                        var types = assembly.assembly.GetExportedTypes();
                        foreach (Type t in types)
                        {
                            if (t.FullName.Equals("FerramAerospaceResearch.FARWind")) { FARAtm = t; }
                            if (t.FullName.Equals("FerramAerospaceResearch.FARAtmosphere")) { FARAtm = t; }
                        }
                    }
                }
                if (FARAtm != null)
                {
                    LogInfo("FerramAerospaceResearch detected. Aerodynamics calculations will be deferred to FAR.");
                    return true;
                }
                return false;
            }
            catch (Exception e) { LogError("An Exception occurred when checking for FAR's presence. Exception thrown: " + e.ToString()); }
            return false;
        }
    }
}
