using UnityEngine;

namespace Client.Common
{
    public class MaterialConst
    {
        public const string PlaceBlockAnimationMaterial = "PlaceBlockAnimation";
        
        public const string PreviewPlaceBlockMaterial = "PreviewPlaceBlock";

        // 半透明版はアセットとして持つ。実行時にキーワードで透過へ切り替えるとビルドでバリアントが焼かれない
        // The translucent variant lives as an asset; flipping the keyword at runtime leaves the build without that variant
        public const string PreviewPlaceBlockTransparentMaterial = "PreviewPlaceBlockTransparent";

        // インタラクト対象のアウトライン材質（ステンシル方式。URPのOutlinePassが描く）
        // Outline material for interact targets (stencil based, drawn by the URP OutlinePass)
        public const string InteractOutlineMaterial = "InteractOutline";

        private static Material _previewPlaceBlockMaterial;
        private static Material _previewPlaceBlockTransparentMaterial;
        private static Material _placeBlockAnimationMaterial;
        private static Material _interactOutlineMaterial;

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

        public static Material GetPreviewPlaceBlockTransparentMaterial()
        {
            // 半透明プレビュー材質も一度だけロードして再利用する
            // Load the translucent preview material once and reuse it
            _previewPlaceBlockTransparentMaterial ??= Resources.Load<Material>(PreviewPlaceBlockTransparentMaterial);
            return _previewPlaceBlockTransparentMaterial;
        }

        public static Material GetPlaceBlockAnimationMaterial()
        {
            // 設置アニメーション材質も繰り返しロードしない
            // Avoid repeated resource loads for placement animation material
            _placeBlockAnimationMaterial ??= Resources.Load<Material>(PlaceBlockAnimationMaterial);
            return _placeBlockAnimationMaterial;
        }

        public static Material GetInteractOutlineMaterial()
        {
            // インタラクトアウトライン材質も一度だけロードして再利用する
            // Load the interact outline material once and reuse it
            _interactOutlineMaterial ??= Resources.Load<Material>(InteractOutlineMaterial);
            return _interactOutlineMaterial;
        }
    }
}
