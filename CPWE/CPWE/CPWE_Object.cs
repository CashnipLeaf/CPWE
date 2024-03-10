using System;
using System.Collections.Generic;
using UnityEngine;

namespace CPWE
{ 
    //the object used to store wind pattern data
    internal class CPWE_Object
    {
        private readonly Dictionary<string, CPWE_Body> Bodies;
        internal CPWE_Object()
        {
            Bodies = new Dictionary<string, CPWE_Body>();
        }

        //add a new body to the list if said body hasn't already been added. Prevents duplicate keys from being entered.
        internal void AddBody(string body, double scalefactor)
        {
            if (!HasBody(body)) { Bodies.Add(body, new CPWE_Body(body, scalefactor)); }
        }

        internal bool HasBody(string bod) => Bodies.ContainsKey(bod);

        //X = North, Y = Up, Z = East
        internal Vector3 GetWindVector(string body, double vlon, double vlat, double vheight, double time) => HasBody(body) ? Bodies[body].GetWind(vlon, vlat, vheight, time) : Vector3.zero;

        internal void AddWind(string body, Wind wnd)
        {
            //check that the CPWE_Body object is present before adding a wind object
            if (HasBody(body)) { Bodies[body].AddNewWind(wnd); }
        }
        internal void AddFlowMap(string body, FlowMap flmp)
        {
            //check that the CPWE_Body object is present before adding a flowmap object
            if (HasBody(body)) { Bodies[body].AddFlowMap(flmp); }
        }

        //Remove from memory
        internal void Delete()
        {
            foreach (KeyValuePair<string, CPWE_Body> de in Bodies) { Bodies[de.Key].Delete(); }
            Bodies.Clear();
        }
    }

    //stores the information for each body
    internal class CPWE_Body
    {
        private List<Wind> winds;
        private List<FlowMap> flowmaps;
        private readonly string body;

        private double scalefactor;
        private double ScaleFactor
        {
            get => scalefactor;
            set => scalefactor = value <= 0.0 ? 1.0 : value;
        }

        internal CPWE_Body(string body, double scale)
        {
            this.body = body;
            winds = new List<Wind>();
            flowmaps = new List<FlowMap>();
            ScaleFactor = scale;
        }

        internal void AddNewWind(Wind wnd) => winds.Add(wnd);
        internal void AddFlowMap(FlowMap flmp) => flowmaps.Add(flmp);

        internal Vector3 GetWind(double lon, double lat, double alt, double time)
        {
            Vector3 windvec = Vector3.zero;
            alt /= ScaleFactor;
            foreach (Wind w in winds) 
            {
                windvec += w.GetWindVec(lon, lat, alt, time);
            }
            foreach (FlowMap flmp in flowmaps) 
            {
                windvec += flmp.GetWindVec(lon, lat, alt, time);
            }
            return windvec;
        }

        internal void Delete(){ //clear from memory
            winds.Clear();
            winds = null;

            foreach (FlowMap flmp in flowmaps) { flmp.Delete(); }

            flowmaps.Clear();
            flowmaps = null;
        }
    }

    internal abstract class Wind //basic structure for wind objects
    {
        internal float radius;
        internal float windSpeed;
        internal FloatCurve RadiusSpeedMultCurve;
        internal FloatCurve WindSpeedTimeMultCurve;
        internal FloatCurve AltitudeSpeedMultCurve;

        internal float timeoffset;
        internal float TimeOffset
        {
            get => timeoffset;
            set => timeoffset = WindSpeedTimeMultCurve.maxTime != 0.0f ? value % WindSpeedTimeMultCurve.maxTime : 0.0f;
        }

        internal abstract Vector3 GetWindVec(double lon, double lat, double alt, double time);
    }

    internal class JetStream : Wind
    {
        internal float longitude;
        internal float length;
        internal FloatCurve RadiusCurve;
        internal FloatCurve LatitudeCurve;
        internal FloatCurve LonLatSpeedMultCurve;

        internal JetStream(float speed, float lon, float len, FloatCurve radcurve, FloatCurve latcurve, FloatCurve speedCurve, FloatCurve lonlatmult, FloatCurve radmult, FloatCurve altmult, float offset)
        {
            windSpeed = speed;
            longitude = lon;
            length = len;
            RadiusCurve = radcurve;
            LatitudeCurve = latcurve;
            LonLatSpeedMultCurve = lonlatmult;
            WindSpeedTimeMultCurve = speedCurve;
            RadiusSpeedMultCurve = radmult;
            AltitudeSpeedMultCurve = altmult;
            TimeOffset = offset;
        }

