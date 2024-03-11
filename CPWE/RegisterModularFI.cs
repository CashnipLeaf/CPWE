using System;
using System.Reflection;
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
            if (CheckFAR())
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

            //Failsafes if something unwanted happens
            CPWE_Core Core = CPWE_Core.Instance;
            if (Core == null || !Core.HasWind) { return; }

            Vector3 windvec = Core.AppliedWind;
            //Am I just paranoid for including THIS many NaN/Infinity checks?
            if (windvec == null || Utils.IsVectorNaNOrInfinity(windvec) || windvec == Vector3.zero) { return; }

            Rigidbody rb = part.rb;
            if (rb && !part.ShieldedFromAirstream && part.staticPressureAtm > 0.0 && !(part.Rigidbody != rb && !PhysicsGlobals.ApplyDragToNonPhysicsParts))
            {
                //initialize drag vectors
                Vector3 NormalDragVector = rb.velocity + Krakensbane.GetFrameVelocity();
                Vector3 WindDragVector = NormalDragVector - windvec;
                float machNumber = (fi.Vessel.speedOfSound > 0.0f) ? (NormalDragVector.magnitude / (float)fi.Vessel.speedOfSound) : 0.0f;
                double oldQ = part.dynamicPressurekPa * 1000.0;

                Vector3 DragForce = Vector3.zero;
                Vector3 LiftForce = Vector3.zero;

                //get body drag/lift force w/o wind
                if (!part.DragCubes.None) { DragForce += -part.dragVectorDir * part.dragScalar; }

                if (!part.hasLiftModule)
                {
                    LiftForce += Vector3.ProjectOnPlane(part.transform.rotation * (part.bodyLiftScalar * part.DragCubes.LiftForce), -part.dragVectorDir);
                }

                //insert new drag vector, update values
                part.dragVector = WindDragVector;
                part.dragVectorSqrMag = part.dragVector.sqrMagnitude;
                double newmach = 0.0;
                if (fi.Vessel.speedOfSound > 0.0)
                {
                    newmach = WindDragVector.magnitude / fi.Vessel.speedOfSound;
                    part.machNumber = newmach;
                    if (part == fi.Vessel.rootPart) { fi.mach = fi.Vessel.mach = newmach; }
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
                Vector3 DragForceWithWind = Vector3.zero;
                Vector3 LiftForceWithWind = Vector3.zero;

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

                //get wing lift/drag force w/o wind and w/ wind
                foreach (var m in part.Modules)
                {
                    if (m is ModuleControlSurface elevon)
                    {
                        if (part.isKerbalEVA() || part.name.Contains("kerbalEVA")) { continue; }
                        //the fields I need for control surface lift/drag are protected, so this is how I get my way.
                        Type ctrlsrf = typeof(ModuleControlSurface);
                        FieldInfo ctrlpointvel = ctrlsrf.GetField("pointVelocity", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetField);
                        Vector3 vel = ctrlpointvel != null ? (Vector3)ctrlpointvel.GetValue(elevon) : elevon.part.dragVector;

                        //no wind
                        elevon.SetupCoefficients(vel, out Vector3 nVel, out Vector3 liftvector, out float liftDot, out float absdot);
                        LiftForce += elevon.GetLiftVector(liftvector, liftDot, absdot, oldQ, machNumber) * (1.0f - elevon.ctrlSurfaceArea);
                        if (elevon.useInternalDragModel) { DragForce += elevon.GetDragVector(nVel, absdot, oldQ, machNumber) * (1.0f - elevon.ctrlSurfaceArea); }

                        float newliftDot = liftDot;
                        float newabsdot = absdot;
                        Vector3 newliftvec = liftvector;

                        //yes wind
                        double Q2 = part.dynamicPressurekPa * 1000.0;
                        elevon.SetupCoefficients(vel - windvec, out Vector3 nVel2, out Vector3 liftvector2, out float liftDot2, out float absdot2);
                        LiftForceWithWind += elevon.GetLiftVector(liftvector2, liftDot2, absdot2, Q2, (float)newmach) * (1.0f - elevon.ctrlSurfaceArea);
                        if (elevon.useInternalDragModel) { DragForceWithWind += elevon.GetDragVector(nVel2, absdot2, Q2, (float)newmach) * (1.0f - elevon.ctrlSurfaceArea); }
                        
                        float newliftDot2 = liftDot2;
                        float newabsdot2 = absdot2;
                        Vector3 newliftvec2 = liftvector2;
                        try
                        {
                            FieldInfo incidence = ctrlsrf.GetField("airflowIncidence", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetField);
                            FieldInfo surfacetransform = ctrlsrf.GetField("ctrlSurface", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetField);
                            FieldInfo neutral = ctrlsrf.GetField("neutral", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetField);
                            if (incidence != null && surfacetransform != null && neutral != null)
                            {
                                Transform surface = (Transform)surfacetransform.GetValue(elevon);
                                Quaternion Neutral = (Quaternion)neutral.GetValue(elevon);
                                if (elevon.alwaysRecomputeLift || surface.localRotation != Neutral)
                                {
                                    Quaternion airflowincidence = (Quaternion)incidence.GetValue(elevon);
                                    //no wind
                                    newliftvec = airflowincidence * liftvector;
                                    newliftDot = Vector3.Dot(nVel, newliftvec);
                                    newabsdot = Math.Abs(newliftDot);

                                    //yes wind
                                    newliftvec2 = airflowincidence * liftvector2;
                                    newliftDot2 = Vector3.Dot(nVel2, newliftvec2);
                                    newabsdot2 = Math.Abs(newliftDot2);
                                }
                            }
                        }
                        finally
                        {
                            //no wind
                            LiftForce += elevon.GetLiftVector(newliftvec, newliftDot, newabsdot, oldQ, machNumber) * elevon.ctrlSurfaceArea;
                            if (elevon.useInternalDragModel) { DragForce += elevon.GetDragVector(nVel, newabsdot, oldQ, machNumber) * elevon.ctrlSurfaceArea; }

                            //yes wind
                            LiftForceWithWind += elevon.GetLiftVector(newliftvec2, newliftDot2, newabsdot2, Q2, (float)newmach) * elevon.ctrlSurfaceArea;
                            if (elevon.useInternalDragModel) { DragForceWithWind += elevon.GetDragVector(nVel2, newabsdot2, Q2, (float)newmach) * elevon.ctrlSurfaceArea; }
                        }
                    }
                    else if (m is ModuleLiftingSurface wing)
                    {
                        if (part.isKerbalEVA() || part.name.Contains("kerbalEVA")) { continue; }
                        Type liftsrf = typeof(ModuleLiftingSurface);
                        FieldInfo wingpointvel = liftsrf.GetField("pointVelocity", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetField);
                        Vector3 vel = wingpointvel != null ? (Vector3)wingpointvel.GetValue(wing) : wing.part.dragVector;

                        //no wind
                        wing.SetupCoefficients(vel, out Vector3 nVel, out Vector3 liftvector, out float liftDot, out float absdot);
                        LiftForce += wing.GetLiftVector(liftvector, liftDot, absdot, oldQ, machNumber);
                        if (wing.useInternalDragModel) { DragForce += wing.GetDragVector(nVel, absdot, oldQ, machNumber); }

                        //yes wind
                        double Q2 = part.dynamicPressurekPa * 1000.0;
                        wing.SetupCoefficients(vel - windvec, out Vector3 nVel2, out Vector3 liftvector2, out float liftDot2, out float absdot2);
                        LiftForceWithWind += wing.GetLiftVector(liftvector2, liftDot2, absdot2, Q2, (float)newmach);
                        if (wing.useInternalDragModel) { DragForceWithWind += wing.GetDragVector(nVel2, absdot2, Q2, (float)newmach); }
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
                    if (!Utils.IsNaNOrInfinity(numericalControlFactor)) //You never know.
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

        internal bool CheckFAR() //check if FAR is installed
        {
            try
            {
                Type FARAtm = null;
                foreach (var assembly in AssemblyLoader.loadedAssemblies)
                {
                    if (assembly.name == "FerramAerospaceResearch")
                    {
                        var types = assembly.assembly.GetExportedTypes();
                        foreach (Type t in types)
                        {
                            if (t.FullName.Equals("FerramAerospaceResearch.FARWind")) { FARAtm = t; }
                            if (t.FullName.Equals("FerramAerospaceResearch.FARAtmosphere")) { FARAtm = t; }
                        }
                    }
                }
                if (FARAtm != null)
                {
                    Utils.LogInfo("FerramAerospaceResearch detected. Aerodynamics calculations will be deferred to FAR.");
                    return true;
                }
                return false;
            }
            catch (Exception e) { Utils.LogError("An Exception occurred when checking for FAR's presence. Exception thrown: " + e.ToString()); }
            return false;
        }
    }
}
