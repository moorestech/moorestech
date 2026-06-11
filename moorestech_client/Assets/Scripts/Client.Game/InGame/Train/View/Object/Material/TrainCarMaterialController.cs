using Client.Common;
using Client.Game.InGame.Block;
using UnityEngine;

namespace Client.Game.InGame.Train.View.Object.Material
{
    public sealed class TrainCarMaterialController
    {
        private readonly RendererMaterialReplacerController _materialController;
        private TrainCarVisualMaterialMode _baseMaterialMode = TrainCarVisualMaterialMode.Normal;
        private TrainCarVisualMaterialMode _overlayMaterialMode = TrainCarVisualMaterialMode.Normal;
        private int _overlayFrame = -1;

        public TrainCarMaterialController(GameObject targetObject)
        {
            // 対象オブジェクト配下の renderer material 管理を組み立てる
            // Build renderer material handling from the target object structure
            _materialController = new RendererMaterialReplacerController(targetObject);
        }

        public void SetMaterialMode(TrainCarVisualMaterialMode materialMode)
        {
            // preview や削除表示など継続する material 状態を指定する
            // Set persistent material state for preview and removal views
            _baseMaterialMode = materialMode;
            ApplyMaterialMode(ResolveEffectiveMaterialMode());
        }

        public void RequestOverlayForCurrentFrame(TrainCarVisualMaterialMode materialMode)
        {
            // 既存車両の一時ハイライト要求を現在フレームに記録する
            // Record an existing-car temporary highlight request for the current frame
            _overlayMaterialMode = materialMode;
            _overlayFrame = Time.frameCount;
            ApplyMaterialMode(ResolveEffectiveMaterialMode());
        }

        public void RefreshCurrentFrameOverlay()
        {
            if (_overlayFrame == Time.frameCount)
            {
                return;
            }
            if (_overlayMaterialMode == TrainCarVisualMaterialMode.Normal)
            {
                return;
            }

            // 次フレームまで要求が続かなければ継続 material 状態へ戻す
            // Return temporary highlights to the persistent material state when no frame request remains
            _overlayMaterialMode = TrainCarVisualMaterialMode.Normal;
            ApplyMaterialMode(ResolveEffectiveMaterialMode());
        }

        public void DestroyRuntimeMaterials()
        {
            _materialController.DestroyMaterial();
        }

        private TrainCarVisualMaterialMode ResolveEffectiveMaterialMode()
        {
            if (_overlayFrame == Time.frameCount && _overlayMaterialMode != TrainCarVisualMaterialMode.Normal)
            {
                return _overlayMaterialMode;
            }
            return _baseMaterialMode;
        }

        private void ApplyMaterialMode(TrainCarVisualMaterialMode materialMode)
        {
            // 通常表示では shared material へ戻し、runtime material を破棄する
            // Normal rendering restores shared materials and destroys runtime materials
            if (materialMode == TrainCarVisualMaterialMode.Normal)
            {
                _materialController.ResetMaterial();
                return;
            }

            _materialController.CopyAndSetMaterial(MaterialConst.GetPreviewPlaceBlockMaterial());
            _materialController.SetColor(MaterialConst.PreviewColorPropertyName, ResolveMaterialColor(materialMode));
        }

        private static Color ResolveMaterialColor(TrainCarVisualMaterialMode materialMode)
        {
            return materialMode switch
            {
                TrainCarVisualMaterialMode.PlacementPreviewNotPlaceable => MaterialConst.NotPlaceableColor,
                TrainCarVisualMaterialMode.RemovePreview => MaterialConst.NotPlaceableColor,
                _ => MaterialConst.PlaceableColor,
            };
        }
    }
}