        internal override Vector3 GetWindVec(double lon, double lat, double alt, double time)
        {
            double latAtLon = (double)LatitudeCurve.Evaluate((float)lon);
            double radAtLon = Math.Max(RadiusCurve.Evaluate((float)lon), 0.0f);

            double distfraction = Utils.Clamp(Utils.GreatCircleAngle(lon, lat, lon, latAtLon) / radAtLon, 0.0, double.MaxValue);
            float speedmult = Utils.GetValAtLoopTime(WindSpeedTimeMultCurve, time - timeoffset) * RadiusSpeedMultCurve.Evaluate((float)distfraction) * AltitudeSpeedMultCurve.Evaluate((float)alt) * LonLatSpeedMultCurve.Evaluate((float)lon);
            if (distfraction < 1.0 && (length == 0.0f || Utils.InRange(lat, longitude, longitude + length)) && speedmult != 0.0f)
            {
                Vector3 windvec = new Vector3(Utils.FloatCurveDerivative(LatitudeCurve, (float)lat), 0.0f, 1.0f);
                windvec.Normalize();
                return windvec * speedmult * windSpeed;
            }
            return Vector3.zero;
        }
    }

    internal class PolarStream : Wind
    {
        internal float latitude;
        internal float length;
        internal FloatCurve RadiusCurve;
        internal FloatCurve LongitudeCurve;
        internal FloatCurve LonLatSpeedMultCurve;

        internal PolarStream(float speed, float lat, float len, FloatCurve radcurve, FloatCurve loncurve, FloatCurve speedCurve, FloatCurve lonlatmult, FloatCurve radmult, FloatCurve altmult, float offset)
        {
            windSpeed = speed;
            latitude = lat;
            length = len;
            RadiusCurve = radcurve;
            LongitudeCurve = loncurve;
            LonLatSpeedMultCurve = lonlatmult;
            WindSpeedTimeMultCurve = speedCurve;
            RadiusSpeedMultCurve = radmult;
            AltitudeSpeedMultCurve = altmult;
            TimeOffset = offset;
        }

        internal override Vector3 GetWindVec(double lon, double lat, double alt, double time)
        {
            double lonAtLat = (double)LongitudeCurve.Evaluate((float)lat);
            double radAtLat = Math.Max(RadiusCurve.Evaluate((float)lat), 0.0f);
            double distfraction = Utils.Clamp(Utils.GreatCircleAngle(lon, lat, lonAtLat, lat) / radAtLat, 0.0, double.MaxValue);
            float speedmult = Utils.GetValAtLoopTime(WindSpeedTimeMultCurve, time - timeoffset) * RadiusSpeedMultCurve.Evaluate((float)distfraction) * AltitudeSpeedMultCurve.Evaluate((float)alt) * LonLatSpeedMultCurve.Evaluate((float)lon);
            if (distfraction < 1.0 && (length == 0.0f || Utils.InRange(lat, latitude, latitude + length)) && speedmult != 0.0f)
            {
                Vector3 windvec = new Vector3(1.0f, 0.0f, Utils.FloatCurveDerivative(LongitudeCurve, (float)lat));
                windvec.Normalize();
                return windvec * speedmult * windSpeed;
            }
            return Vector3.zero;
        }
    }

    internal class Vortex : Wind
    {
        internal FloatCurve LonTimeCurve;
        internal FloatCurve LatTimeCurve;

        internal Vortex(float speed, float rad, FloatCurve radspdcurve, FloatCurve lontime, FloatCurve lattime, FloatCurve speedtime, FloatCurve altmult, float offset)
        {
            windSpeed = speed;
            radius = rad;
            RadiusSpeedMultCurve = radspdcurve;
            LonTimeCurve = lontime;
            LatTimeCurve = lattime;
            WindSpeedTimeMultCurve = speedtime;
            AltitudeSpeedMultCurve = altmult;
            TimeOffset = offset;
        }

