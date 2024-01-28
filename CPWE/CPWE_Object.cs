using System;
using System.Collections.Generic;
using UnityEngine;

namespace CPWE
{
    //the object used to store wind pattern data
    internal class CPWE_Object : MonoBehaviour
    {
        private readonly Dictionary<string, CPWE_Body> Bodies;
        internal CPWE_Object()
        {
            Bodies = new Dictionary<string, CPWE_Body>();
        }

        //add a new body to the list if said body hasn't already been added. Prevents duplicate keys from being entered.
        internal void AddBody(string body, double scalefactor)
        {
            if (!HasBody(body)) 
            { 
                Bodies.Add(body, new CPWE_Body(body, scalefactor)); 
            }
        }

        internal bool HasBody(string bod) { return Bodies.ContainsKey(bod); }

        //X = North, Y = Up, Z = East
        internal Vector3 GetWindVector(string body, double vlon, double vlat, double vheight) => HasBody(body) ? Bodies[body].GetWind(vlon, vlat, vheight) : Vector3.zero;

        internal void AddWind(string body, Wind wnd)
        {
            //check that the CPWE_Body object is present before adding a wind object
            if (Bodies.ContainsKey(body)) 
            { 
                Bodies[body].AddNewWind(wnd); 
            }
        }
        internal void AddFlowMap(string body, FlowMap flmp)
        {
            //check that the CPWE_Body object is present before adding a flowmap object
            if (Bodies.ContainsKey(body)) 
            { 
                Bodies[body].AddFlowMap(flmp); 
            }
        }

        //Remove from memory
        internal void Delete()
        {
            foreach (KeyValuePair<string, CPWE_Body> de in Bodies)
            {
                Bodies[de.Key].Delete();
            }
            Bodies.Clear();
        }
    }

    //stores the information for each body
    internal class CPWE_Body
    {
        private List<Wind> winds;
        private List<FlowMap> flowmaps;
        private readonly string body;
        private readonly double scalefactor;

        internal CPWE_Body(string body, double scale)
        {
            this.body = body;
            winds = new List<Wind>();
            flowmaps = new List<FlowMap>();
            scalefactor = scale;
        }

        internal void AddNewWind(Wind wnd) { winds.Add(wnd); }
        internal void AddFlowMap(FlowMap flmp) { flowmaps.Add(flmp); }
        
