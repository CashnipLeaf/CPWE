using System;
using UnityEngine;
using ToolbarControl_NS;

namespace CPWE
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class RegisterToolbarController : MonoBehaviour
    {
        void Start()
        {
            try
            {
                if(ToolbarControl.RegisterMod(CPWE_UnityGUI.modID, CPWE_UnityGUI.modNAME))
                {
                    Utils.LogInfo("Successfully registered CPWE with the toolbar controller.");
                }
                else
                {
                    Utils.LogWarning("Unable to register CPWE with the toolbar. CPWE's UI will not be available.");
                }
            }
            catch(Exception e) 
            { 
                Utils.LogError("An Exception occurred when registering CPWE with the toolbar controller. Exception thrown: " + e.ToString()); 
            }
            Destroy(this);
        }
    }
}
