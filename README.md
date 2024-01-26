# Configurable Planetary Wind Effects
This is a mod for Kerbal Space Program designed to provide a framework for defining wind patterns for any celestial body, including custom/modded ones. 

In all the time I spent on flight simulators, wind was always a persistent challenge element, and I've wanted something similar in KSP. However, none of the (admittedly very few) existing wind mods for KSP had what I wanted. Namely, I wanted to be able to configure the wind based on location to allow for interesting wind patterns rather than a purely randomized system. At the same time, while I loved the depth that Kerbal Weather Project brought to the game, I felt that running a climate simulation for the other planets might be a bridge too far. 

CPWE is my attempt at filling the need I created for myself. It is designed to be relatively easy to configure and provides a lot of options for configuring wind patterns and prevailing winds. The structure of the config entries all included on CPWE's github wiki page. 

**API**  
An API will be included to allow other mods to not only retrieve the wind vector that CPWE is using, but supply CPWE with their own wind vectors. Details on this are not yet finalized, but once the API is active, instructions on how to interface with it will be included in the Github wiki

NOTE: CPWE does NOT add any visual effects. However, it's likely possible to integrate this mod with one that does.

**Installation:**
1. Grab the latest release of CPWE from the Releases section, install the contents to your KSP install's GameData folder
2. Install dependencies:
- ModularFlightIntegrator: https://forum.kerbalspaceprogram.com/topic/106369-19-modularflightintegrator-127-19-october-2019/
- Toolbar Controller: https://github.com/linuxgurugamer/ToolbarControl/releases
- ClickThrough Blocker: https://github.com/linuxgurugamer/ClickThroughBlocker/releases

**Mod Compatibility**
- Recommended Mods:
> - Kopernicus
> - KSPCommunityFixes
> - ModuleManager 
- Integration with:
> - FerramAerospaceResearch (FAR)
- Compatible with:
> - Most, if not all parts mods
- Conflicts with:
> - Mods that modify the stock aerodynamics system

**Support:**  
As this is an alpha release, I fully expect there to be bugs and issues that I may have missed during testing. Please report these on the Issues tab.

**Credits and Acknowledgements (not necessarily in order of contribution):**
- @sarbian, @ferram4, and @Starwaster for making the Modular Flight Integrator that allows interfacing with KSP's physics system.
- @cmac994 for making Kerbal Weather Project (https://forum.kerbalspaceprogram.com/topic/199347-18x-111x-kerbal-weather-project-kwp-v100/), which was a big source of inspiration for this mod.

**License Information:**
- The source code, settings config, localization configs, and toolbar icons are licensed under the MIT license. (see the LICENSE file)
- All other configs and textures are licensed under the WTFPL license (http://www.wtfpl.net/)
