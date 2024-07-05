using UnityEngine;

namespace Client.Common
{
    public class MaterialConst
    {
        public const string PlaceBlockAnimationMaterial = "PlaceBlockAnimation";
        
        public const string PreviewPlaceBlockMaterial = "PreviewPlaceBlock";
        
        public static readonly Color PlaceableColor = new(0.9f,0.25f,0.16f,1);
        public static readonly Color NotPlaceableColor = new(0.51f,0.54f,0.86f,1f);
    }
}