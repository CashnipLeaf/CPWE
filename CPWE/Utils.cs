using System;
using UnityEngine;

namespace CPWE
{
    internal static class Utils //this class contains a bunch of helper and utility functions
    {
        internal const string version = "v0.8.5-alpha";

        //------------------------------LOGGING FUNCTIONS--------------------------------
        internal static void LogInfo(string message) => Debug.Log("[CPWE][INFO] " + message); //General information
        internal static void LogAPI(string message) => Debug.Log("[CPWE][API] " + message); //API Logging
        internal static void LogWarning(string message) => Debug.LogWarning("[CPWE][WARNING] " + message); //If this appears in your log, it usually means a failsafe was tripped.
        internal static void LogError(string message) => Debug.LogError("[CPWE][ERROR] " + message); //Exceptions thrown by other sources.

        //------------------------------SETTINGS AND SETUP--------------------------------
        internal static bool devMode = false;
        internal static bool minutesforcoords = false;
        internal static float GlobalWindSpeedMultiplier = 1.0f;
        internal static bool FARConnected = false;

        //The game's path plus gamedata. Used to locate files in the gamedata folder.
        internal static string GameDataPath { get { return KSPUtil.ApplicationRootPath + "GameData/"; } }

        internal static void CheckSettings()
        {
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

                //you do not get to set a negative wind speed multiplier, you masochist. >:3
                GlobalWindSpeedMultiplier = Math.Abs(windspeedmult);

                if (debug)
                {
                    LogInfo("Developer Mode Enabled.");
                    devMode = true;
                }
                minutesforcoords = mins;
            }
            catch (Exception e) { LogError("An Exception occurred when loading the settings config. Exception thrown: " + e.ToString()); }
        }  

        //------------------------------MATH FUNCTIONS >:3-------------------------

        //conversion factors between radians and degrees
        internal const double degtorad = Math.PI / 180.0;
        internal const double radtodeg = 180.0 / Math.PI;

        /// <summary>
        /// Brute-force the derivative (change in y / change in x) of the float curve at the given point.
        /// why is there no "FloatCurve.derivative()" function? that would have made life so much simpler =(
        /// </summary>
        /// <param name="fc">The float curve to take the derivative of</param>
        /// <param name="f">The time to take the derivative from</param>
        /// <returns>The derivative of the float curve at the given time value</returns>
        internal static float FloatCurveDerivative(FloatCurve fc,  float f) => (fc.Evaluate(f + 0.0001f) - fc.Evaluate(f - 0.0001f)) / 0.0002f;

        //Clamping functions
        internal static int Clamp(int value, int min, int max) => Math.Min(Math.Max(value, min), max);
        internal static float Clamp(float value, float min, float max) => Math.Min(Math.Max(value, min), max);
        internal static double Clamp(double value, double min, double max) => Math.Min(Math.Max(value, min), max);

        /// <summary>
        /// Check if val is within the range spanned by v1 and v2
        /// </summary>
        /// <param name="val">The value to compare</param>
        /// <param name="v1">The first extreme</param>
        /// <param name="v2">The second extreme</param>
        /// <returns>A boolean value indicating that the value is inside the range</returns>
        internal static bool InRange(double val, double v1, double v2) => (v2 >= v1) ? val >= v1 && val <= v2 : val >= v2 && val <= v1;

        //Lerping Functions
        internal static double Lerp(double first, double second, double by) => (first * (1.0 - by)) + (second * by);
        internal static float Lerp(float first, float second, float by) => (first * (1.0f - by)) + (second * by);
        internal static double LerpClamped(double first, double second, double by) => Lerp(first, second, Clamp(by, 0.0, 1.0));
        internal static float LerpClamped(float first, float second, float by) => Lerp(first, second, Clamp(by, 0.0f, 1.0f));

        internal static Vector3 BiLerp(Vector3 first1, Vector3 second1, Vector3 first2, Vector3 second2, float byX, float byY)
        {
            Vector3 firstset = Vector3.Lerp(first1, second1, byX);
            Vector3 secondset = Vector3.Lerp(first2, second2, byX);
            return Vector3.Lerp(firstset, secondset, byY);
        }

        //A class for faster approximations of select trigonometry functions
        internal static class FastTrig
        {
            private const double PI = 3.1415926535;
            private const double TwoPI = 6.2831853071;
            private const double C = 0.10501094;