        internal override Vector3 GetWindVec(double lon, double lat, double alt, double time)
        {
            double lonAtTime = Utils.GetValAtLoopTime(LonTimeCurve, time);
            double latAtTime = Utils.GetValAtLoopTime(LatTimeCurve, time);
            double distfraction = Utils.Clamp(Utils.GreatCircleAngle(lon, lat, lonAtTime, latAtTime) / radius, 0.0, double.MaxValue);
            float speedmult = Utils.GetValAtLoopTime(WindSpeedTimeMultCurve, time - timeoffset) * RadiusSpeedMultCurve.Evaluate((float)distfraction) * AltitudeSpeedMultCurve.Evaluate((float)alt);
            if (distfraction < 1.0 && speedmult != 0.0f)
            {
                double heading = Utils.RelativeHeading(lon, lat, lonAtTime, latAtTime, true);
                return new Vector3((float)Utils.FastTrig.Cos(heading), 0.0f, (float)Utils.FastTrig.Sin(heading)) * windSpeed * speedmult;
            }
            return Vector3.zero;
        }
    }
    
    internal class Updraft : Wind
    {
        internal FloatCurve LonTimeCurve;
        internal FloatCurve LatTimeCurve;

        internal Updraft(float speed, float rad, FloatCurve radspdcurve, FloatCurve lontime, FloatCurve lattime, FloatCurve speedtime, FloatCurve altmult, float offset)
        {
            windSpeed = speed;
            radius = rad;
            RadiusSpeedMultCurve = radspdcurve;
            LonTimeCurve = lontime;
            LatTimeCurve = lattime;
            WindSpeedTimeMultCurve = speedtime;
            AltitudeSpeedMultCurve = altmult;
            TimeOffset = offset;
        }

        internal override Vector3 GetWindVec(double lon, double lat, double alt, double time)
        {
            double lonAtTime = Utils.GetValAtLoopTime(LonTimeCurve, time);
            double latAtTime = Utils.GetValAtLoopTime(LatTimeCurve, time);
            double distfraction = Utils.Clamp(Utils.GreatCircleAngle(lon, lat, lonAtTime, latAtTime) / radius, 0.0, double.MaxValue);
            float speed = Utils.GetValAtLoopTime(WindSpeedTimeMultCurve, time - timeoffset) * RadiusSpeedMultCurve.Evaluate((float)distfraction) * AltitudeSpeedMultCurve.Evaluate((float)alt) * windSpeed;
            return (distfraction < 1.0 && speed != 0.0f) ? new Vector3(0.0f, speed, 0.0f) : Vector3.zero;
        }
    }

    internal class ConvergingWind : Wind
    {
        internal FloatCurve LonTimeCurve;
        internal FloatCurve LatTimeCurve;

        internal ConvergingWind(float speed, float rad, FloatCurve radspdcurve, FloatCurve lontime, FloatCurve lattime, FloatCurve speedtime, FloatCurve altmult, float offset)
        {
            windSpeed = speed;
            radius = rad;
            RadiusSpeedMultCurve = radspdcurve;
            LonTimeCurve = lontime;
            LatTimeCurve = lattime;
            WindSpeedTimeMultCurve = speedtime;
            AltitudeSpeedMultCurve = altmult;
            TimeOffset = offset;
        }

        internal override Vector3 GetWindVec(double lon, double lat, double alt, double time)
        {
            double lonAtTime = Utils.GetValAtLoopTime(LonTimeCurve, time);
            double latAtTime = Utils.GetValAtLoopTime(LatTimeCurve, time);
            double distfraction = Utils.Clamp(Utils.GreatCircleAngle(lon, lat, lonAtTime, latAtTime) / radius, 0.0, double.MaxValue);
            float speedmult = Utils.GetValAtLoopTime(WindSpeedTimeMultCurve, time - timeoffset) * RadiusSpeedMultCurve.Evaluate((float)distfraction) * AltitudeSpeedMultCurve.Evaluate((float)alt);
            if (distfraction < 1.0 && speedmult != 0.0f)
            {
                double heading = Utils.RelativeHeading(lon, lat, lonAtTime, latAtTime, true);
                return new Vector3((float)Utils.FastTrig.Sin(heading), 0.0f, (float)Utils.FastTrig.Cos(heading)) * speedmult * windSpeed;
            }
            return Vector3.zero;
        }
    }

