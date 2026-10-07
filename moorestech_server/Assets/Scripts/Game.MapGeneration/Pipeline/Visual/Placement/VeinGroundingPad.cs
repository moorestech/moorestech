using UnityEngine;

namespace Game.MapGeneration.Pipeline.Visual.Placement
{
    internal readonly struct VeinGroundingPad
    {
        public readonly Rect Core;
        public readonly float HeightMeters;
        public readonly float BlendWidth;

        public VeinGroundingPad(Rect core, float heightMeters, float blendWidth)
        {
            Core = core;
            HeightMeters = heightMeters;
            BlendWidth = blendWidth;
        }
    }
}
