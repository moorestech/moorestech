using UnityEngine;

namespace Client.Common
{
    public class MaterialConst
    {
        public const string PlaceBlockAnimationMaterial = "PlaceBlockAnimation";
        
        public const string PreviewPlaceBlockMaterial = "PreviewPlaceBlock";

        // チュートリアル用プレビューマテリアルのAddressableパス
        // Tutorial preview block material addressable path
        public const string TutorialPreviewBlockMaterialPath = "Vanilla/Material/TutorialPreviewBlock";

        public const string PreviewColorPropertyName = "_PreviewColor";
        public static readonly Color PlaceableColor = new(0.41f,0.59f,0.86f,1f);
        public static readonly Color NotPlaceableColor = new(0.9f,0.25f,0.16f,1);
    }
}