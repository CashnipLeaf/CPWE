# CPWE - Configurable Planetary Wind Effects

### This mod is under active development. Everything in here is subject to change.

CPWE is a mod for Kerbal Space Program designed to provide a framework for defining wind patterns for any celestial body, including custom/modded ones. 

In all the time I spent on flight simulators, wind was always a persistent challenge element, and I've wanted something similar in KSP. However, none of the (admittedly very few) existing wind mods for KSP had what I wanted. Namely, I wanted to be able to configure the wind based on location to allow for interesting wind patterns. At the same time, while I loved the depth that Kerbal Weather Project brought to the game, I felt that running a climate simulation for other celestial bodies might be a bridge too far (especially if we include custom bodies which may not necessarily be grounded in reality). 

CPWE is my attempt at filling the need I created for myself. It is designed to be relatively easy to configure and provides a lot of options for configuring wind patterns and prevailing winds. The structure of the config entries are included on this repository's wiki page. 

### API (Coming Soon)  
An API will be included to allow other mods to not only retrieve the wind vector that CPWE is using, but supply CPWE with their own wind vectors. Details on this are not yet finalized, but once the API is active, instructions on how to interface with it will be included in the Github wiki

### Planned Features (in order of priority):
- More advanced options for defining wind patterns, such as using float curves
- FAR compatibility
- Support for dynamic wind patterns (i.e. ones that can change with time)
- The aforementioned API
- Integration with other mods

### Installation:
1. Grab the latest release of CPWE from the Releases section, install the contents to your KSP install's GameData folder
2. Install dependencies:
- ModularFlightIntegrator: https://forum.kerbalspaceprogram.com/topic/106369-19-modularflightintegrator-127-19-october-2019/
- Toolbar Controller: https://github.com/linuxgurugamer/ToolbarControl/releases
- ClickThrough Blocker: https://github.com/linuxgurugamer/ClickThroughBlocker/releases

### Mod Compatibility  
**Recommended Mods:**
- Kopernicus
- KSPCommunityFixes
- ModuleManager

**Compatible With:**
- FerramAerospaceResearch (Planned) 
- Most, if not all parts mods

**Conflicts With:** 
- Other mods that modify the stock aerodynamics system

**Support:**  
As this is an alpha release, I fully expect there to be bugs and issues that I may have missed during testing. Please report these on the Issues tab.

**Credits and Acknowledgements (not necessarily in order of contribution):**
- @sarbian, @ferram4, and @Starwaster for making the Modular Flight Integrator that allows interfacing with KSP's physics system.
- @cmac994 for making Kerbal Weather Project (https://forum.kerbalspaceprogram.com/topic/199347-18x-111x-kerbal-weather-project-kwp-v100/), which was a big source of inspiration for this mod.

**License Information:**
- The source code, settings config, localization configs, and toolbar icons are licensed under the MIT license. (see the LICENSE file)
- All other configs and textures are licensed under the WTFPL license (http://www.wtfpl.net/)
