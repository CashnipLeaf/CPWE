using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace CPWE
{
    //Delegate for FAR
    using WindDelegate = Func<CelestialBody, Part, Vector3, Vector3>;

    //Core class of this plugin. Handles wind data setup and supplies data to the MFI aero update.
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    [DefaultExecutionOrder(1)]
    public class CPWE_Core : MonoBehaviour
    {
        private static CPWE_Core instance;
        public static CPWE_Core Instance { get { return instance; } }

        private CPWE_Object atmocurrents;
        private Vessel activevessel;
        private Part refpart;
        internal bool haswind;
        internal Vector3 windVec = Vector3.zero;
        internal string source = "None";

        public CPWE_Core()
        {
            //Check for duplicate instances and destroy any that might be present.
            if (instance == null)
            {
                instance = this;
                Utils.LogInfo("Initializing CPWE Core.");
            }
            else
            {
                Utils.LogWarning("Destroying duplicate instance. Check your install for duplicate mod folders.");
                Destroy(this);
            }
        }

        void Awake()
        {
            RegisterWithFAR();
            CPWE_API.core = this;

            //read all the nodes and create the wind objects to be stored in memory
            atmocurrents = new CPWE_Object();
            UrlDir.UrlConfig[] CPWE_Configs = GameDatabase.Instance.GetConfigs("CPWE");
            Utils.LogInfo("Loading Configs.");

            foreach (UrlDir.UrlConfig _url in CPWE_Configs)
            {
                ConfigNode[] CPWE_bodies = _url.config.GetNodes("CPWE_BODY");

                foreach (ConfigNode cn in CPWE_bodies)
                {
                    string body = "";
                    double altitudeScaleFactor = 1.0;
                    cn.TryGetValue("body", ref body);
                    if (string.IsNullOrEmpty(body)) //check that a body key exists in the CPWE_BODY node
                    {
                        Utils.LogWarning("Unable to initialize CPWE_BODY node. No body name given.");
                        continue;
                    }

                    cn.TryGetValue("altitudeScaleFactor", ref altitudeScaleFactor); //intended to be used if a body's atmosphere gets rescaled.
                    if (altitudeScaleFactor <= 0.0) //You dont get to break my mod >:3.
                    {
                        Utils.LogWarning("An altitudeScaleFactor less than or equal to 0 was inputted for " + body + ". A default value of 1 will be used.");
                        altitudeScaleFactor = 1.0;
                    }

                    CelestialBody cb = FlightGlobals.GetBodyByName(body);
                    if (cb) //Check if the body exists before creating a new object.
                    {
                        if (!cb.atmosphere)
                        {
                            Utils.LogInfo(body + " does not have an atmosphere. No wind data will be stored for it to conserve memory.");
                            continue;
                        }
                        Utils.LogInfo("Creating CPWE object for " + body);
                        atmocurrents.AddBody(body, altitudeScaleFactor);
                        ConfigNode[] windnodes = cn.GetNodes("Wind");
                        ConfigNode[] flowmaps = cn.GetNodes("Flowmap");
                        //read all the wind nodes
                        if (windnodes.Length > 0)
                        {
                            Utils.LogInfo("Loading Wind Objects for " + body);
                            foreach (ConfigNode wind in windnodes)
                            {
                                try
                                {
                                    atmocurrents.AddWind(body, ReadWindNode(wind));
                                }
                                catch (Exception ex)
                                {
                                    Utils.LogWarning("Unable to load Wind object: " + ex.Message);
                                }
                            }
                        }
                        //read the flowmap nodes
                        if (flowmaps.Length > 0)
                        {
                            Utils.LogInfo("Loading Flowmap Objects for " + body);
                            foreach (ConfigNode flowmap in flowmaps)
                            {
                                try
                                {
                                    atmocurrents.AddFlowMap(body, ReadFlowMapNode(flowmap));
                                }
                                catch (Exception ex)
                                {
                                    Utils.LogWarning("Unable to load Flowmap object: " + ex.Message);
                                }
                            }
                        }
                    }
                    else
                    {
                        Utils.LogInfo("Body " + body + " not found.");
                    }
                }
            }
            Utils.LogInfo("All Configs Loaded.");
        }

        void FixedUpdate()
        {
            //reset cached data
            haswind = false;
            source = "None";
            windVec = Vector3.zero;
            refpart = null;

            activevessel = FlightGlobals.ActiveVessel;
            if (activevessel == null) { return; }

            //get the first part with a rigidbody (this is almost always the root part, but it never hurts to check)
            foreach (Part p in activevessel.Parts)
            {
                if (p.rb != null)
                {
                    refpart = p;
                    break;
                }
            }

            CelestialBody body = activevessel.mainBody;
            double lon = activevessel.longitude;
            double lat = activevessel.latitude;
            double alt = activevessel.altitude;

            //cache wind vector at each frame
            if(refpart && refpart.staticPressureAtm > 0.0)
            {
                //External Data is currently disabled for the time being
                /*string bodysource = CPWE_API.GetExternalWindSource(body);
                if (!string.IsNullOrEmpty(bodysource))
                {
                    try
                    {
                        windVec = CPWE_API.GetExternalWind(body, refpart, refpart.rb.position);
                        source = bodysource;
                        haswind = true;
                        return;
                    }
                    catch (Exception e)
                    {
                        Utils.LogWarning(e.Message);
                    }
                }*/
                source = "Internal Data";
                if (atmocurrents.HasBody(body.name))
                {
                    windVec = atmocurrents.GetWindVector(body.name, lon, lat, alt);
                    if (Utils.IsVectorNaNOrInfinity(windVec))
                    {
                        source = "None";
                        Utils.LogWarning("Internal Wind Data returned a NaN or Infinity vector. Defaulting to a Zero vector.");
                        windVec = Vector3.zero;
                        return;
                    }
                    windVec = GetRefFrame(activevessel) * windVec;
                    haswind = true;
                }
            }
        }

        //get the worldframe of the vessel in question to transform the wind vector to be relative to the worldframe.
        internal static Matrix4x4 GetRefFrame(Vessel v)
        {
            Matrix4x4 vesselframe = Matrix4x4.identity;
            vesselframe.SetColumn(0, (Vector3)v.north);
            vesselframe.SetColumn(1, (Vector3)v.upAxis);
            vesselframe.SetColumn(2, (Vector3)v.east);
            return vesselframe;
        }

        void OnDestroy()
        {
            Utils.LogInfo("Flight Scene has ended. Unloading CPWE.");
            atmocurrents.Delete();
            Destroy(atmocurrents);
            CPWE_API.core = null;
            instance = null;
            GC.Collect();
            Destroy(this);
        }

        internal Vector3 GetTheWind(CelestialBody body, Part p, Vector3 pos) { return windVec * Utils.GlobalWindSpeedMultiplier; }
        internal Vector3 GetCachedWind() { return windVec; }

        //------------------------------CONFIG INTERPRETERS-------------------------
        /// <summary>
        /// Config node interpreter for all standard wind types. Tries to fetch all possible values that could appear in the corresponding config node, then only inputs the relevant ones to the desired wind object
        /// Values can be inputted as either floats or floatcurves. If float values are inputted, they will be converted to a float curve with a straight line at the given value.
        /// The interpreter will always favor float curves over float values.
        /// </summary>
        /// <param name="cn">The Wind ConfigNode to interpret</param>
        /// <returns>The corresponding Wind object based on the data inside the ConfigNode</returns>
        /// <exception cref="Exception">Throws an exception if the patternType field is empty or is not valid</exception>
        internal Wind ReadWindNode(ConfigNode cn)
        {
            string type = "";
            cn.TryGetValue("patternType", ref type);
            if (string.IsNullOrEmpty(type))
            {
                throw new ArgumentNullException("Wind field 'patternType' cannot be empty.");
            }
            type = type.ToLower();
            float lon = 0.0f;
            float lat = 0.0f;
            float length = 0.0f;
            float minalt = 0.0f;
            float maxalt = 1000000000.0f; //1Gm. If you somehow have an atmosphere taller than this, you need professional help.
            float radius = 0.0f;
            float windSpeed = 0.0f;
            bool saved = false;

            ConfigNode FloatCurveHolder = new ConfigNode(); //used to hold float curve nodes for processing

            cn.TryGetValue("longitude", ref lon);
            cn.TryGetValue("latitude", ref lat);
            cn.TryGetValue("length", ref length);
            cn.TryGetValue("minAlt", ref minalt);
            cn.TryGetValue("maxAlt", ref maxalt);
            cn.TryGetValue("radius", ref radius);
            cn.TryGetValue("windSpeed", ref windSpeed);

            List<float> floats = new List<float> { lon, lat, length, minalt, maxalt, radius, windSpeed };
            if(floats.Any(f => float.IsNaN(f) || float.IsInfinity(f)))
            {
                throw new Exception("One or more of the inputted float fields returned NaN or Infinity.");
            }

            if(minalt >= maxalt) //You do not get to break my mod, boi. >:3
            {
                throw new ArgumentException("Maximum altitude cannot be less than or equal to Minimum altitude.");
            }

            //saved = cn.TryGetNode("RadiusCurve", ref FloatCurveHolder); //Multiplier for the width of the current as a function of longitude/latitude. used by jetstream and polarstream
            FloatCurve RadiusCurve = CheckCurve(FloatCurveHolder, radius, saved);

            //saved = cn.TryGetNode("LongitudeTimeCurve", ref FloatCurveHolder); //longitude of the center of the current as a function of time. used by vortex, up/downdraft, and converging/diverging
            FloatCurve LonTimeCurve = CheckCurve(FloatCurveHolder, lon, saved);

            //saved = cn.TryGetNode("LatitudeTimeCurve", ref FloatCurveHolder); //latitude of the center of the current as a function of time. used by vortex, up/downdraft, and converging/diverging
            FloatCurve LatTimeCurve = CheckCurve(FloatCurveHolder, lat, saved);

            //saved = cn.TryGetNode("WindSpeedTimeCurve", ref FloatCurveHolder); //wind speed as a function of time. used by all wind patterns
            FloatCurve WindSpeedTimeCurve = CheckCurve(FloatCurveHolder, windSpeed, saved);

            saved = cn.TryGetNode("LatitudeCurve", ref FloatCurveHolder); //latitude as a function of longitude. used by jetstream
            FloatCurve LatitudeCurve = CheckCurve(FloatCurveHolder, lat, saved);

            saved = cn.TryGetNode("LongitudeCurve", ref FloatCurveHolder); //longitude as a function of latitude. used by polarstream
            FloatCurve LongitudeCurve = CheckCurve(FloatCurveHolder, lon, saved);

            saved = false;
            //saved = cn.TryGetNode("LonLatSpeedMultiplierCurve", ref FloatCurveHolder); //wind speed multiplier as a function of longitude/latitude. used by jetstream and polarstream
            FloatCurve LonLatSpeedMultCurve = CheckCurve(FloatCurveHolder, 1.0f, saved);

            //saved = cn.TryGetNode("RadiusSpeedMultiplierCurve", ref FloatCurveHolder); //wind speed multiplier as a function of the fraction of the distance to the center of the current (0 = dead center, 1 = edge of current). used by all wind types
            FloatCurve RadiusSpeedMultCurve = CheckCurve(FloatCurveHolder, 1.0f, saved);

            bool altmult = cn.TryGetNode("AltitudeSpeedMultiplierCurve", ref FloatCurveHolder);
            FloatCurve AltitudeSpeedMultCurve = CreateAltitudeCurve(FloatCurveHolder, altmult, minalt, maxalt);

            FloatCurveHolder = null;
            switch (type)
            {
                case "jetstream": //defined by wind that flows primarily in the east/west direction
                    return new JetStream(lon, length, RadiusCurve, LatitudeCurve, WindSpeedTimeCurve, LonLatSpeedMultCurve, RadiusSpeedMultCurve, AltitudeSpeedMultCurve);

                case "polarstream": //defined by wind that flows primarily in the north/south direction
                    return new PolarStream(lat, length,  RadiusCurve, LongitudeCurve, WindSpeedTimeCurve, LonLatSpeedMultCurve, RadiusSpeedMultCurve, AltitudeSpeedMultCurve);

                case "vortex": //defined by a circular wind pattern around the center.
                    return new Vortex(radius, RadiusSpeedMultCurve, LonTimeCurve, LatTimeCurve, WindSpeedTimeCurve, AltitudeSpeedMultCurve);

                case "updraft": //defined by an up/down wind pattern
                    return new Updraft(radius, RadiusSpeedMultCurve, LonTimeCurve, LatTimeCurve, WindSpeedTimeCurve, AltitudeSpeedMultCurve);

                case "downdraft": //alt for updraft
                    return new Updraft(radius, RadiusSpeedMultCurve, LonTimeCurve, LatTimeCurve, WindSpeedTimeCurve, AltitudeSpeedMultCurve);

                case "converging": //defined by winds that converge towards or diverge away from the center
                    return new ConvergingWind(radius, RadiusSpeedMultCurve, LonTimeCurve, LatTimeCurve, WindSpeedTimeCurve, AltitudeSpeedMultCurve);

                case "diverging": //alt for converging
                    return new ConvergingWind(radius, RadiusSpeedMultCurve, LonTimeCurve, LatTimeCurve, WindSpeedTimeCurve, AltitudeSpeedMultCurve);

                default: //defined by the user not entering a valid wind pattern into the "patternType" field and the plugin going "nope"
                    throw new ArgumentException(type + " is not a valid wind pattern.");
            }
        }

        /// <summary>
        /// ConfigNode interpreter for flowmaps. 
        /// </summary>
        /// <param name="cn">The Flowmap ConfigNode to read</param>
        /// <returns>A Flowmap object</returns>
        /// <exception cref="Exception">Throws an exception if no flowmap texture is provided or the flowmap texture does not exist.</exception>
        internal FlowMap ReadFlowMapNode(ConfigNode cn)
        {
            bool thirdchannel = false;
            float minalt = 0.0f; //support wind currents affecting splashed craft
            float maxalt = 1000000000.0f; //1Gm. If you somehow have an atmosphere taller than this, you need professional help.
            float windSpeed = 0.0f;
            float EWwind = 0.0f;
            float NSwind = 0.0f;
            float vWind = 0.0f;
            string map = "";
            bool curveExists = false;

            ConfigNode floaty = new ConfigNode();

            cn.TryGetValue("useThirdChannel", ref thirdchannel);
            cn.TryGetValue("minAlt", ref minalt);
            cn.TryGetValue("maxAlt", ref maxalt);
            cn.TryGetValue("windSpeed", ref windSpeed);
            if (!cn.TryGetValue("eastWestWindSpeed", ref EWwind)) { EWwind = windSpeed; }
            if (!cn.TryGetValue("northSouthWindSpeed", ref NSwind)) { NSwind = windSpeed; }
            if (!cn.TryGetValue("verticalWindSpeed", ref vWind)) { vWind = windSpeed; }
            cn.TryGetValue("map", ref map);

            List<float> floats = new List<float> { minalt, maxalt, windSpeed, EWwind, NSwind, vWind };
            if (floats.Any(f => float.IsNaN(f) || float.IsInfinity(f)))
            {
                throw new Exception("One or more of the inputted float fields returned NaN or Infinity.");
            }
            if (minalt >= maxalt) //You do not get to break my mod, boi. >:3
            {
                throw new ArgumentException("Maximum altitude cannot be less than or equal to Minimum altitude.");
            }
            if (string.IsNullOrEmpty(map))
            {
                throw new ArgumentNullException("Flowmap field 'map' cannot be empty");
            }
            if (!File.Exists(Utils.gameDataPath + map))
            {
                throw new NullReferenceException("Could not locate Flowmap at file path: " + map + " . Verify that the given file path is correct.");
            }
            
            //curveExists = cn.TryGetNode("WindSpeedMultiplierTimeCurve", ref floaty);
            FloatCurve WindSpeedTimeCurve = CheckCurve(floaty, 1.0f, curveExists);

            //curveExists = cn.TryGetNode("NSSpeedTimeCurve", ref floaty);
            FloatCurve EWSpeedTimeCurve = CheckCurve(floaty, EWwind, curveExists);

            //curveExists = cn.TryGetNode("EWSpeedTimeCurve", ref floaty);
            FloatCurve NSSpeedTimeCurve = CheckCurve(floaty, NSwind, curveExists);

            //curveExists = cn.TryGetNode("VerticalSpeedTimeCurve", ref floaty);
            FloatCurve VSpeedTimeCurve = CheckCurve(floaty, vWind, curveExists);

            bool altmult = cn.TryGetNode("AltitudeSpeedMultiplierCurve", ref floaty);
            FloatCurve AltitudeSpeedMultCurve = CreateAltitudeCurve(floaty, altmult, minalt, maxalt);
            
            Texture2D flowmap = LoadTexFromImage(Utils.gameDataPath + map);
            return new FlowMap(WindSpeedTimeCurve, flowmap, AltitudeSpeedMultCurve, thirdchannel, EWSpeedTimeCurve, NSSpeedTimeCurve, VSpeedTimeCurve);
        }

        //Creates the float curve, or if one isnt available, converts a relevant float value into one.
        internal FloatCurve CheckCurve(ConfigNode node, float backup, bool saved)
        {
            FloatCurve curve = new FloatCurve();
            if (saved)
            {
                curve.Load(node);
            }
            else
            {
                curve.Add(-10000, backup, 0, 0);
                curve.Add(0, backup, 0, 0);
                curve.Add(10000, backup, 0, 0);
            }
            return curve;
        }

        internal FloatCurve CreateAltitudeCurve(ConfigNode node, bool saved, float min, float max)
        {
            FloatCurve curve = new FloatCurve();
            if (saved)
            {
                curve.Load(node);
            }
            //generate a default AltitudeSpeedMultCurve if one isn't inputted.
            else
            {
                float fade = Math.Min(1000.0f, (max - min) / 10.0f);
                if(min <= 0.0f)
                {
                    curve.Add(min - fade, 0.0f, 0.0f, 1.0f / fade);
                    curve.Add(min, 1.0f, 1.0f / fade, 0.0f);
                }
                else
                {
                    curve.Add(min, 0.0f, 0.0f, 1.0f / fade);
                    curve.Add(min + fade, 1.0f, 1.0f / fade, 0.0f); 
                }
                curve.Add(max - fade, 1.0f, 0.0f, -1.0f / fade);
                curve.Add(max, 0.0f, -1.0f / fade, 0.0f);
            }
            return curve;
        }

        internal Texture2D LoadTexFromImage(string filePath)
        {
            byte[] fileData;

            if (File.Exists(filePath))
            {
                fileData = File.ReadAllBytes(filePath);
                Texture2D tex = new Texture2D(2, 2);
                ImageConversion.LoadImage(tex, fileData);
                return tex;
            }
            else
            {
                throw new Exception("File at path " + filePath + " not found.");
            }
        }

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
                            if (t.FullName.Equals("FerramAerospaceResearch.FARWind"))
                            {
                                FARAtm = t;
                            }
                            if (t.FullName.Equals("FerramAerospaceResearch.FARWind+WindFunction"))
                            {
                                FARWindFunc = t;
                            }
                            if (t.FullName.Equals("FerramAerospaceResearch.FARAtmosphere"))
                            {
                                FARAtm = t;
                            }
                        }
                    }
                }

                //If no wind or atmosphere cs available return false
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
            catch (Exception e)
            {
                Utils.LogError("An Exception occurred when registering CPWE with FerramAerospaceResearch. Exception thrown: " + e.ToString());
            }
            return false;
        }
    }
}
