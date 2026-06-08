using System;
using Client.Common;
using Client.Game.InGame.Block;
using UnityEngine;

namespace Client.Game.InGame.Train.View.Object
{
    public sealed class TrainCarRailPositionVisualController : ITrainCarVisualTarget
    {
        private readonly RendererMaterialReplacerController _materialController;
        private readonly TrainCarRailPositionVisualPoseUpdater _visualPoseUpdater;
        private TrainCarVisualMaterialMode _baseMaterialMode = TrainCarVisualMaterialMode.Normal;
        private TrainCarVisualMaterialMode _overlayMaterialMode = TrainCarVisualMaterialMode.Normal;
        private TrainCarVisualMaterialMode _appliedMaterialMode = TrainCarVisualMaterialMode.Normal;
        private int _overlayFrame = -1;

        public TrainCarRailPositionVisualController(GameObject targetObject)
        {
            // 対象オブジェクト構造から material 管理と railposition pose 更新入口を組み立てる
            // Build material handling and the railposition pose update entry point from the target object structure
            _materialController = new RendererMaterialReplacerController(targetObject);
            _visualPoseUpdater = ResolveVisualPoseUpdater(targetObject);
        }

        public bool UpdateVisual(TrainCarRailPositionVisualState visualState)
        {
            // 1フレーム overlay の期限を確認してから railposition pose を更新する
            // Expire one-frame overlays before updating the railposition pose
            RefreshOneFrameOverlay();
            return _visualPoseUpdater.UpdatePose(visualState);
        }

        public void SetMaterialMode(TrainCarVisualMaterialMode materialMode)
        {
            // preview や削除表示など継続する material 状態を指定する
            // Set persistent material state for preview and removal views
            _baseMaterialMode = materialMode;
            ApplyMaterialMode(ResolveEffectiveMaterialMode());
        }

        public void RequestOneFrameOverlay(TrainCarVisualMaterialMode materialMode)
        {
            // 既存車両の一時ハイライトを要求されたフレームだけ有効にする
            // Keep existing-car temporary highlights active only for the requested frame
            _overlayMaterialMode = materialMode;
            _overlayFrame = Time.frameCount;
            ApplyMaterialMode(ResolveEffectiveMaterialMode());
        }

        public void DestroyRuntimeMaterials()
        {
            _materialController.DestroyMaterial();
        }

        private static TrainCarRailPositionVisualPoseUpdater ResolveVisualPoseUpdater(GameObject targetObject)
        {
            var visualPoseUpdater = targetObject.GetComponent<TrainCarRailPositionVisualPoseUpdater>();
            if (visualPoseUpdater == null)
            {
                throw new InvalidOperationException($"TrainCarRailPositionVisualPoseUpdater is missing on train root. Object:{targetObject.name}");
            }

            // Prefab root の pose updater を railposition 入力の描画入口にする
            // Use the pose updater on the Prefab root as the railposition drawing entry point
            return visualPoseUpdater;
        }

        private void RefreshOneFrameOverlay()
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
            if (_appliedMaterialMode == materialMode)
            {
                return;
            }
            _appliedMaterialMode = materialMode;

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
