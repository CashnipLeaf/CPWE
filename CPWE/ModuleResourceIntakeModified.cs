using System;
using KSP.Localization;
using UnityEngine;

namespace CPWE
{
    //slightly modified version of ModuleResourceIntake designed to work with CPWE's wind
    public class ModuleResourceIntakeModified : ModuleResourceIntake
    {
        public ModuleResourceIntakeModified() : base() { }

        public new void FixedUpdate()
        {
            //fall back to stock behavior if something unwanted happens
            CPWE_Core Core = CPWE_Core.Instance;
            if (Core == null || !Core.HasWind || Core.AppliedWind == null)
            {
                base.FixedUpdate();
                return;
            }

            Vector3 windvec = Core.AppliedWind;
            if (windvec == null || Utils.IsVectorNaNOrInfinity(windvec) || windvec == Vector3.zero)
            {
                base.FixedUpdate();
                return;
            }

            if (intakeEnabled && moduleIsEnabled && vessel != null && intakeTransform != null)
            {
                if (!part.ShieldedFromAirstream && !(checkNode && node.attachedPart != null))
                {
                    if (vessel.staticPressurekPa >= kPaThreshold && !(checkForOxygen && !vessel.mainBody.atmosphereContainsOxygen))
                    {
                        if (!disableUnderwater && !underwaterOnly) { goto GetAir; }

                        if (disableUnderwater)
                        {
                            if (!(vessel.mainBody.ocean && FlightGlobals.getAltitudeAtPos(intakeTransform.position, vessel.mainBody) < 0.0))
                            {
                                goto GetAir;
                            }
                        }
                        if (underwaterOnly)
                        {
                            if (!(vessel.mainBody.ocean && FlightGlobals.getAltitudeAtPos(intakeTransform.position, vessel.mainBody) < 0.0))
                            {
                                goto DrainAir;
                            }
                        }
                        else { goto DrainAir; }
                    GetAir:
                        Vector3d vel = vessel.srf_velocity - (Vector3d)windvec;
                        double sqrmag = vel.sqrMagnitude;
                        double truespeed = Math.Sqrt(sqrmag);
                        Vector3d truedir = vel / truespeed;

                        double newmach = truespeed / vessel.speedOfSound;

                        double intakeairspeed = (Utils.Clamp01(Vector3.Dot((Vector3)truedir, intakeTransform.forward)) * truespeed) + intakeSpeed;
                        airSpeedGui = (float)intakeairspeed;
                        double intakemult = intakeairspeed * (unitScalar * area * (double)machCurve.Evaluate((float)newmach));
                        double airdensity = underwaterOnly ? vessel.mainBody.oceanDensity : vessel.atmDensity;
                        resourceUnits = intakemult * airdensity * densityRecip;

                        if (resourceUnits > 0.0)
                        {
                            airFlow = (float)resourceUnits;
                            resourceUnits *= (double)TimeWarp.fixedDeltaTime;
                            if (res.maxAmount - res.amount >= resourceUnits)
                            {
                                part.TransferResource(resourceId, resourceUnits);
                            }
                            else { part.RequestResource(resourceId, -resourceUnits); }
                        }
                        else
                        {
                            resourceUnits = 0.0;
                            airFlow = 0.0f;
                        }
                        status = Localizer.Format("#autoLOC_235936");
                        return;
                    }
                DrainAir:
                    airFlow = 0.0f;
                    airSpeedGui = 0.0f;
                    part.TransferResource(resourceId, double.MinValue);
                    status = Localizer.Format("#autoLOC_235946");
                    return;
                }
                airFlow = 0.0f;
                airSpeedGui = 0.0f;
                status = Localizer.Format("#autoLOC_235899");
                return;
            }
            status = Localizer.Format("#autoLOC_8005416");
            return;
        }
    }
}
