using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CPWE
{
    internal static class Utils //this class contains a bunch of helper and utility functions
    {
        internal const string version = "ALPHA v0.8.0";

        //------------------------------LOGGING FUNCTIONS--------------------------------
        //General information
        internal static void LogInfo(string message) 
        { 
            Debug.Log("[CPWE][INFO] " + message); 
        }
        //API Logging
        internal static void LogAPI(string message)
        {
            Debug.Log("[CPWE][API] " + message);
        }
        //If this appears in your log, it usually means a failsafe was tripped.
        internal static void LogWarning(string message) 
        { 
            Debug.LogWarning("[CPWE][WARNING] " + message); 
        }
        //Exceptions thrown by other sources.
        internal static void LogError(string message) 
        { 
            Debug.LogError("[CPWE][ERROR] " + message); 
        }

        //------------------------------SETTINGS AND SETUP--------------------------------
        internal static bool devMode = false;
        internal static bool minutesforcoords = false;
        internal static float GlobalWindSpeedMultiplier = 1.0f;

        //The game's path plus gamedata. Used to locate files in the gamedata folder.
        internal static string gameDataPath = KSPUtil.ApplicationRootPath + "GameData/";

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
            catch (Exception e) 
            { 
                LogError("An Exception occurred when loading the settings config. Exception thrown: " + e.ToString()); 
            }
        }

        //------------------------------FUNCTIONS TO CHECK FOR OTHER MODS-------------------------
        internal static bool FARConnected = false;

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
                            if (t.FullName.Equals("FerramAerospaceResearch.FARWind"))
                            {
                                FARAtm = t;
                            }
                            if (t.FullName.Equals("FerramAerospaceResearch.FARAtmosphere"))
                            {
                                FARAtm = t;
                            }
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
            catch (Exception e)
            {
                LogError("An Exception occurred when checking for FAR's presence. Exception thrown: " + e.ToString());
            }
            return false;
        }

        //------------------------------MATH FUNCTIONS >:3-------------------------

        //conversion factors between radians and degrees
        internal static double degtorad = Math.PI / 180.0;
        internal static double radtodeg = 180.0 / Math.PI;

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

            if (radians)
            {
                return angle;
            }
            return angle * radtodeg;
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
        internal static double RelativeHeading(double lon1, double lat1, double lon2, double lat2,bool radians=false)
        {
            //default to north if the two points are exactly antipodal or exactly on top of each other.
            float dotproduct = Math.Abs(Vector3.Dot(ToCartesian(lon1, lat1), ToCartesian(lon2, lat2)));
            if (dotproduct == 1.0f) 
            {
                return 0.0;
            }

            //Compute the angle between the north pole and the second point relative to the first point using the spherical law of cosines.
            //Don't worry, this hurt my brain, too. 
            double sideA = GreatCircleAngle(lon1, lat1, 0.0, 90.0, true); //craft to north pole
            double sideB = GreatCircleAngle(lon2, lat2, 0.0, 90.0, true); //center of current to north pole
            double sideC = GreatCircleAngle(lon1, lat1, lon2, lat2, true); //craft to center of current
            
            double heading = Math.Acos((Math.Cos(sideA) - (Math.Cos(sideB) * Math.Cos(sideC))) / (Math.Cos(sideB) * Math.Cos(sideC)));

            //The above function only computes the angle from 0 to 180 degrees, irrespective of east/west direction.
            //This line checks for that direction and modifies the heading accordingly.
            if (Math.Sin((lon1 - lon2) * degtorad) < 0)
            {
                heading *= -1;
            }
            if (radians)
            {
                return heading;
            }
            return heading * radtodeg;
        }

        /// <summary>
        /// Return a unit vector in Cartesian coordinates given longitude and latitude
        /// </summary>
        /// <param name="lon">Longitude</param>
        /// <param name="lat">Latitude</param>
        /// <returns></returns>
        internal static Vector3 ToCartesian(double lon, double lat)
        {
            lon *= degtorad;
            lat *= degtorad;
            return new Vector3((float)(Math.Cos(lat) * Math.Cos(lon)), (float)(Math.Sin(lon) * Math.Cos(lat)), (float)Math.Sin(lat)).normalized;
        }

        /// <summary>
        /// Brute-force the derivative (change in y / change in x) of the float curve at the given point.
        /// why is there no "FloatCurve.derivative()" function? that would have made life so much simpler =(
        /// </summary>
        /// <param name="fc">The float curve to take the derivative of</param>
        /// <param name="f">The time to take the derivative from</param>
        /// <returns>The derivative of the float curve at the given time value</returns>
        internal static float FloatCurveDerivative(FloatCurve fc,  float f)
        {
            return (fc.Evaluate(f + 0.0001f) - fc.Evaluate(f - 0.0001f))/0.0002f;
        }

        /// <summary>
        /// Check if val is within the range spanned by v1 and v2
        /// </summary>
        /// <param name="val">The value to compare</param>
        /// <param name="v1">The first extreme</param>
        /// <param name="v2">The second extreme</param>
        /// <returns>A boolean value indicating that the value is inside the range</returns>
        internal static bool InRange(double val, double v1, double v2)
        {
            if (v1 < v2)
            {
                return (val >= v1 && val <= v2);
            }
            else
            {
                return (val <= v1 && val >= v2);
            }
        }

        /// <summary>
        /// Lerp between two doubles.
        /// </summary>
        /// <param name="first">The first double, returned if by = 0</param>
        /// <param name="second">The second double, returned if by = 1</param>
        /// <param name="by">Value used to interpolate between first and second</param>
        /// <returns></returns>
        internal static double Lerp(double first, double second, double by)
        {
            return (first * (1.0 - by)) + (second * by);
        }

        /// <summary>
        /// Lerp between two floats.
        /// </summary>
        /// <param name="first">The first float, returned if by = 0</param>
        /// <param name="second">The second float, returned if by = 1</param>
        /// <param name="by">Value used to interpolate between first and second</param>
        /// <returns></returns>
        internal static float Lerp(float first, float second, float by)
        {
            return (first * (1.0f - by)) + (second * by);
        }

        /// <summary>
        /// Bilinear Interpolation between four points.
        /// </summary>
        /// <param name="first1">The first vector in the first pair</param>
        /// <param name="second1">The second vector in the first pair</param>
        /// <param name="first2">The first vector in the second pair</param>
        /// <param name="second2">The second vector in the second pair</param>
        /// <param name="byX">Value used to interpolate between the two vectors in each pair</param>
        /// <param name="byY">Value used to interpolate between the two pairs of vectors</param>
        /// <returns>The interpolated vector</returns>
        internal static Vector3 BiLerp(Vector3 first1, Vector3 second1, Vector3 first2, Vector3 second2, float byX, float byY)
        {
            Vector3 firstset = Vector3.Lerp(first1, second1, byX);
            Vector3 secondset = Vector3.Lerp(first2, second2, byX);
            return Vector3.Lerp(firstset, secondset, byY);
        }

        //------------------------------MISCELLANEOUS-------------------------

        /// <summary>
        /// Check if a vector's magnitude or any of its components return NaN or Infinity. This is to provide a failsafe against potentially unwanted behavior.
        /// </summary>
        /// <param name="v">The vector to check</param>
        /// <returns>true if the vector's magnitude or its x, y, or z component return NaN or Infinity, false otherwise.</returns>
        internal static bool IsVectorNaNOrInfinity(Vector3 v)
        {
            List<float> components = new List<float> { v.x, v.y, v.z };
            return (components.Any(c => float.IsInfinity(c) || float.IsNaN(c)) || float.IsInfinity(v.magnitude) || float.IsNaN(v.magnitude));
        }

        /// <summary>
        /// Evaluate the float curve at the time relative to the loop
        /// </summary>
        /// <param name="curve">The float curve to evaluate</param>
        /// <returns></returns>
        internal static float GetValAtLoopTime(FloatCurve curve)
        {
            return curve.Evaluate((float)Planetarium.GetUniversalTime() % curve.maxTime);
        }
    }
}
