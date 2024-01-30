using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CPWE
{
    //API for interfacing with this mod.
    //CURRENTLY DISABLED.
    public static class CPWE_API
    {
        public delegate Vector3 windDelegate(CelestialBody cb, Part p, Vector3 pos);
        public delegate bool HasWind();
        public delegate double altitudeTransform(double alt);

        internal static altitudeTransform alttransform = null;
        internal static windDelegate globalWind = null;
        internal static HasWind hasGlobalWind = null;
        internal static string globalwindname = "";
        internal static int globalwindinvalidcounter = 0;
        internal static Dictionary<string, BodyWindData> externalbodydata = new Dictionary<string, BodyWindData>();
        internal const string unknown = "Unidentified Mod";

        internal static CPWE_Core core = null;

        //Retrieve the current wind vector. CPWE is only active during the FLIGHT scene, so a zero vector will be returned if accessed outside of the FLIGHT scene.
        public static Vector3 GetWindData(CelestialBody cb, Part p, Vector3 pos)
        {
            if (core == null) { return Vector3.zero; }
            return core.GetCachedWind();
        }

        /*--------------------------EXTERNAL WIND DATA SOURCES--------------------------
         * You can register a mod to supply wind vectors to CPWE, which it will then use in place of any internally stored wind data.
         * You may register your mod as a source of wind globally or for a specific body. 
         * 
         * Due to CPWE's dynamic data source selection, registering a global source of wind vectors requires a boolean function that lets CPWE know if wind data is available.
         * 
         * If a mod registers for an specific body, CPWE will assume that mod contains data for the entire body, thus a "HasWind" function is not necessary.
         * Note: Multiple mods can register body-specific wind data, but only one mod at a time can register for a given body.
         * (As an example: Mod 1 registers wind data for Kerbin and Eve, Mod 2 registers wind data for Laythe and Duna, and Mod 3 registers wind data for Jool)
         * 
         * I also recommend entering a name for your mod. This is helpful for debug purposes.
         * 
         * Data Hierarchy
         * CPWE will first check if a mod has registered for the current main body. If one has registered, it will retrieve the wind vector from that mod.
         * If no mods have registered for the current main body, it will then check if a mod has registered for global wind data. If one has registered and has data available, it will retrieve the wind vector from that mod.
         * If no external wind data is available, CPWE will retrieve the wind vector from its internally-stored wind objects.
         *  
         * Protection against unwanted behavior:
         * If your mod returns a NaN or Infinity wind vector, CPWE will ignore the data from the mod and substitute in its own internal data, or use a zero vector if no internal data is available.
         * If this happens three times in a row, CPWE will automatically de-register that mod as a failsafe.
         * 
         * NOTE: External Wind Data is currently disabled. It will be enabled in a BETA release.
         */

        /// <summary>
        /// Register your mod with CPWE to supply wind vectors to the mod on any body. 
        /// Due to CPWE's ability to dynamically select its wind data source, this requires the mod to be able to tell CPWE when it can provide wind data via a function that returns a boolean value.
        /// </summary>
        /// <param name="dlg">The function that CPWE will call to retrieve the wind vector</param>
        /// <param name="hw">Boolean value indicating whether data is available. If this returns false, CPWE will use its internally-stored data.</param>
        /// <param name="name">(optional) A name for your mod. This name is only shown in developer mode information and is intended to be used for debug purposes.</param>
        /// <returns>true if registration was successful, false if another mod has already registered global wind data.</returns>
        public static bool RegisterGlobalWindData(windDelegate dlg, HasWind hw, string name)
        {
            if (globalWind == null)
            {
                globalWind = dlg;
                hasGlobalWind = hw;
                globalwindname = name;
                globalwindinvalidcounter = 0;
                Utils.LogAPI(name + " has registered as a Global Wind Data source.");
                return true;
            }
            Utils.LogAPI("Could not register " + name + " as a Wind Data source. Another mod has already registered.");
            return false;
        }

        //Alternate if you dont want to include a name for some odd reason.
        public static bool RegisterGlobalWindData(windDelegate dlg, HasWind hw)
        {
            return RegisterGlobalWindData(dlg, hw, unknown);
        }

        /// <summary>
        /// Register external wind data for a specific body. CPWE will assume that any mods registering wind data for a given body contain wind data for the entire body. 
        /// Thus, a function to specify whether data is available is not needed.
        /// </summary>
        /// <param name="body">The internal name of the body you want to register wind data for</param>
        /// <param name="dlg">The function that CPWE will call to retrieve the wind vector.</param>
        /// <param name="name">(optional) A name for your mod. This name is only shown in developer mode information and is intended to be used for debug purposes.</param>
        /// <returns>true if registration was successful, false if another mod has already registered wind data for that body.</returns>
        public static bool RegisterBodyWindData(string body, windDelegate dlg, string name)
        {
            if (externalbodydata.ContainsKey(body)) 
            {
                Utils.LogAPI("Could not register " + name + " as a Wind Data source for the Celestial Body" + body + ". Another mod has already registered.");
                return false; 
            }
            externalbodydata.Add(body, new BodyWindData(name, dlg));
            Utils.LogAPI(name + " has registered as a Wind Data source for the Celestial Body" + body + ".");
            return true;
        }

        //Alternate if you dont want to include a name for some odd reason.
        public static bool RegisterBodyWindData(string body, windDelegate dlg)
        {
            return RegisterBodyWindData(body, dlg, unknown);
        }

        /// <summary>
        /// Alternate function to register external wind data for multiple bodies in one fell swoop.
        /// </summary>
        /// <param name="bodies">A List of the internal names of the bodies you want to register wind data for</param>
        /// <param name="dlg">The function that CPWE will call to retrieve the wind vector.</param>
        /// <param name="name">(optional) A name for your mod. This name is only shown in developer mode information and is intended to be used for debug purposes.</param>
        /// <returns>true if registration was successful for all bodies, false if another mod has already registered wind data for at least one body in the list.</returns>
        public static bool RegisterBodyWindData(List<string> bodies, windDelegate dlg, string name)
        {
            List<bool> allregistered = new List<bool>();
            foreach(string body in bodies)
            {
                allregistered.Add(RegisterBodyWindData(body, dlg, name));
            }
            return allregistered.All(b => b == true);
        }

        //Alternate if you dont want to include a name for some odd reason.
        public static bool RegisterBodyWindData(List<string> bodies, windDelegate dlg)
        {
            return RegisterBodyWindData(bodies, dlg, unknown);
        }

        //-------------Retrieve External Wind Data-------------
        internal static string GetExternalWindSource(CelestialBody cb)
        {
            if (externalbodydata.ContainsKey(cb.name))
            {
                return externalbodydata[cb.name].GetName();
            }
            if (hasGlobalWind())
            {
                return globalwindname;
            }
            return null;
        }

        internal static Vector3 GetExternalWind(CelestialBody cb, Part p, Vector3 pos)
        {
            string body = cb.name;
            if (externalbodydata.ContainsKey(body))
            {
                Vector3 bodywind = externalbodydata[body].GetWind(cb, p, pos);
                if (Utils.IsVectorNaNOrInfinity(bodywind))
                {
                    externalbodydata[body].invalidcounter += 1;
                    if (externalbodydata[body].invalidcounter >= 3)
                    {
                        string copyname = string.Copy(externalbodydata[body].GetName());
                        DeRegisterBodyWind(body);
                        throw new Exception("CPWE has de-registered " + copyname + " as a source of wind data for " + body + " as it has returned three consecutive NaN or Infinity wind vectors.");
                    }
                    throw new Exception(externalbodydata[body].GetName() + " returned a NaN or Infinity wind vector for " + body + ".");
                }
                else
                {
                    externalbodydata[body].invalidcounter = 0;
                    if (bodywind == null) { return Vector3.zero; }
                    else { return bodywind; }
                }
            }
            if (globalWind != null)
            {
                Vector3 globalwindvec = globalWind(cb, p, pos);
                if (Utils.IsVectorNaNOrInfinity(globalwindvec))
                {
                    globalwindinvalidcounter += 1;
                    if (globalwindinvalidcounter >= 3)
                    {
                        string copyname = string.Copy(globalwindname);
                        DeRegisterGlobalWind();
                        throw new Exception("CPWE has de-registered " + copyname + " as a source of global wind data as it has returned three consecutive NaN or Infinity wind vectors.");
                    }
                    throw new Exception(globalwindname + " returned a NaN or Infinity wind vector.");
                }
                else
                {
                    globalwindinvalidcounter = 0;
                    return globalWind(cb, p, pos);
                }
            }
            return Vector3.zero;
        }

        internal class BodyWindData
        {
            internal string name;
            internal windDelegate dlg;
            internal int invalidcounter;

            internal BodyWindData(string name, windDelegate dlg)
            {
                this.name = name;
                this.dlg = dlg;
                invalidcounter = 0;
            }
            internal string GetName() { return name; }
            internal Vector3 GetWind(CelestialBody body, Part part, Vector3 pos) { return dlg(body, part, pos); }
        }

        //Function provided to get a matrix to transform the wind vector to the desired vessel's reference frame
        public static Matrix4x4 GetRefFrame(Vessel v)
        {
            Matrix4x4 vesselframe = Matrix4x4.identity;
            vesselframe.SetColumn(0, (Vector3)v.north);
            vesselframe.SetColumn(1, (Vector3)v.upAxis);
            vesselframe.SetColumn(2, (Vector3)v.east);
            return vesselframe;
        }

        //-------------Modify the altitude that the wind vector is retrieved from-------------
        public static bool RegisterAltitudeTransformation(altitudeTransform alty)
        {
            if(alttransform == null)
            {
                alttransform = alty;
                return true;
            }
            return false;
        }

        internal static double TransformAltitude(double alt)
        {
            if(alttransform == null) { return alt; }
            double newalt = alttransform(alt);
            if(double.IsNaN(newalt) || double.IsInfinity(newalt))
            {
                return alt;
            }
            return newalt;
        }

        //De-register a mod with CPWE if the mod returns a NaN or Infinity vector three times in a row.
        internal static void DeRegisterBodyWind(string body)
        {
            externalbodydata.Remove(body);
        }
        internal static void DeRegisterGlobalWind()
        {
            globalWind = null;
            globalwindinvalidcounter = 0;
            globalwindname = null;
            hasGlobalWind = null;
        }
    }
}
