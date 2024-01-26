using System;
using ModularFI;
using UnityEngine;

namespace CPWE
{
    [KSPAddon(KSPAddon.Startup.SpaceCentre, true)]
    [DefaultExecutionOrder(-1)]
    public class RegisterModularFI : MonoBehaviour
    {
        //register the aero update with ModularFI
        void Start()
        {
            Utils.CheckSettings();

            //If FAR is installed, do not register the aerodynamics update. Leave the aerodynamics calulations to FAR.
            if (!Utils.CheckFAR())
            {
                Utils.LogInfo("Registering CPWE with ModularFlightIntegrator.");
                try
                {
                    if (ModularFlightIntegrator.RegisterUpdateAerodynamicsOverride(AeroUpdate)) 
                    { 
                        Utils.LogInfo("Successfully Registered CPWE's Aerodynamics Override with ModularFlightIntegrator."); 
                    }
                    /*if (ModularFlightIntegrator.RegisterCalculateShockTemperature(ShockTemperatureOverride))
                    {
                        Utils.LogInfo("Successfully Registered CPWE's Aerodynamics Override with ModularFlightIntegrator.");
                    }*/
                }
                catch (Exception e) 
                { 
                    Utils.LogError("An Exception occurred when registering CPWE with ModularFlightIntegrator. Exception thrown: " + e.ToString()); 
                } 
            }
            Destroy(this);
        }

