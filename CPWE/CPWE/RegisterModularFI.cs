using System;
using UnityEngine;
using ModularFI;

namespace CPWE
{
    [KSPAddon(KSPAddon.Startup.SpaceCentre, true)]
    public class RegisterModularFI : MonoBehaviour
    {
        //register the aero update with ModularFI
        void Start()
        {
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
                    if (ModularFlightIntegrator.RegisterCalculateShockTemperature(ShockTemperatureOverride))
                    {
                        Utils.LogInfo("Successfully Registered CPWE's Shock Temperature Override with ModularFlightIntegrator.");
                    }
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

            CPWE_Core Core = CPWE_Core.Instance;
            //Failsafe if the core fails to load.
            if (Core == null || !Core.HasWind) { return; }

            Vector3 windvec = Core.AppliedWind;
            //Am I just paranoid for including THIS many failsafes?
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
                float machNumber = (fi.Vessel.speedOfSound > 0.0f) ? (NormalDragVector.magnitude / (float)fi.Vessel.speedOfSound) : 0.0f;

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
                        if (part.isKerbalEVA() || part.name.Contains("kerbalEVA")) { continue; }
                        double Q = part.dynamicPressurekPa * 1000.0;
                        wing.SetupCoefficients(wing.part.dragVector, out Vector3 nVel, out Vector3 liftvector, out float liftDot, out float absdot);
                        LiftForce += wing.GetLiftVector(liftvector, liftDot, absdot, Q, machNumber);
                        if (wing.useInternalDragModel) { DragForce += wing.GetDragVector(nVel, absdot, Q); }
                    }
                }

                //insert new drag vector
                part.dragVector = WindDragVector;
                part.dragVectorSqrMag = part.dragVector.sqrMagnitude;
                if (fi.Vessel.speedOfSound > 0.0)
                {
                    double newmach = WindDragVector.magnitude / fi.Vessel.speedOfSound;
                    part.machNumber = newmach;
                    if (part == fi.Vessel.rootPart)
                    {
                        fi.mach = fi.Vessel.mach = newmach;
                    }
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
                if(!Utils.IsNaNOrInfinity(drag) && drag != 0.0)
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
                        if (part.isKerbalEVA() || part.name.Contains("kerbalEVA")) { continue; }
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
                    Vector3 position = (part.Rigidbody != part.rb && PhysicsGlobals.ApplyDragToNonPhysicsPartsAtParentCoM) ? rb.worldCenterOfMass : part.partTransform.TransformPoint(part.CoPOffset);
                    //Adapted from FAR - apply a numerical control factor
                    float numericalControlFactor = (float)(part.rb.mass * part.dragVector.magnitude * 0.67 / (AddedForce.magnitude * TimeWarp.fixedDeltaTime));
                    //add the extra force to the part's center of pressure.
                    if (!Utils.IsNaNOrInfinity(numericalControlFactor)) 
                    {
                        part.Rigidbody.AddForceAtPosition(AddedForce * Utils.Clamp01(numericalControlFactor), position, (ForceMode)(PhysicsGlobals.DragUsesAcceleration ? 5 : 0));
                    }
                }
            }
        }

        void IntegrateOverride(ModularFlightIntegrator fi, Part part)
        {
            if (part.rb != null)
            {
                part.rb.AddForce((Vector3) fi.Vessel.precalc.integrationAccel, ForceMode.Acceleration);
                part.rb.AddForce((Vector3) part.force);
                part.rb.AddTorque((Vector3) part.torque);
                int count = part.forces.Count;
                while (count-- > 0)
                {
                    part.rb.AddForceAtPosition((Vector3) part.forces[count].force, (Vector3) part.forces[count].pos);
                }
            }
            if (part.servoRb != null)
            {
                part.servoRb.AddForce((Vector3) fi.Vessel.precalc.integrationAccel, ForceMode.Acceleration);
            }
            part.forces.Clear();
            part.force.Zero();
            part.torque.Zero();
            NewAeroUpdate(fi, part);

            int partcount = part.children.Count;
            for (int i = 0; i < partcount; i++)
            {
                Part p = part.children[i];
                if(p.isAttached) { IntegrateOverride(fi, p); }
            }
        }
        
        double ShockTemperatureOverride(ModularFlightIntegrator fi)
        {
            CPWE_Core CPWEdata = CPWE_Core.Instance;
            //Failsafes that will fall back to stock FI if anything goes wrong.
            if (CPWEdata == null) { return fi.BaseFICalculateShockTemperature(); }

            Vector3 windvec = CPWEdata.AppliedWind;
            if (windvec == null || Utils.IsVectorNaNOrInfinity(windvec) || windvec == Vector3.zero) { return fi.BaseFICalculateShockTemperature(); }

            Vector3 VelVector = fi.Vel - windvec;
            double spd = VelVector.magnitude;
            double mach = fi.Vessel.speedOfSound > 0.0 ? spd / fi.Vessel.speedOfSound : 0.0;
            double shock = spd * PhysicsGlobals.NewtonianTemperatureFactor;
            fi.convectiveMachLerp = Math.Pow(Utils.Clamp01((mach - PhysicsGlobals.NewtonianMachTempLerpStartMach) / (PhysicsGlobals.NewtonianMachTempLerpEndMach - PhysicsGlobals.NewtonianMachTempLerpStartMach)), PhysicsGlobals.NewtonianMachTempLerpExponent);
            if (fi.convectiveMachLerp > 0.0)
            {
                double shocklerp = PhysicsGlobals.MachTemperatureScalar * Math.Pow(spd, PhysicsGlobals.MachTemperatureVelocityExponent);
                shock = Utils.Lerp(shock, shocklerp, fi.convectiveMachLerp);
            }
            return shock * HighLogic.CurrentGame.Parameters.Difficulty.ReentryHeatScale * fi.CurrentMainBody.shockTemperatureMultiplier;
        }
    }
}
