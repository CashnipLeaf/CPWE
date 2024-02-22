using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;
using KSP.UI.Screens;
using ToolbarControl_NS;

namespace CPWE
{
    //Runs the GUI
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    [DefaultExecutionOrder(10)] 
    public class CPWE_UnityGUI : MonoBehaviour
    {
        private static CPWE_UnityGUI instance;
        public static CPWE_UnityGUI Instance { get { return instance; } }

        private ToolbarControl toolbarController;
        private bool toolbarButtonAdded = false;
        private bool GUIEnabled = false;

        internal CPWE_Core Core;
        private Vector3 internalwindvec = Vector3.zero;
        private Vector3 appliedwindvec = Vector3.zero;
        private Vector3 multipliedwindvec = Vector3.zero;
        private Vector3 finalwindvec = Vector3.zero; //the actual wind vector being applied to the craft, after being multiplied by the wind speed multiplier.
        private bool haswind = false;

        private Vector3 craftdragvectorwind = Vector3.zero;
        private Vector3 craftdragvector = Vector3.zero;
        private Vector3 craftdragvectortransformedwind = Vector3.zero;
        private Vector3 craftdragvectortransformed = Vector3.zero;

        private Matrix4x4 worldframe;
        private Matrix4x4 inverseworldframe;

        internal const string modNAME = "CPWE";
        internal const string modID = "CPWE_NS";

        private CelestialBody mainbody;
        private Vessel activevessel;
        private Part refpart;

        private static float xpos = 100f;
        private static float ypos = 100f;
        private static float xwidth = 285f;
        private static float yheight = 60.0f;
        //private Rect titleRect = new Rect(0, 0, 10000, 10000);
        private Rect windowPos = new Rect(xpos, ypos, xwidth, yheight);
        //private Rect wpos;

        private string altitude;
        //private string IAS;
        private string TAS;
        private string groundspeed;
        private string mach;

        private string windspeed;
        private string h_windspeed;
        private string v_windspeed;
        private double heading;
        private string windheading;
        private string winddirection;

        private static string distunit;
        private static string speedunit;
        private const string degreesstr = "°";
        private const string minutesstr = "'";
        private const string secondsstr = "″";
        private static string[] cardinaldirs = { "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW" };

        //cache for localization tags
        private Dictionary<string, string> LOCCache;

        public CPWE_UnityGUI() //Check for duplicate instances and destroy any that might be present.
        {
            if (instance == null)
            {
                Utils.LogInfo("Initializing GUI.");
                instance = this;
            }
            else { Destroy(this); }
        }

        void Start()
        {
            Core = CPWE_Core.Instance;
            if (Utils.devMode) { xwidth = 345f; }
            UpdateUISizes();
            AddToolbarButton();
            LOCCache = new Dictionary<string, string>();
            CacheLOC();

            speedunit = GetLOC("#LOC_CPWE_meterspersec");
            distunit = GetLOC("#LOC_CPWE_meter");
        }

        void FixedUpdate()
        {
            refpart = null;
            activevessel = null;
            if (!FlightGlobals.ready) { return; }
            if (FlightGlobals.ActiveVessel == null) {  return; }
            if (Core == null)
            {
                if (CPWE_Core.Instance == null) { return; }
                Core = CPWE_Core.Instance;
            }
            activevessel = FlightGlobals.ActiveVessel;

            //get the first part with a rigidbody (this is almost always the root part, but it never hurts to check)
            foreach (Part p in activevessel.Parts)
            {
                if (p.rb)
                {
                    refpart = p;
                    goto CacheStuff;
                }
            }
            return;

        CacheStuff:
            mainbody = activevessel.mainBody;
            worldframe = CPWE_Core.GetRefFrame(activevessel);
            inverseworldframe = worldframe.inverse;
            
            haswind = Core.haswind;
            appliedwindvec = Core.GetCachedWind();
            internalwindvec = inverseworldframe * appliedwindvec;
            multipliedwindvec = internalwindvec * Utils.GlobalWindSpeedMultiplier;
            finalwindvec = appliedwindvec * Utils.GlobalWindSpeedMultiplier;

            craftdragvectorwind = refpart.dragVector;
            craftdragvector = craftdragvectorwind + finalwindvec;
            craftdragvectortransformedwind = inverseworldframe * craftdragvectorwind;
            craftdragvectortransformed = inverseworldframe * craftdragvector;
        }

        //clear from memory
        void OnDestroy()
        {
            LOCCache.Clear();
            LOCCache = null;
            instance = null;
            Destroy();
            Destroy(this);
        }

        void OnGUI()
        {
            if (GUIEnabled) { windowPos = GUILayout.Window("CPWE".GetHashCode(), windowPos, DrawWindow, "CPWE: " + Utils.version); }
        }