        //Newer aero update that should be more accurate and less taxing on the physics engine
        void AeroUpdate(ModularFlightIntegrator fi, Part part)
        {
            CPWE_Core CPWEdata = CPWE_Core.Instance;
            //Failsafes that will fall back to stock FI if anything goes wrong.
            if (CPWEdata == null || part.staticPressureAtm <= 0.0)
            {
                fi.BaseFIUpdateAerodynamics(part);
                return;
            }
            Vector3 windvec = CPWEdata.GetCachedWind() * Utils.GlobalWindSpeedMultiplier;
            if(windvec == null || Utils.IsVectorNaNOrInfinity(windvec) || windvec == Vector3.zero)
            {
                fi.BaseFIUpdateAerodynamics(part);
                return;
            }

            Rigidbody rb = part.Rigidbody;
            if(rb != null)
            {
                bool rigid = part.rb != null;
                bool servorigid = part.servoRb != null;
                part.aerodynamicArea = 0.0;
                part.exposedArea = fi.BaseFICalculateAreaExposed(part);
                part.dynamicPressurekPa = part.submergedDynamicPressurekPa = 0.0;
                if (rigid && part.angularDragByFI)
                {
                    part.angularDrag = 0.0f;
                }
                if (servorigid)
                {
                    part.servoRb.angularDrag = 0.0f;
                }
                double submerged = Math.Max(Math.Min(part.submergedPortion, 1.0), 0.0);

                //insert the wind vector as an offset to the drag vector used for calculations
                Vector3 adjustedwindvec = Vector3.Lerp(windvec, Vector3.zero, (float)submerged);
                Vector3 normaldragVector = rb.velocity + Krakensbane.GetFrameVelocity();
                Vector3 winddragvector = normaldragVector - adjustedwindvec;
                if(part == fi.Vessel.rootPart && fi.Vessel.speedOfSound > 0.0)
                {
                    fi.mach = fi.Vessel.mach = winddragvector.magnitude / fi.Vessel.speedOfSound;
                }
                part.dragVector = winddragvector;
                part.dragVectorSqrMag = part.dragVector.sqrMagnitude;

                bool vec = false;
                if(part.dragVectorSqrMag != 0.0f) 
                {
                    vec = true;
                    part.dragVectorMag = Mathf.Sqrt(part.dragVectorSqrMag);
                    part.dragVectorDir = part.dragVector / part.dragVectorMag;
                    part.dragVectorDirLocal = -part.partTransform.InverseTransformDirection(part.dragVectorDir);
                }
                else
                {
                    part.dragVectorMag = 0.0f;
                    part.dragVectorDir = Vector3.zero;
                    part.dragVectorDirLocal = Vector3.zero; 
                }
                part.dragScalar = 0.0f;

                if (part.ShieldedFromAirstream || (part.atmDensity <= 0.0f && submerged <= 0.0f)) { return; }

                if (!part.DragCubes.None)
                {
                    part.DragCubes.SetDrag(part.dragVectorDirLocal, (float)fi.mach);
                }

                part.aerodynamicArea = fi.BaseFICalculateAerodynamicArea(part);
                if (!(PhysicsGlobals.ApplyDrag && vec)) { return; }
                if (rb != part.rb && !PhysicsGlobals.ApplyDragToNonPhysicsParts) { return; }

                double notsubmerged = 1.0 - submerged;
                bool partsubmerged = false;
                double staticpress;
                double OceanDensity = fi.CurrentMainBody.oceanDensity * 1000.0;

                if (fi.CurrentMainBody.ocean && submerged > 0.0f)
                {
                    partsubmerged = true;
                    double cacheOceanDensity = OceanDensity * PhysicsGlobals.BuoyancyWaterAngularDragScalar * part.waterAngularDragMultiplier;
                    part.submergedDynamicPressurekPa = OceanDensity;
                    staticpress = Utils.Lerp(part.staticPressureAtm, cacheOceanDensity, submerged);
                }
                else
                {
                    part.dynamicPressurekPa = part.atmDensity;
                    staticpress = part.staticPressureAtm;
                }

                double dynamicpressV = 0.0005 * part.dragVectorSqrMag;
                part.dynamicPressurekPa *= dynamicpressV;
                part.submergedDynamicPressurekPa *= dynamicpressV;
                if (rigid && part.angularDragByFI)
                {
                    double angularDragQ;
                    double cachewaterpress = part.submergedDynamicPressurekPa * 0.0098692326671601278 * PhysicsGlobals.BuoyancyWaterAngularDragScalar * part.waterAngularDragMultiplier;
                    if (partsubmerged)
                    {
                        angularDragQ = staticpress + Utils.Lerp(part.dynamicPressurekPa * 0.0098692326671601278, cachewaterpress, submerged);
                    }
                    else
                    {
                        angularDragQ = part.dynamicPressurekPa * 0.0098692326671601278;
                    }
                    angularDragQ = Math.Max(angularDragQ, 0.0);

                    part.rb.angularDrag = part.angularDrag * (float)angularDragQ * PhysicsGlobals.AngularDragMultiplier;
                    if (servorigid)
                    {
                        part.servoRb.angularDrag = part.rb.angularDrag;
                    }
                }
                int mode = 0;
                if (PhysicsGlobals.DragUsesAcceleration) { mode = 5; }

                double drag = fi.BaseFICalculateDragValue(part) * fi.pseudoReDragMult;
                if (!double.IsNaN(drag) && drag != 0.0)
                {
                    part.dragScalar = (float)(part.dynamicPressurekPa * drag * notsubmerged) * PhysicsGlobals.DragMultiplier;
                    //inline version of ApplyAeroDrag()
                    Vector3 position;
                    if (rb != part.rb)
                    {
                        if (PhysicsGlobals.ApplyDragToNonPhysicsPartsAtParentCoM)
                        {
                            position = rb.worldCenterOfMass;
                            goto add_drag;
                        }
                    }
                    position = part.partTransform.TransformPoint(part.CoPOffset);
                add_drag:
                    rb.AddForceAtPosition(-part.dragVectorDir * part.dragScalar, position, (ForceMode)mode);
                    goto ApplyLift;
                }
                part.dragScalar = 0.0f;

            ApplyLift:
                if(part.hasLiftModule) 
                {
                    if (adjustedwindvec != Vector3.zero)
                    {
                        //compute and add additional forces to wing parts since their lift/drag calculations are (annoyingly) done separately from the flightintegrator >:(
                        foreach (var m in part.Modules)
                        {
                            if (m is ModuleLiftingSurface wing)
                            {
                                double QLiftWithWind = Utils.Lerp(wing.part.dynamicPressurekPa, wing.part.submergedDynamicPressurekPa * wing.part.submergedLiftScalar, submerged) * 1000.0;
                                double QDragWithWind = QLiftWithWind;
                                wing.SetupCoefficients(wing.part.dragVector, out Vector3 nVel, out Vector3 liftvector, out float liftDot, out float absdot);
                                Vector3 LiftForce = wing.GetLiftVector(liftvector, liftDot, absdot, QLiftWithWind, (float)fi.mach);
                                Vector3 DragForce = wing.GetDragVector(nVel, absdot, QDragWithWind);

                                double QLiftNoWind = Utils.Lerp(staticpress * 0.0005 * normaldragVector.sqrMagnitude, OceanDensity * 0.0005 * normaldragVector.sqrMagnitude * wing.part.submergedLiftScalar, submerged) * 1000.0;
                                double QDragNoWind = QLiftNoWind;
                                double normalmach = 0.0;
                                if (fi.Vessel.speedOfSound > 0.0)
                                {
                                    normalmach = normaldragVector.magnitude / fi.Vessel.speedOfSound;
                                }
                                wing.SetupCoefficients(normaldragVector, out Vector3 nVel2, out Vector3 liftvector2, out float liftDot2, out float absdot2);
                                Vector3 LiftForceNoWind = wing.GetLiftVector(liftvector2, liftDot2, absdot2, QLiftNoWind, (float)normalmach);
                                Vector3 DragForceNoWind = wing.GetDragVector(nVel2, absdot2, QDragNoWind);

                                Vector3 AddedForce = LiftForce - LiftForceNoWind;
                                if (wing.useInternalDragModel) { AddedForce += DragForce - DragForceNoWind; }

                                //Adapted from FAR - apply a numerical control factor
                                float numericalControlFactor = (float)(wing.part.rb.mass * wing.part.dragVector.magnitude * 0.67 / (AddedForce.magnitude * TimeWarp.fixedDeltaTime));
                                wing.part.AddForce(AddedForce * Math.Min(numericalControlFactor, 1));
                            }
                        }
                    }
                    return; 
                }

                if (part.bodyLiftOnlyUnattachedLiftActual)
                {
                    if (part.bodyLiftOnlyProvider != null && part.bodyLiftOnlyProvider.IsLifting) { return; }
                }
                double LiftQ;
                if (!partsubmerged)
                {
                    LiftQ = part.dynamicPressurekPa;
                }
                else
                {
                    LiftQ = Utils.Lerp(part.dynamicPressurekPa, part.submergedDynamicPressurekPa * part.submergedLiftScalar, submerged);
                }
                double liftScalar = LiftQ * part.bodyLiftMultiplier * (double)PhysicsGlobals.BodyLiftMultiplier * (double)PhysicsGlobals.BodyLiftCurve.liftMachCurve.Evaluate((float)fi.mach);
                part.bodyLiftScalar = (float)liftScalar;
                if (part.bodyLiftScalar == 0.0 || part.DragCubes.LiftForce == Vector3.zero || part.DragCubes.LiftForce.IsInvalid()) { return; }

                //inline version of ApplyAeroLift()
                Vector3 position1;
                if (rb != part.rb)
                {
                    if (PhysicsGlobals.ApplyDragToNonPhysicsPartsAtParentCoM)
                    {
                        position1 = rb.worldCenterOfMass;
                        goto add_lift;
                    }
                }
                position1 = part.partTransform.TransformPoint(part.CoLOffset);
            add_lift:
                Vector3 vector = Vector3.ProjectOnPlane(part.transform.rotation * (part.bodyLiftScalar * part.DragCubes.LiftForce), -part.dragVectorDir);
                part.bodyLiftLocalPosition = part.partTransform.InverseTransformPoint(position1);
                part.bodyLiftLocalVector = part.partTransform.InverseTransformDirection(vector);
                rb.AddForceAtPosition(vector, position1, (ForceMode)mode);
            }
        }