            internal static double Sin(double x)
            {
                if (x < -PI)
                    x += TwoPI;
                else
                if (x > PI)
                    x -= TwoPI;

                double sinn = 1.27323954 * x + ((x < 0) ? 0.405284735 : -0.405284735) * x * x;
                return 0.225 * (sinn * ((sinn < 0) ? -sinn : sinn) - sinn) + sinn;
            }

            internal static double Cos(double x) => Sin(x + 1.570796327);
            internal static double Tan(double x) => Sin(x) / Cos(x);

            internal static double Acos(double x)
            {
                double r, s, u;
                u = 1.0 - ((x < 0) ? -x : x);
                s = Math.Sqrt(u + u);
                r = C * u * s + s;
                return (x < 0) ? PI - r : r;
            }
        }

        //------------------------------DIRECTION/DISTANCE FUNCTIONS-------------------------

        /// <summary>
        /// Calculate the Great Circle angle between two points. Remember, planets are spherical. Mostly, anyways.
        /// </summary>
        /// <param name="lon1">The longitude of the first point</param>
        /// <param name="lat1">The latitude of the first point</param>
        /// <param name="lon2">The longitude of the second point</param>
        /// <param name="lat2">The latitude of the second point</param>
        /// <param name="radians">Return the value in radians instead of degrees. default = false</param>
        /// <returns></returns>
        internal static double GreatCircleAngle(double lon1, double lat1, double lon2, double lat2, bool radians = false)
        {
            //convert degrees to radians
            lon1 *= degtorad;
            lat1 *= degtorad;
            lon2 *= degtorad;
            lat2 *= degtorad;
            double angle = Math.Acos((Math.Sin(lat1) * Math.Sin(lat2)) + (Math.Cos(lat1) * Math.Cos(lat2) * Math.Cos(Math.Abs(lon1 - lon2))));
            return radians ? angle : angle * radtodeg;
        }

        /// <summary>
        /// Calculate the relative heading from point 1 to point 2, with 0 being north, 90 being east, 180 being south, and -90/270 being west.
        /// </summary>
        /// <param name="lon1">The longitude of the first point</param>
        /// <param name="lat1">The latitude of the first point</param>
        /// <param name="lon2">The longitude of the second point</param>
        /// <param name="lat2">The latitude of the second point</param>
        /// <param name="radians">Return the value in radians instead of degrees. default = false</param>
        /// <returns></returns>
        internal static double RelativeHeading(double lon1, double lat1, double lon2, double lat2, bool radians = false)
        {
            //default to north if the two points are exactly antipodal or exactly on top of each other.
            if((lat1 == lat2 && lon1 == lon2) || ((lat1 == (lat2 * -1)) && (lon1 + 180 == lon2 || lon1 - 180 == lon2))) { return 0.0; }

            //Compute the angle between the north pole and the second point relative to the first point using the spherical law of cosines.
            //Don't worry, this hurt my brain, too. 
            double sideA = GreatCircleAngle(lon1, lat1, 0.0, 90.0, true); //craft to north pole
            double sideB = GreatCircleAngle(lon2, lat2, 0.0, 90.0, true); //center of current to north pole
            double sideC = GreatCircleAngle(lon1, lat1, lon2, lat2, true); //craft to center of current

            double heading = Math.Acos((Math.Cos(sideA) - (Math.Cos(sideB) * Math.Cos(sideC))) / (Math.Cos(sideB) * Math.Cos(sideC)));

            //The above function only computes the angle from 0 to 180 degrees, irrespective of east/west direction.
            //This line checks for that direction and modifies the heading accordingly.
            if (Math.Sin((lon1 - lon2) * degtorad) < 0) { heading *= -1; }
            return radians ? heading : heading * radtodeg;
        }

        //------------------------------MISCELLANEOUS------------------------- 
        internal static bool IsVectorNaNOrInfinity(Vector3 v) => IsNaNOrInfinity(v.x) || IsNaNOrInfinity(v.y) || IsNaNOrInfinity(v.z);
        internal static unsafe bool IsNaNOrInfinity(float f) => (*(int*)(&f) & 0x7F800000) == 0x7F800000;
        internal static unsafe bool IsNaNOrInfinity(double d) => (*(long*)(&d) & 0x7FF0000000000000L) == 0x7FF0000000000000L;

        /// <summary>
        /// Evaluate the float curve at the time relative to the loop
        /// </summary>
        /// <param name="curve">The float curve to evaluate</param>
        /// <returns></returns>
        internal static float GetValAtLoopTime(FloatCurve curve) => curve.Evaluate((float)Planetarium.GetUniversalTime() % curve.maxTime);
    }
}
