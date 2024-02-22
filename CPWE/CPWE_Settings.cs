using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/*
namespace CPWE
{
    public class CPWE_Settings : GameParameters.CustomParameterNode
    {
        public override string Title => "CPWE";
        public override GameParameters.GameMode GameMode => GameParameters.GameMode.ANY;
        public override string Section => "CPWE";
        public override string DisplaySection => "CPWE";
        public override int SectionOrder => 1;
        public override bool HasPresets => false;

        [GameParameters.CustomFloatParameterUI("Global Wind Speed Multiplier", toolTip = "All wind speeds will be multiplied by this value.", maxValue = 5.0f, minValue = 0.0f, stepCount = 101, autoPersistance = true)]
        public float windmult = 1.0f;

        [GameParameters.CustomStringParameterUI("Longitude/Latitude Units", autoPersistance = true)]
        public string lonlatunits = "Degrees";

        [GameParameters.CustomStringParameterUI("Wind Direction Labels", autoPersistance = true)]
        public string dirunits = "N,NNE,NE,ENE,...";

        [GameParameters.CustomParameterUI("Developer Mode", toolTip = "If enabled, the GUI will display a bunch of raw information.", autoPersistance = true)]
        public bool DevMode = true;

        public override void SetDifficultyPreset(GameParameters.Preset preset)
        {
            switch (preset)
            {
                case GameParameters.Preset.Easy:
                    break;
                case GameParameters.Preset.Normal:
                    break;
                case GameParameters.Preset.Moderate:
                    break;
                case GameParameters.Preset.Hard:
                    break;
            }
        }

        public override IList ValidValues(MemberInfo member)
        {
            if(member.Name == "lonlatunits")
            {
                List<string> v = new List<string>
                {
                    "Degrees",
                    "Degrees, Minutes",
                    "Degrees, Minutes, Seconds"
                };
                IList list = v;
                return list;
            }
            if(member.Name == "dirunits")
            {
                List<string> v = new List<string>
                {
                    "N,NNE,NE,ENE,...",
                    "N,NE,E,SE,...",
                    "N,E,S,W"
                };
                IList list = v;
                return list;
            }
            return null;
        }
    }
}

*/