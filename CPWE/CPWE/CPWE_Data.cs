using System;
using System.IO;
using UnityEngine;

namespace CPWE
{
    //Data storage class. Handles config interpreting
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class CPWE_Data : MonoBehaviour
    {
        private static CPWE_Data instance;
        public static CPWE_Data Instance => instance;

        private CPWE_Object atmocurrents;

        public CPWE_Data()
        {
            //Check for duplicate instances and destroy any that might be present.
            if (instance == null)
            {
                instance = this;
                Utils.LogInfo("Initializing Core Plugin.");
            }
            else
            {
                Utils.LogWarning("Destroying duplicate instance. Check your install for duplicate mod folders.");
                Destroy(this);
            }
        }

        void Awake()
        {
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
                                try { atmocurrents.AddWind(body, ReadWindNode(wind)); }
                                catch (Exception ex) { Utils.LogWarning("Unable to load Wind object: " + ex.Message); }
                            }
                        }
                        //read the flowmap nodes
                        if (flowmaps.Length > 0)
                        {
                            Utils.LogInfo("Loading Flowmap Objects for " + body);
                            foreach (ConfigNode flowmap in flowmaps)
                            {
                                try { atmocurrents.AddFlowMap(body, ReadFlowMapNode(flowmap)); }
                                catch (Exception ex) { Utils.LogWarning("Unable to load Flowmap object: " + ex.Message); }
                            }
                        }
                    }
                    else { Utils.LogInfo("Body " + body + " not found."); }
                }
            }
            Utils.LogInfo("All Configs Loaded.");
            Utils.CheckSettings();
            DontDestroyOnLoad(this);
        }

        internal bool HasBody(string bodyname) => atmocurrents.HasBody(bodyname);
        internal Vector3 GetWind(string bodyname, double lon, double lat, double alt, double time) => atmocurrents.GetWindVector(bodyname, lon, lat, alt, time);

        //------------------------------CONFIG INTERPRETERS-------------------------
        internal Wind ReadWindNode(ConfigNode cn)
        {
            string type = "";
            cn.TryGetValue("patternType", ref type);
            if (string.IsNullOrEmpty(type)) { throw new ArgumentNullException("Wind field 'patternType' cannot be empty."); }
            type = type.ToLower();

            float lon = 0.0f;
            float lat = 0.0f;
            float length = 0.0f;
            float minalt = 0.0f;
            float maxalt = 1000000000.0f; //1Gm. If you somehow have an atmosphere taller than this, you need professional help.
            float radius = 0.0f;
            float windSpeed = 0.0f;
            bool curveExists;

            ConfigNode FloatCurveHolder = new ConfigNode(); //used to hold float curve nodes for processing

            cn.TryGetValue("longitude", ref lon);
            cn.TryGetValue("latitude", ref lat);
            cn.TryGetValue("length", ref length);
            cn.TryGetValue("minAlt", ref minalt);
            cn.TryGetValue("maxAlt", ref maxalt);
            cn.TryGetValue("radius", ref radius);
            cn.TryGetValue("windSpeed", ref windSpeed);

            //You do not get to break my mod, boi. >:3
            if (minalt >= maxalt) { throw new ArgumentException("maxAlt cannot be less than or equal to minAlt."); }

            float difference = Math.Min(1000.0f, (maxalt - minalt) / 10.0f);
            if (minalt == 0.0f) { minalt -= difference; }
            float lowerfade = minalt + difference;
            float upperfade = maxalt - difference;

            ConfigNode altrange = new ConfigNode();
            if (cn.TryGetNode("AltitudeRange", ref altrange))
            {
                altrange.TryGetValue("startStart", ref minalt);
                altrange.TryGetValue("endEnd", ref maxalt);
                if (minalt >= maxalt) { throw new ArgumentException("Invalid AltitudeRange Node: endEnd cannot be less than or equal to startStart."); }

                //fallback if these things dont get entered
                difference = Math.Min(1000.0f, (maxalt - minalt) / 10.0f);
                if (!altrange.TryGetValue("startEnd", ref lowerfade)) { lowerfade = minalt + difference; }
                if (!altrange.TryGetValue("endStart", ref upperfade)) { upperfade = maxalt - difference; }
            }
            //Clamp startEnd and endStart to prevent weird floatcurve shenanigans
            upperfade = Utils.Clamp(upperfade, minalt + 0.001f, maxalt - 0.001f);
            lowerfade = Utils.Clamp(lowerfade, minalt + 0.001f, upperfade - 0.001f);

            ConfigNode timesettings = new ConfigNode();
            bool timeexists = cn.TryGetNode("TimeSettings", ref timesettings);
            float interval = 20.0f;
            float duration = 10.0f;
            float fadein = 1.0f;
            float fadeout = 1.0f;
            float offset = 0.0f;
            if (timeexists)
            {
                timesettings.TryGetValue("interval", ref interval);
                timesettings.TryGetValue("duration", ref duration);
                timesettings.TryGetValue("fadeIn", ref fadein);
                timesettings.TryGetValue("fadeOut", ref fadeout);
                timesettings.TryGetValue("offset", ref offset);
            }

            curveExists = cn.TryGetNode("LongitudeTimeCurve", ref FloatCurveHolder); //longitude of the center of the current as a function of time. used by vortex, up/downdraft, and converging/diverging
            FloatCurve LonTimeCurve = CheckCurve(FloatCurveHolder, lon, curveExists);

            curveExists = cn.TryGetNode("LatitudeTimeCurve", ref FloatCurveHolder); //latitude of the center of the current as a function of time. used by vortex, up/downdraft, and converging/diverging
            FloatCurve LatTimeCurve = CheckCurve(FloatCurveHolder, lat, curveExists);

            curveExists = cn.TryGetNode("TimeSpeedMultiplierCurve", ref FloatCurveHolder); //wind speed as a function of time. used by all wind patterns
            FloatCurve WindSpeedMultTimeCurve = CreateSpeedTimeCurve(FloatCurveHolder, curveExists, timeexists, interval, duration, fadein, fadeout);

            curveExists = cn.TryGetNode("RadiusCurve", ref FloatCurveHolder); //Width of the current as a function of longitude/latitude. used by jetstream and polarstream
            FloatCurve RadiusCurve = CheckCurve(FloatCurveHolder, radius, curveExists);

            curveExists = cn.TryGetNode("LatitudeCurve", ref FloatCurveHolder); //latitude as a function of longitude. used by jetstream
            FloatCurve LatitudeCurve = CheckCurve(FloatCurveHolder, lat, curveExists);

            curveExists = cn.TryGetNode("LongitudeCurve", ref FloatCurveHolder); //longitude as a function of latitude. used by polarstream
            FloatCurve LongitudeCurve = CheckCurve(FloatCurveHolder, lon, curveExists);

            curveExists = cn.TryGetNode("LonLatSpeedMultiplierCurve", ref FloatCurveHolder); //wind speed multiplier as a function of longitude/latitude. used by jetstream and polarstream
            FloatCurve LonLatSpeedMultCurve = CheckCurve(FloatCurveHolder, 1.0f, curveExists);

            curveExists = cn.TryGetNode("RadiusSpeedMultiplierCurve", ref FloatCurveHolder); //wind speed multiplier as a function of the fraction of the distance to the center of the current (0 = dead center, 1 = edge of current). used by all wind types
            FloatCurve RadiusSpeedMultCurve = CreateRadiusSpeedCurve(FloatCurveHolder, curveExists);

            curveExists = cn.TryGetNode("AltitudeSpeedMultiplierCurve", ref FloatCurveHolder);
            FloatCurve AltitudeSpeedMultCurve = CreateAltitudeCurve(FloatCurveHolder, curveExists, minalt, maxalt, lowerfade, upperfade);

            switch (type)
            {
                case "jetstream": //defined by wind that flows primarily in the east/west direction
                    return new JetStream(windSpeed, lon, length, RadiusCurve, LatitudeCurve, WindSpeedMultTimeCurve, LonLatSpeedMultCurve, RadiusSpeedMultCurve, AltitudeSpeedMultCurve, offset);

                case "polarstream": //defined by wind that flows primarily in the north/south direction
                    return new PolarStream(windSpeed, lat, length, RadiusCurve, LongitudeCurve, WindSpeedMultTimeCurve, LonLatSpeedMultCurve, RadiusSpeedMultCurve, AltitudeSpeedMultCurve, offset);

                case "vortex": //defined by a circular wind pattern around the center.
                    return new Vortex(windSpeed, radius, RadiusSpeedMultCurve, LonTimeCurve, LatTimeCurve, WindSpeedMultTimeCurve, AltitudeSpeedMultCurve, offset);

                case "updraft": //defined by an up/down wind pattern
                    return new Updraft(windSpeed, radius, RadiusSpeedMultCurve, LonTimeCurve, LatTimeCurve, WindSpeedMultTimeCurve, AltitudeSpeedMultCurve, offset);

                case "downdraft": //alt for updraft
                    return new Updraft(windSpeed, radius, RadiusSpeedMultCurve, LonTimeCurve, LatTimeCurve, WindSpeedMultTimeCurve, AltitudeSpeedMultCurve, offset);

                case "converging": //defined by winds that converge towards or diverge away from the center
                    return new ConvergingWind(windSpeed, radius, RadiusSpeedMultCurve, LonTimeCurve, LatTimeCurve, WindSpeedMultTimeCurve, AltitudeSpeedMultCurve, offset);

                case "diverging": //alt for converging
                    return new ConvergingWind(windSpeed, radius, RadiusSpeedMultCurve, LonTimeCurve, LatTimeCurve, WindSpeedMultTimeCurve, AltitudeSpeedMultCurve, offset);

                default: //defined by the user not entering a valid wind pattern into the "patternType" field and the plugin going "nope"
                    throw new ArgumentException(type + " is not a valid wind pattern.");
            }
        }

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
            bool curveExists;

            ConfigNode floaty = new ConfigNode();

            cn.TryGetValue("useThirdChannel", ref thirdchannel);
            cn.TryGetValue("minAlt", ref minalt);
            cn.TryGetValue("maxAlt", ref maxalt);
            cn.TryGetValue("windSpeed", ref windSpeed);
            if (!cn.TryGetValue("eastWestWindSpeed", ref EWwind)) { EWwind = windSpeed; }
            if (!cn.TryGetValue("northSouthWindSpeed", ref NSwind)) { NSwind = windSpeed; }
            if (!cn.TryGetValue("verticalWindSpeed", ref vWind)) { vWind = windSpeed; }
            cn.TryGetValue("map", ref map);

            if (string.IsNullOrEmpty(map)) { throw new ArgumentNullException("Flowmap field 'map' cannot be empty"); }
            if (!File.Exists(Utils.GameDataPath + map)) { throw new NullReferenceException("Could not locate Flowmap at file path: " + map + " . Verify that the given file path is correct."); }

            //You do not get to break my mod, boi. >:3
            if (minalt >= maxalt) { throw new ArgumentException("maxAlt cannot be less than or equal to minAlt."); }

            float difference = Math.Min(1000.0f, (maxalt - minalt) / 10.0f);
            if (minalt == 0.0f) { minalt -= difference; }
            float lowerfade = minalt + difference;
            float upperfade = maxalt - difference;

            ConfigNode altrange = new ConfigNode();
            if (cn.TryGetNode("AltitudeRange", ref altrange))
            {
                altrange.TryGetValue("startStart", ref minalt);
                altrange.TryGetValue("endEnd", ref maxalt);
                if (minalt >= maxalt) { throw new ArgumentException("Invalid AltitudeRange Node: endEnd cannot be less than or equal to startStart."); }

                //fallback if these things dont get entered
                difference = Math.Min(1000.0f, (maxalt - minalt) / 10.0f);
                if (!altrange.TryGetValue("startEnd", ref lowerfade)) { lowerfade = minalt + difference; }
                if (!altrange.TryGetValue("endStart", ref upperfade)) { upperfade = maxalt - difference; }
            }
            //Clamp startEnd and endStart to prevent weird floatcurve shenanigans
            upperfade = Utils.Clamp(upperfade, minalt + 0.001f, maxalt - 0.001f);
            lowerfade = Utils.Clamp(lowerfade, minalt + 0.001f, upperfade - 0.001f);

            ConfigNode timesettings = new ConfigNode();
            bool timeexists = cn.TryGetNode("TimeSettings", ref timesettings);
            float interval = 1000.0f;
            float duration = 500.0f;
            float fadein = 50.0f;
            float fadeout = 50.0f;
            float offset = 0.0f;
            if (timeexists)
            {
                timesettings.TryGetValue("interval", ref interval);
                timesettings.TryGetValue("duration", ref duration);
                timesettings.TryGetValue("fadeIn", ref fadein);
                timesettings.TryGetValue("fadeOut", ref fadeout);
                timesettings.TryGetValue("offset", ref offset);
            }

            curveExists = cn.TryGetNode("TimeSpeedMultiplierCurve", ref floaty);
            FloatCurve WindSpeedMultTimeCurve = CreateSpeedTimeCurve(floaty, curveExists, timeexists, interval, duration, fadein, fadeout);

            curveExists = cn.TryGetNode("AltitudeSpeedMultiplierCurve", ref floaty);
            FloatCurve AltitudeSpeedMultCurve = CreateAltitudeCurve(floaty, curveExists, minalt, maxalt, lowerfade, upperfade);

            curveExists = cn.TryGetNode("EastWestAltitudeSpeedMultiplierCurve", ref floaty);
            FloatCurve EWAltMult = CheckCurve(floaty, 1.0f, curveExists);

            curveExists = cn.TryGetNode("NorthSouthAltitudeSpeedMultiplierCurve", ref floaty);
            FloatCurve NSAltMult = CheckCurve(floaty, 1.0f, curveExists);

            curveExists = cn.TryGetNode("VerticalAltitudeSpeedMultiplierCurve", ref floaty);
            FloatCurve VertAltMult = CheckCurve(floaty, 1.0f, curveExists);

            Texture2D flowmap = LoadTexFromImage(Utils.GameDataPath + map);
            return new FlowMap(flowmap, thirdchannel, AltitudeSpeedMultCurve, EWAltMult, NSAltMult, VertAltMult, EWwind, NSwind, vWind, WindSpeedMultTimeCurve, offset);
        }

        //Creates the float curve, or if one isnt available, converts a relevant float value into one.
        internal FloatCurve CheckCurve(ConfigNode node, float backup, bool saved)
        {
            FloatCurve curve = new FloatCurve();
            if (saved) { curve.Load(node); }
            else
            {
                curve.Add(0, backup, 0, 0);
                curve.Add(10000, backup, 0, 0);
            }
            return curve;
        }

        internal FloatCurve CreateAltitudeCurve(ConfigNode node, bool saved, float min, float max, float lowerfade, float upperfade)
        {
            FloatCurve curve = new FloatCurve();
            if (saved) { curve.Load(node); }
            //generate a default AltitudeSpeedMultCurve with the inputted fade information.
            else
            {
                curve.Add(min, 0.0f, 0.0f, 1.0f / (lowerfade - min));
                curve.Add(min + lowerfade, 1.0f, 1.0f / (lowerfade - min), 0.0f);
                curve.Add(max - upperfade, 1.0f, 0.0f, -1.0f / (max - upperfade));
                curve.Add(max, 0.0f, -1.0f / (max - upperfade), 0.0f);
            }
            return curve;
        }

        internal FloatCurve CreateRadiusSpeedCurve(ConfigNode node, bool saved)
        {
            FloatCurve curve = new FloatCurve();
            if (saved) { curve.Load(node); }
            //generate a default radius speed multiplier curve if one is not inputted
            else
            {
                curve.Add(0.0f, 1.0f, 0.0f, 0.0f);
                curve.Add(0.8f, 1.0f, 0.0f, -5.0f); //fade to 0 in the outer 20% of the curve
                curve.Add(1.0f, 0.0f, -5.0f, 0.0f);
            }
            return curve;
        }

        internal FloatCurve CreateSpeedTimeCurve(ConfigNode node, bool curveexists, bool nodeexists, float interval, float duration, float fadein, float fadeout)
        {
            FloatCurve curve = new FloatCurve();
            if (curveexists) { curve.Load(node); }
            else if (nodeexists)
            {
                curve.Add(0.0f, 0.0f, 0.0f, 1 / fadein);
                curve.Add(fadein, 1.0f, 1 / fadein, 0.0f);
                curve.Add(duration - fadeout, 1.0f, 0.0f, -1.0f / fadeout);
                curve.Add(duration, 0.0f, -1.0f / fadeout, 0.0f);
                curve.Add(interval, 0.0f, 0.0f, 0.0f);
            }
            else
            {
                curve.Add(0, 1.0f, 0, 0);
                curve.Add(1000, 1.0f, 0, 0);
            }
            return curve;
        }

        internal Texture2D LoadTexFromImage(string filePath)
        {
            if (File.Exists(filePath))
            {
                byte[] fileData = File.ReadAllBytes(filePath);
                Texture2D tex = new Texture2D(2, 2);
                ImageConversion.LoadImage(tex, fileData);
                return tex;
            }
            else { throw new Exception("File at path " + filePath + " not found."); }
        }
    }
}