    internal class FlowMap
    {
        internal Texture2D flowmap;
        internal bool useThirdChannel; //whether or not to use the Blue channel to add a vertical component to the winds.
        internal FloatCurve AltitudeSpeedMultCurve;
        internal FloatCurve EWAltitudeSpeedMultCurve;
        internal FloatCurve NSAltitudeSpeedMultCurve;
        internal FloatCurve VAltitudeSpeedMultCurve;
        internal FloatCurve WindSpeedMultiplierTimeCurve;
        internal float EWwind;
        internal float NSwind;
        internal float vWind;

        private float timeoffset;
        internal float TimeOffset { 
            get => timeoffset; 
            set => timeoffset = WindSpeedMultiplierTimeCurve.maxTime != 0.0f ? value % WindSpeedMultiplierTimeCurve.maxTime : 0.0f; 
        }

        internal int x;
        internal int y;

        internal FlowMap(Texture2D path, bool use3rdChannel, FloatCurve altmult, FloatCurve ewaltmultcurve, FloatCurve nsaltmultcurve, FloatCurve valtmultcurve, float EWwind, float NSwind, float vWind, FloatCurve speedtimecurve, float offset)
        {
            flowmap = path;
            useThirdChannel = use3rdChannel;
            AltitudeSpeedMultCurve = altmult;
            EWAltitudeSpeedMultCurve = ewaltmultcurve;
            NSAltitudeSpeedMultCurve = nsaltmultcurve;
            VAltitudeSpeedMultCurve = valtmultcurve;
            WindSpeedMultiplierTimeCurve = speedtimecurve;
            this.EWwind = EWwind;
            this.NSwind = NSwind;
            this.vWind = vWind;
            TimeOffset = offset;

            x = flowmap.width;
            y = flowmap.height;
        }

        //I am concerned enough with memory leaks to include this.
        internal void Delete() => UnityEngine.Object.Destroy(flowmap);

        internal Vector3 GetWindVec(double lon, double lat, double alt, double time)
        {
            //AltitudeSpeedMultiplierCurve cannot go below 0.
            float speedmult = Math.Max(AltitudeSpeedMultCurve.Evaluate((float)alt), 0.0f) * Utils.GetValAtLoopTime(WindSpeedMultiplierTimeCurve, time - timeoffset);
            if (speedmult > 0.0f)
            {
                //adjust longitude so the center of the map is the prime meridian for the purposes of these calculations
                lon += 90.0;
                if (lon > 180.0) { lon -= 360; }
                if (lon <= -180.0) { lon += 360; }
                double mapx = ((lon / 360.0) * x) + (x / 2) - 0.5;
                double mapy = ((lat / 180.0) * y) + (y / 2) - 0.5;
                double lerpx = Utils.Clamp01(mapx - Math.Truncate(mapx));
                double lerpy = Utils.Clamp01(mapy - Math.Truncate(mapy));

                //locate the four nearby points, but don't go over the poles.
                int leftx = (int)(Math.Truncate(mapx) + x) % x;
                int topy = Utils.Clamp((int)Math.Truncate(mapy), 0, y - 1);
                int rightx = (int)(Math.Truncate(mapx) + 1 + x) % x;
                int bottomy = Utils.Clamp((int)Math.Truncate(mapy) + 1, 0, y - 1);

                Color[] colors = new Color[4];
                Vector3[] vectors = new Vector3[4];
                colors[0] = flowmap.GetPixel(leftx, topy);
                colors[1] = flowmap.GetPixel(rightx, topy);
                colors[2] = flowmap.GetPixel(leftx, bottomy);
                colors[3] = flowmap.GetPixel(rightx, bottomy);

                for (int i = 0; i < 4; i++)
                {
                    Vector3 windvec = Vector3.zero;

                    windvec.z = (colors[i].r * 2.0f) - 1.0f;
                    windvec.x = (colors[i].g * 2.0f) - 1.0f;
                    windvec.y = useThirdChannel ? (colors[i].b * 2.0f) - 1.0f : 0.0f;
                    vectors[i] = windvec;
                }
                Vector3 wind = Utils.BiLerp(vectors[0], vectors[1], vectors[2], vectors[3], (float)lerpx, (float)lerpy);
                wind.x = wind.x * NSwind * NSAltitudeSpeedMultCurve.Evaluate((float)alt);
                wind.y = wind.y * vWind * VAltitudeSpeedMultCurve.Evaluate((float)alt);
                wind.z = wind.z * EWwind * EWAltitudeSpeedMultCurve.Evaluate((float)alt);
                return wind * speedmult;
            }
            return Vector3.zero;
        }
    }
}