        /*
        double ShockTemperatureOverride(ModularFlightIntegrator fi)
        {
            CPWE_Core CPWEdata = CPWE_Core.Instance;
            //Failsafes that will fall back to stock FI if anything goes wrong.
            if (CPWEdata == null)
            {
                return fi.BaseFICalculateShockTemperature();
            }
            Vector3 windvec = CPWEdata.GetCachedWind();
            if (windvec == null || Utils.IsVectorNaNOrInfinity(windvec) || windvec == Vector3.zero)
            {
                return fi.BaseFICalculateShockTemperature();
            }

            Vector3 DragVector = fi.Vel - windvec;
            double shock = DragVector.magnitude * PhysicsGlobals.NewtonianTemperatureFactor;
            if(fi.convectiveMachLerp > 0.0)
            {
                double shocklerp = PhysicsGlobals.MachTemperatureScalar * Math.Pow(DragVector.magnitude, PhysicsGlobals.MachTemperatureVelocityExponent);
                shock = Mathf.LerpUnclamped((float)shock, (float)shocklerp, (float)fi.convectiveMachLerp);
            }
            return shock * HighLogic.CurrentGame.Parameters.Difficulty.ReentryHeatScale * fi.CurrentMainBody.shockTemperatureMultiplier;
        }*/
    }
}