        void DrawWindow(int windowID)
        {
            altitude = string.Format("{0:F1} {1}", activevessel.altitude, Localizer.Format(distunit));
            double grndspd = Math.Sqrt(Math.Pow(craftdragvectortransformed.x, 2) + Math.Pow(craftdragvectortransformed.z, 2));

            groundspeed = string.Format("{0:F1} {1}", grndspd, Localizer.Format(speedunit));
            //IAS = string.Format("{0:F1} {1}", craftdragvectorwind.magnitude / rootpart.staticPressureAtm, Localizer.Format(speedunit));
            TAS = string.Format("{0:F1} {1}", craftdragvectorwind.magnitude, Localizer.Format(speedunit));
            mach = string.Format("{0:F2}", activevessel.mach);

            windspeed = string.Format("{0:F1} {1}", multipliedwindvec.magnitude, Localizer.Format(speedunit));
            v_windspeed = string.Format("{0:F1} {1}", multipliedwindvec.y, Localizer.Format(speedunit));
            h_windspeed = string.Format("{0:F1} {1}", Math.Sqrt(Math.Pow(multipliedwindvec.x,2) + Math.Pow(multipliedwindvec.z, 2)), Localizer.Format(speedunit));

            if (multipliedwindvec.x == 0.0 && multipliedwindvec.z == 0.0)
            {
                windheading = GetLOC("#LOC_CPWE_na");
                winddirection = GetLOC("#LOC_CPWE_na");
            }
            else
            {
                heading = ((Math.Atan2(multipliedwindvec.z, multipliedwindvec.x) * Utils.radtodeg) + 180.0) % 360.0;
                windheading = string.Format("{0:F1} {1}", heading, Localizer.Format(degreesstr));
                winddirection = CardinalDirection(heading);
            }            

            GUILayout.BeginVertical();

            //Craft Information
            DrawHeader(GetLOC("#LOC_CPWE_grdtrk"));
            DrawElement(GetLOC("#LOC_CPWE_body"), Localizer.Format(mainbody.displayName.Split('^')[0]));
            DrawElement(GetLOC("#LOC_CPWE_lon"), DegreesString(activevessel.longitude, 1)); //east/west
            DrawElement(GetLOC("#LOC_CPWE_lat"), DegreesString(activevessel.latitude, 0)); //north/south
            DrawElement(GetLOC("#LOC_CPWE_alt"), altitude);

            //Velocity Information
            DrawHeader(GetLOC("#LOC_CPWE_vel"));
            //DrawElement(GetLOC("#LOC_CPWE_ias"), IAS);
            DrawElement(GetLOC("#LOC_CPWE_tas"), TAS);
            DrawElement(GetLOC("#LOC_CPWE_gs"), groundspeed);
            DrawElement(GetLOC("#LOC_CPWE_mach"), mach);

            //Wind Information
            DrawHeader(GetLOC("#LOC_CPWE_windinfo"));

            if (mainbody.atmosphere)
            {
                if (refpart.staticPressureAtm > 0.0)
                {
                    if (haswind)
                    {
                        DrawElement(GetLOC("#LOC_CPWE_windspd"), windspeed);
                        DrawElement(GetLOC("#LOC_CPWE_windvert"), v_windspeed);
                        DrawElement(GetLOC("#LOC_CPWE_windhoriz"), h_windspeed);
                        DrawElement(GetLOC("#LOC_CPWE_heading"), windheading);
                        DrawElement(GetLOC("#LOC_CPWE_cardinal"), winddirection);
                    }
                    else
                    {
                        DrawCentered(GetLOC("#LOC_CPWE_nowind"));
                    }
                }
                else
                {
                    DrawCentered(GetLOC("#LOC_CPWE_outatmo"));
                }
            }
            else
            {
                DrawCentered(GetLOC("#LOC_CPWE_noatmo"));
            }
            GUILayout.FlexibleSpace();

            if (Utils.devMode)
            {
                DrawHeader("Developer Mode Information");
                DrawElement("Wind Data Source", Core.source); //Data source
                DrawElement("Connected to FAR", Utils.FARConnected.ToString()); //connected to FAR
                DrawElement("Body Internal Name", mainbody.name); //internal name of the current celestial body
                DrawElement("Wind Speed Multiplier", string.Format("{0:F2}", Utils.GlobalWindSpeedMultiplier));
                DrawElement("Wind Vector (Vessel)", internalwindvec.ToString()); //wind vector retrieved from the wind objects
                DrawElement("Wind Vector (World)", appliedwindvec.ToString()); //wind vector after being transformed relative to the craft's frame of reference
                DrawElement("Wind Vector (Applied)", finalwindvec.ToString()); //wind vector after being multiplied by the wind speed multiplier
                DrawElement("Active Vessel", activevessel.GetDisplayName());
                DrawElement("World Position", activevessel.GetWorldPos3D().ToString("F1"));
                DrawElement("Drag Vector (World)", craftdragvector.ToString());
                DrawElement("Drag Vector (Vessel)", craftdragvectortransformed.ToString());
                DrawElement("Drag Vector + Wind (World)", craftdragvectorwind.ToString());
                DrawElement("Drag Vector + Wind (Vessel)", craftdragvectortransformedwind.ToString());
                DrawElement("Universal Time", string.Format("{0:F1}", Planetarium.GetUniversalTime()));
            }
            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        internal void UpdateUISizes()
        {
            xwidth *= Math.Min(GameSettings.UI_SCALE, 1.5f);
            yheight *= GameSettings.UI_SCALE;
            xpos *= GameSettings.UI_SCALE;
            ypos *= GameSettings.UI_SCALE;
            //titleRect = new Rect(0, 0, 10000 * (int)GameSettings.UI_SCALE, 10000 * (int)GameSettings.UI_SCALE);
            windowPos = new Rect(xpos, ypos, xwidth, yheight);
        }

        //GUILayout functions to avoid being a WET boi
        private void DrawHeader(string tag)
        {
            GUILayout.BeginHorizontal();
            GUI.skin.label.margin = new RectOffset(5, 5, 5, 5);
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUILayout.Label(tag);
            GUI.skin.label.fontStyle = FontStyle.Normal;
            GUILayout.EndHorizontal();
            GUI.skin.label.margin = new RectOffset(2, 2, 2, 2);
        }

        private void DrawElement(string tag, string value)
        {
            GUILayout.BeginHorizontal();
            GUI.skin.label.alignment = TextAnchor.MiddleLeft;
            GUILayout.Label(tag);
            GUI.skin.label.alignment = TextAnchor.MiddleRight;
            GUILayout.Label(value);
            GUILayout.EndHorizontal();
        }      

        private void DrawCentered(string tag)
        {
            GUILayout.BeginHorizontal();
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUILayout.Label(tag);
            GUILayout.EndHorizontal();
        }

        //add to toolbar
        private void AddToolbarButton()
        {
            ApplicationLauncher.AppScenes scenes = ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.MAPVIEW;
            toolbarController = gameObject.AddComponent<ToolbarControl>();
            if (!toolbarButtonAdded)
            {
                toolbarController.AddToAllToolbars(ToolbarButtonOnTrue, ToolbarButtonOnFalse, scenes, modID, "991291", DebugPath(), DebugPath(), Localizer.Format(modNAME));
                toolbarButtonAdded = true;
            }
        }

        //Remove from toolbar
        private void RemoveToolbarButton()
        {
            if (toolbarButtonAdded)
            {
                toolbarController.OnDestroy();
                Destroy(toolbarController);
                toolbarButtonAdded = false;
            }
        }

        private void ToolbarButtonOnTrue() => GUIEnabled = true; 
        private void ToolbarButtonOnFalse() => GUIEnabled = false;

        //cache localization tags
        private void CacheLOC()
        {
            IEnumerator tags = Localizer.Tags.Keys.GetEnumerator();
            string tag;
            while (tags.MoveNext())
            {
                if(tags.Current == null) { continue; }
                tag = tags.Current.ToString();
                if (tag.Contains("#LOC_CPWE_"))
                {
                    LOCCache.Add(tag, Localizer.GetStringByTag(tag).Replace("\\n", "\n"));
                }
            }
        }

        //retrieve the localization tag.
        internal string GetLOC(string name) => LOCCache.ContainsKey(name) ? LOCCache[name] : name;

        //if developer mode is enabled, a modified green logo will replace the normal white Logo.
        internal static string DebugPath() => Utils.devMode ? "CPWE/PluginData/CPWE_Debug" : "CPWE/PluginData/CPWE_Logo";

        //display the longitude and latitude information as either degrees or degrees, minutes, and seconds + direction
        internal static string DegreesString(double deg, int axis)
        {
            if (Utils.minutesforcoords)
            {
                string[] directions = { "N", "S", "E", "W" };
                int direction = (deg < 0.0) ? (2 * axis) + 1 : 2 * axis;
                double minutes = (deg % 1) * 60.0;
                double seconds = ((deg % 1) * 3600.0) % 60;
                string degs = string.Format("{0:F0}{1}", Math.Floor(Math.Abs(deg)), Localizer.Format(degreesstr));
                string mins = string.Format("{0:F0}{1}", Math.Floor(Math.Abs(minutes)), Localizer.Format(minutesstr));
                string secs = string.Format("{0:F0}{1}", Math.Floor(Math.Abs(seconds)), Localizer.Format(secondsstr));
                return degs + " " + mins + " " + secs + " " + directions[direction];
            }
            return string.Format("{0:F2}{1}", deg, Localizer.Format(degreesstr));
        }

        internal static string CardinalDirection(double heading)
        {
            int val = (int)((heading / 22.5) + .5);
            return cardinaldirs[val % 16];
        }

        void Destroy()
        {
            RemoveToolbarButton();
            GameEvents.onGUIApplicationLauncherDestroyed.Remove(RemoveToolbarButton);
        }
    }
}
