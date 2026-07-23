using UnityEngine;

namespace Client.Common
{
    public class MaterialConst
    {
        public const string PlaceBlockAnimationMaterial = "PlaceBlockAnimation";
        
        public const string PreviewPlaceBlockMaterial = "PreviewPlaceBlock";

        private static Material _previewPlaceBlockMaterial;
        private static Material _placeBlockAnimationMaterial;

        // チュートリアル用プレビューマテリアルのAddressableパス
        // Tutorial preview block material addressable path
        public const string TutorialPreviewBlockMaterialPath = "Vanilla/Material/TutorialPreviewBlock";

        public const string PreviewColorPropertyName = "_PreviewColor";
        public static readonly Color PlaceableColor = new(0.41f,0.59f,0.86f,1f);
        public static readonly Color NotPlaceableColor = new(0.9f,0.25f,0.16f,1);

        public static Material GetPreviewPlaceBlockMaterial()
        {
            // 共通プレビュー材質は一度だけロードして再利用する
            // Load the shared preview material once and reuse it
            _previewPlaceBlockMaterial ??= Resources.Load<Material>(PreviewPlaceBlockMaterial);
            return _previewPlaceBlockMaterial;
        }

        public static Material GetPlaceBlockAnimationMaterial()
        {
            // 設置アニメーション材質も繰り返しロードしない
            // Avoid repeated resource loads for placement animation material
            _placeBlockAnimationMaterial ??= Resources.Load<Material>(PlaceBlockAnimationMaterial);
            return _placeBlockAnimationMaterial;
        }
    }
}
