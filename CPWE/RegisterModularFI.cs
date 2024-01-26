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
                    if (ModularFlightIntegrator.RegisterUpdateAerodynamicsOverride(NewAeroUpdate)) 
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

        void NewAeroUpdate(ModularFlightIntegrator fi, Part part)
        {
            fi.BaseFIUpdateAerodynamics(part);

            CPWE_Core CPWEdata = CPWE_Core.Instance;
            //Failsafes if the core failes to load.
            if (CPWEdata == null) { return; }

            Vector3 windvec = CPWEdata.GetCachedWind() * Utils.GlobalWindSpeedMultiplier;
            if (windvec == null || Utils.IsVectorNaNOrInfinity(windvec) || windvec == Vector3.zero) { return; }

            Rigidbody rb = part.rb;
            if (rb && !part.ShieldedFromAirstream && part.staticPressureAtm > 0.0 && !(part.Rigidbody != rb && !PhysicsGlobals.ApplyDragToNonPhysicsParts))
            {
                Vector3 DragForce = Vector3.zero;
                Vector3 LiftForce = Vector3.zero;
                Vector3 DragForceWithWind = Vector3.zero;
                Vector3 LiftForceWithWind = Vector3.zero;

                Vector3 NormalDragVector = rb.velocity + Krakensbane.GetFrameVelocity();
                Vector3 WindDragVector = NormalDragVector - windvec;
                float machNumber = 0.0f;
                if(fi.Vessel.speedOfSound > 0.0f)
                {
                    machNumber = NormalDragVector.magnitude / (float)fi.Vessel.speedOfSound;
                }

                //get body drag/lift force w/o wind
                if (!part.DragCubes.None)
                {
                    DragForce += -part.dragVectorDir * part.dragScalar;
                }
                if (!part.hasLiftModule)
                {
                    LiftForce += Vector3.ProjectOnPlane(part.transform.rotation * (part.bodyLiftScalar * part.DragCubes.LiftForce), -part.dragVectorDir);
                }
                //compute wing lift/drag force w/o wind
                foreach (var m in part.Modules)
                {
                    if (m is ModuleLiftingSurface wing)
                    {
                        double Q = part.dynamicPressurekPa * 1000.0;
                        wing.SetupCoefficients(wing.part.dragVector, out Vector3 nVel, out Vector3 liftvector, out float liftDot, out float absdot);
                        LiftForce += wing.GetLiftVector(liftvector, liftDot, absdot, Q, machNumber);
                        if (wing.useInternalDragModel) { DragForce += wing.GetDragVector(nVel, absdot, Q); }
                    }
                }

                //insert new drag vector
                part.dragVector = WindDragVector;
                part.dragVectorSqrMag = part.dragVector.sqrMagnitude;
                if (part == fi.Vessel.rootPart && fi.Vessel.speedOfSound > 0.0)
                {
                    fi.mach = fi.Vessel.mach = WindDragVector.magnitude / fi.Vessel.speedOfSound;
                }
                if (part.dragVectorSqrMag != 0.0f)
                {
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

                //update dynamic pressure
                part.dynamicPressurekPa = part.atmDensity * 0.0005 * part.dragVectorSqrMag;
                
                //get body drag/lift force with wind
                if (!part.DragCubes.None)
                {
                    part.DragCubes.SetDrag(part.dragVectorDirLocal, (float)fi.mach);
                }
                part.aerodynamicArea = fi.BaseFICalculateAerodynamicArea(part);
                double drag = fi.BaseFICalculateDragValue(part) * fi.pseudoReDragMult;
                if(!double.IsNaN(drag) && drag != 0.0)
                {
                    part.dragScalar = (float)(drag * part.dynamicPressurekPa) * PhysicsGlobals.DragMultiplier;
                    DragForceWithWind += -part.dragVectorDir * part.dragScalar;
                }
                if (!part.hasLiftModule)
                {
                    part.bodyLiftScalar = (float)(part.dynamicPressurekPa * part.bodyLiftMultiplier * (double)PhysicsGlobals.BodyLiftMultiplier * (double)PhysicsGlobals.BodyLiftCurve.liftMachCurve.Evaluate((float)fi.mach));
                    LiftForceWithWind += Vector3.ProjectOnPlane(part.transform.rotation * (part.bodyLiftScalar * part.DragCubes.LiftForce), -part.dragVectorDir);
                }

                //compute wing lift/drag force w/ wind
                foreach (var m in part.Modules)
                {
                    if (m is ModuleLiftingSurface wing)
                    {
                        double Q2 = part.dynamicPressurekPa * 1000.0;
                        wing.SetupCoefficients(WindDragVector, out Vector3 nVel, out Vector3 liftvector, out float liftDot, out float absdot);
                        LiftForceWithWind += wing.GetLiftVector(liftvector, liftDot, absdot, Q2, (float)fi.mach);
                        if (wing.useInternalDragModel) { DragForceWithWind += wing.GetDragVector(nVel, absdot, Q2); }
                    }
                }

                //compute the difference in lift/drag with and without wind
                Vector3 AddedForce = (DragForceWithWind - DragForce) + (LiftForceWithWind - LiftForce);
                if(!Utils.IsVectorNaNOrInfinity(AddedForce) && AddedForce != Vector3.zero)
                {
                    //Adapted from FAR - apply a numerical control factor
                    float numericalControlFactor = (float)(part.rb.mass * part.dragVector.magnitude * 0.67 / (AddedForce.magnitude * TimeWarp.fixedDeltaTime));

                    //add the extra force to the part.
                    part.AddForce(AddedForce * Math.Min(numericalControlFactor, 1));
                }
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
