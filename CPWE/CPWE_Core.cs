using System;
using System.Reflection;
using UnityEngine;

namespace CPWE
{
    //Delegate for FAR
    using WindDelegate = Func<CelestialBody, Part, Vector3, Vector3>;

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class CPWE_Core : MonoBehaviour
    {
        private static CPWE_Core instance;
        public static CPWE_Core Instance => instance;

        private Vessel activevessel;
        private Part refpart;
        private CPWE_Data data;
        internal Matrix4x4 vesselframe = Matrix4x4.identity;

        private bool haswind = false;
        internal bool HasWind => haswind;

        private Vector3 windVec = Vector3.zero;
        internal Vector3 CachedWind => windVec;

        private Vector3 appliedWind = Vector3.zero;
        internal Vector3 AppliedWind => appliedWind; 
        
        private Vector3 rawWindVec = Vector3.zero;
        internal Vector3 RawWind => rawWindVec;

        private string source = "None";
        internal string Source => source;

        public CPWE_Core()
        {
            if(instance == null)
            {
                Utils.LogInfo("Initializing flight scene handler.");
                instance = this;
            }
            else { Destroy(this); }
        }

        void Awake()
        {
            data = CPWE_Data.Instance;
            RegisterWithFAR();
        }

        void FixedUpdate()
        {
            //reset cached data
            haswind = false;
            source = "None";
            windVec = Vector3.zero;
            rawWindVec = Vector3.zero;
            refpart = null;

            if (!FlightGlobals.ready || FlightGlobals.ActiveVessel == null) { return; }
            activevessel = FlightGlobals.ActiveVessel;

            //Get the worldframe of the vessel in question to transform the wind vector to be relative to the worldframe.
            vesselframe = Matrix4x4.identity;
            vesselframe.SetColumn(0, (Vector3)activevessel.north);
            vesselframe.SetColumn(1, (Vector3)activevessel.upAxis);
            vesselframe.SetColumn(2, (Vector3)activevessel.east);

            //Get the first part with a rigidbody (this is almost always the root part, but it never hurts to check)
            //If no such part is found, return.
            foreach (Part p in activevessel.Parts)
            {
                if (p.rb != null)
                {
                    refpart = p;
                    break;
                }
            }
            if (refpart == null) { return; }

            CelestialBody body = activevessel.mainBody;
            double lon = activevessel.longitude;
            double lat = activevessel.latitude;
            double alt = activevessel.altitude;
            Vector3 worldpos = activevessel.GetWorldPos3D();

            //cache wind vector so it only needs to be calculated once per frame.
            if (refpart && refpart.staticPressureAtm > 0.0)
            {
                //External Data is currently disabled for the time being
                /*try
                {
                    string bodysource = CPWE_API.GetExternalWindSource(body);
                    if (!string.IsNullOrEmpty(bodysource))
                    {
                        windVec = CPWE_API.GetExternalWind(body, refpart, worldpos);
                        if (Utils.IsVectorNaNOrInfinity(windVec)) { throw new Exception("External wind data returned a NaN or Infinity vector."); }
                        rawWindVec = vesselframe.inverse * windVec;
                        source = bodysource;
                        haswind = true;
                        return;
                    }
                }
                catch (Exception e) { Utils.LogWarning(e.Message + " Defaulting to Internal wind data."); }
                */

                windVec = Vector3.zero;
                if(data != null)
                {
                    if (data.HasBody(body.name))
                    {
                        rawWindVec = data.GetWind(body.name, lon, lat, alt, Planetarium.GetUniversalTime());
                        //Failsafe in case the internal data somehow returned a non-finite vector.
                        if (Utils.IsVectorNaNOrInfinity(rawWindVec))
                        {
                            Utils.LogWarning("Internal Wind Data returned a NaN or Infinity vector. Defaulting to a Zero vector.");
                            source = "None";
                            rawWindVec = Vector3.zero;
                            return;
                        }
                        source = "Internal Data";
                        windVec = vesselframe * rawWindVec;
                        appliedWind = windVec * Utils.GlobalWindSpeedMultiplier;
                        haswind = true;
                    }
                }
            }
        }

        void OnDestroy()
        {
            Utils.LogInfo("Flight Scene has ended. Unloading flight scene handler.");
            Utils.FARConnected = false;
            instance = null;
        }

        internal Vector3 GetTheWind(CelestialBody body, Part p, Vector3 pos) => windVec * Utils.GlobalWindSpeedMultiplier;

        //Register CPWE with FAR.
        internal bool RegisterWithFAR()
        {
            try
            {
                //Define type methods
                Type FARWindFunc = null;
                Type FARAtm = null;
                foreach (var assembly in AssemblyLoader.loadedAssemblies)
                {
                    if (assembly.name == "FerramAerospaceResearch")
                    {
                        Utils.LogInfo("Attempting to Register CPWE with FAR...");
                        var types = assembly.assembly.GetExportedTypes();

                        foreach (Type t in types)
                        {
                            if (t.FullName.Equals("FerramAerospaceResearch.FARWind")) { FARAtm = t; }
                            if (t.FullName.Equals("FerramAerospaceResearch.FARWind+WindFunction")) { FARWindFunc = t; }
                            if (t.FullName.Equals("FerramAerospaceResearch.FARAtmosphere")) { FARAtm = t; }
                        }
                    }
                }

                //If no wind or atmosphere is available return false
                if (FARAtm == null) { return false; }

                if (FARWindFunc != null) //Check if an older version of FAR is installed
                {
                    //Get FAR Wind Method
                    MethodInfo SetWindFunction = FARAtm.GetMethod("SetWindFunction");
                    if (SetWindFunction == null) { return false; }

                    //Set FARWind function
                    var del = Delegate.CreateDelegate(FARWindFunc, this, typeof(CPWE_Core).GetMethod("GetTheWind"), true);
                    SetWindFunction.Invoke(null, new object[] { del });
                }
                else
                {
                    //Get FAR Atmosphere Methods 
                    MethodInfo SetWindFunction = FARAtm.GetMethod("SetWindFunction");

                    //Return false if no wind method available
                    if (SetWindFunction == null) { return false; }
                    // Set FAR Atmosphere functions
                    WindDelegate del1 = GetTheWind;
                    SetWindFunction.Invoke(null, new object[] { del1 });
                }
                Utils.LogInfo("Successfully Registered with FerramAerospaceResearch");
                Utils.FARConnected = true;
                return true;
            }
            catch (Exception e) { Utils.LogError("An Exception occurred when registering CPWE with FerramAerospaceResearch. Exception thrown: " + e.ToString()); }
            return false;
        }
    }
}
