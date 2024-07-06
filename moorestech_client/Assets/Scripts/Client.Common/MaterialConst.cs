using UnityEngine;

namespace Client.Common
{
    public class MaterialConst
    {
        public const string PlaceBlockAnimationMaterial = "PlaceBlockAnimation";
        
        public const string PreviewPlaceBlockMaterial = "PreviewPlaceBlock";
        
        public static readonly Color PlaceableColor = new(0.41f,0.59f,0.86f,1f);
        public static readonly Color NotPlaceableColor = new(0.9f,0.25f,0.16f,1);
    }
}