        internal Vector3 GetWind(double lon, double lat, double alt)
        {
            Vector3 windvec = Vector3.zero;
            alt /= scalefactor;
            foreach (Wind w in winds) 
            {
                Vector3 tempwindvec = w.GetWindVec(lon, lat, alt);
                if (Utils.IsVectorNaNOrInfinity(tempwindvec)) { continue; }
                windvec += tempwindvec; 
            }
            foreach (FlowMap flmp in flowmaps) 
            {
                Vector3 tempwindvec = flmp.GetWindVec(lon, lat, alt);
                if (Utils.IsVectorNaNOrInfinity(tempwindvec)) { continue; }
                windvec += tempwindvec; 
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
        internal FloatCurve RadiusSpeedMultCurve;
        internal FloatCurve WindSpeedTimeCurve;
        internal FloatCurve AltitudeSpeedMultCurve;

        internal abstract Vector3 GetWindVec(double lon, double lat, double alt);
    }

    internal class JetStream : Wind
    {
        internal float longitude;
        internal float length;
        internal FloatCurve RadiusCurve;
        internal FloatCurve LatitudeCurve;
        internal FloatCurve LonLatSpeedMultCurve;

        internal JetStream(float lon, float len, FloatCurve radcurve, FloatCurve latcurve, FloatCurve speedCurve, FloatCurve lonlatmult, FloatCurve radmult, FloatCurve altmult)
        {
            longitude = lon;
            length = len;
            RadiusCurve = radcurve;
            LatitudeCurve = latcurve;
            LonLatSpeedMultCurve = lonlatmult;
            WindSpeedTimeCurve = speedCurve;
            RadiusSpeedMultCurve = radmult;
            AltitudeSpeedMultCurve = altmult;
        }

        internal override Vector3 GetWindVec(double lon, double lat, double alt)
        {
            double distfraction = Utils.GreatCircleAngle(lon, lat, (double)LatitudeCurve.Evaluate((float)lat), lat) / (RadiusCurve.Evaluate((float)lon));
            float speed = Utils.GetValAtLoopTime(WindSpeedTimeCurve) * RadiusSpeedMultCurve.Evaluate((float)distfraction) * AltitudeSpeedMultCurve.Evaluate((float)alt) * LonLatSpeedMultCurve.Evaluate((float)lon);
            if (distfraction < 1.0 && (length == 0.0f || Utils.InRange(lat, longitude, longitude + length)) && speed != 0.0f)
            {
                Vector3 windvec = new Vector3(Utils.FloatCurveDerivative(LatitudeCurve, (float)lat), 0.0f, 1.0f);
                windvec.Normalize();
                return windvec * speed;
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

        internal PolarStream(float lat, float len, FloatCurve radcurve, FloatCurve loncurve, FloatCurve speedCurve, FloatCurve lonlatmult, FloatCurve radmult, FloatCurve altmult)
        {
            latitude = lat;
            length = len;
            RadiusCurve = radcurve;
            LongitudeCurve = loncurve;
            LonLatSpeedMultCurve = lonlatmult;
            WindSpeedTimeCurve = speedCurve;
            RadiusSpeedMultCurve = radmult;
            AltitudeSpeedMultCurve = altmult;
        }

        internal override Vector3 GetWindVec(double lon, double lat, double alt)
        {
            double distfraction = Utils.GreatCircleAngle(lon, lat, (double)LongitudeCurve.Evaluate((float)lat), lat) / (RadiusCurve.Evaluate((float)lat));
            float speed = Utils.GetValAtLoopTime(WindSpeedTimeCurve) * RadiusSpeedMultCurve.Evaluate((float)distfraction) * AltitudeSpeedMultCurve.Evaluate((float)alt) * LonLatSpeedMultCurve.Evaluate((float)lon);
            if (distfraction < 1.0 && (length == 0.0f || Utils.InRange(lat, latitude, latitude + length)) && speed != 0.0f)
            {
                Vector3 windvec = new Vector3(1.0f, 0.0f, Utils.FloatCurveDerivative(LongitudeCurve, (float)lat));
                windvec.Normalize();
                return windvec * speed;
            }
            return Vector3.zero;
        }
    }

    internal class Vortex : Wind
    {
        internal FloatCurve LonTimeCurve;
        internal FloatCurve LatTimeCurve;

        internal Vortex(float rad, FloatCurve radspdcurve, FloatCurve lontime, FloatCurve lattime, FloatCurve speedtime, FloatCurve altmult)
        {
            radius = rad;
            RadiusSpeedMultCurve = radspdcurve;
            LonTimeCurve = lontime;
            LatTimeCurve = lattime;
            WindSpeedTimeCurve = speedtime;
            AltitudeSpeedMultCurve = altmult;
        }

        internal override Vector3 GetWindVec(double lon, double lat, double alt)
        {
            double lonAtTime = Utils.GetValAtLoopTime(LonTimeCurve);
            double latAtTime = Utils.GetValAtLoopTime(LatTimeCurve);
            double distfraction = Utils.GreatCircleAngle(lon, lat, lonAtTime, latAtTime) / radius;
            float speed = Utils.GetValAtLoopTime(WindSpeedTimeCurve) * RadiusSpeedMultCurve.Evaluate((float)distfraction) * AltitudeSpeedMultCurve.Evaluate((float)alt);
            if (distfraction < 1.0 && speed != 0.0f)
            {
                double heading = Utils.RelativeHeading(lon, lat, lonAtTime, latAtTime, true);
                return new Vector3((float)Math.Cos(heading), 0.0f, (float)Math.Sin(heading)) * speed;
            }
            return Vector3.zero;
        }
    }
    
    internal class Updraft : Wind
    {
        internal FloatCurve LonTimeCurve;
        internal FloatCurve LatTimeCurve;

        internal Updraft(float rad, FloatCurve radspdcurve, FloatCurve lontime, FloatCurve lattime, FloatCurve speedtime, FloatCurve altmult)
        {
            radius = rad;
            RadiusSpeedMultCurve = radspdcurve;
            LonTimeCurve = lontime;
            LatTimeCurve = lattime;
            WindSpeedTimeCurve = speedtime;
            AltitudeSpeedMultCurve = altmult;
        }

        internal override Vector3 GetWindVec(double lon, double lat, double alt)
        {
            double distfraction = Utils.GreatCircleAngle(lon, lat, Utils.GetValAtLoopTime(LonTimeCurve), Utils.GetValAtLoopTime(LatTimeCurve)) / radius;
            float speed = Utils.GetValAtLoopTime(WindSpeedTimeCurve) * RadiusSpeedMultCurve.Evaluate((float)distfraction) * AltitudeSpeedMultCurve.Evaluate((float)alt);
            if (distfraction < 1.0 && speed != 0.0f)
            {
                return new Vector3(0.0f, speed, 0.0f);
            }
            return Vector3.zero;
        }
    }

    internal class ConvergingWind : Wind
    {
        internal FloatCurve LonTimeCurve;
        internal FloatCurve LatTimeCurve;

        internal ConvergingWind(float rad, FloatCurve radspdcurve, FloatCurve lontime, FloatCurve lattime, FloatCurve speedtime, FloatCurve altmult)
        {
            radius = rad;
            RadiusSpeedMultCurve = radspdcurve;
            LonTimeCurve = lontime;
            LatTimeCurve = lattime;
            WindSpeedTimeCurve = speedtime;
            AltitudeSpeedMultCurve = altmult;
        }

        internal override Vector3 GetWindVec(double lon, double lat, double alt)
        {
            double lonAtTime = Utils.GetValAtLoopTime(LonTimeCurve);
            double latAtTime = Utils.GetValAtLoopTime(LatTimeCurve);
            double distfraction = Utils.GreatCircleAngle(lon, lat, lonAtTime, latAtTime) / radius;
            float speed = Utils.GetValAtLoopTime(WindSpeedTimeCurve) * RadiusSpeedMultCurve.Evaluate((float)distfraction) * AltitudeSpeedMultCurve.Evaluate((float)alt);
            if (distfraction < 1.0 && speed != 0.0f)
            {
                double heading = Utils.RelativeHeading(lon, lat, lonAtTime, latAtTime, true);
                return new Vector3((float)Math.Sin(heading), 0.0f, (float)Math.Cos(heading)) * speed;
            }
            return Vector3.zero;
        }
    }

    internal class FlowMap
    {
        internal bool useThirdChannel; //whether or not to use the Blue channel to add a vertical component to the winds.
        internal FloatCurve WindSpeedMultiplierTimeCurve;
        internal FloatCurve AltitudeSpeedMultCurve;
        internal Texture2D flowmap;
        internal FloatCurve EWwind;
        internal FloatCurve NSwind;
        internal FloatCurve vWind;

        internal int x;
        internal int y;

        internal FlowMap(FloatCurve windspd, Texture2D path, FloatCurve altmultcurve, bool use3rdChannel, FloatCurve EWwind, FloatCurve NSwind, FloatCurve vWind)
        {
            WindSpeedMultiplierTimeCurve = windspd;
            AltitudeSpeedMultCurve = altmultcurve;
            useThirdChannel = use3rdChannel;
            this.EWwind = EWwind;
            this.NSwind = NSwind;
            this.vWind = vWind;

            flowmap = path;
            x = flowmap.width; 
            y = flowmap.height;
        }

        //I am concerned enough with memory leaks to include this.
        internal void Delete()
        {
            UnityEngine.Object.Destroy(flowmap);
            flowmap = null;
        }

        internal Vector3 GetWindVec(double lon, double lat, double alt)
        {
            float windspeedmult = Math.Abs(Utils.GetValAtLoopTime(WindSpeedMultiplierTimeCurve) * AltitudeSpeedMultCurve.Evaluate((float)alt));
            if (windspeedmult > 0.0f)
            {
                //adjust longitude so the center of the map is the prime meridian for the purposes of these calculations
                lon += 90;
                if(lon > 180)
                {
                    lon -= 360;
                }
                if (lon <= -180)
                {
                    lon += 360;
                }
                double mapx = ((lon / 360) * x) + (x / 2) - 0.5;
                double mapy = ((lat / 180) * y) + (y / 2) - 0.5;
                double lerpx = mapx - Math.Truncate(mapx);
                double lerpy = mapy - Math.Truncate(mapy);

                //locate the four nearby points, but don't go over the poles.
                int leftx = (int)(Math.Truncate(mapx) + x) % x;
                int topy = Math.Max(0, (int)Math.Truncate(mapy));
                int rightx = (int)(Math.Truncate(mapx) + 1 + x) % x;
                int bottomy = Math.Min((int)(Math.Truncate(mapy) + 1), y-1);

                Color[] colors = new Color[4];
                Vector3[] vectors = new Vector3[4];
                colors[0] = flowmap.GetPixel(leftx,topy);
                colors[1] = flowmap.GetPixel(rightx, topy);
                colors[2] = flowmap.GetPixel(leftx, bottomy);
                colors[3] = flowmap.GetPixel(rightx, bottomy);

                for (int i = 0; i < 4; i++)
                {
                    Vector3 windvec = Vector3.zero;
                    float r = colors[i].r;
                    float g = colors[i].g;
                    float b = colors[i].b;

                    windvec.z = (r * 2.0f) - 1.0f;
                    windvec.x = (g * 2.0f) - 1.0f;
                    if (useThirdChannel) { windvec.y = (b * 2.0f) - 1.0f; }
                    vectors[i] = windvec;
                }
                Vector3 wind = Utils.BiLerp(vectors[0], vectors[1], vectors[2], vectors[3], (float)lerpx, (float)lerpy) * windspeedmult;
                return new Vector3(wind.x * Math.Abs(Utils.GetValAtLoopTime(NSwind)), wind.y * Math.Abs(Utils.GetValAtLoopTime(vWind)), wind.z * Math.Abs(Utils.GetValAtLoopTime(EWwind)));
            }
            return Vector3.zero;
        }
    }

    //finish this, you dummy
    internal class FlowMapStack
    {
        internal FlowMapStack()
        {

        }
    }
}
