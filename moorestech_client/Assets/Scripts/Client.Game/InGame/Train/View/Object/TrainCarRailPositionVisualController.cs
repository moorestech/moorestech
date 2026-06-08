using System;
using Client.Common;
using Client.Game.InGame.Block;
using UnityEngine;

namespace Client.Game.InGame.Train.View.Object
{
    public sealed class TrainCarRailPositionVisualController : ITrainCarVisualTarget
    {
        private readonly RendererMaterialReplacerController _materialController;
        private readonly TrainCarRailPositionVisualApplier _visualApplier;
        private TrainCarVisualMaterialMode _baseMaterialMode = TrainCarVisualMaterialMode.Normal;
        private TrainCarVisualMaterialMode _overlayMaterialMode = TrainCarVisualMaterialMode.Normal;
        private TrainCarVisualMaterialMode _appliedMaterialMode = TrainCarVisualMaterialMode.Normal;
        private int _overlayFrame = -1;

        public TrainCarRailPositionVisualController(GameObject targetObject)
        {
            // 対象オブジェクト構造からmaterial管理とrailposition描画実装を一度だけ組み立てる
            // Build material handling and railposition visual implementation once from the target object structure
            _materialController = new RendererMaterialReplacerController(targetObject);
            _visualApplier = ResolveVisualApplier(targetObject);
        }

        public bool UpdateVisual(TrainCarRailPositionVisualState visualState)
        {
            // 1フレームoverlayの期限を確認してからrailposition描画を行う
            // Expire one-frame overlays before drawing the railposition state
            RefreshOneFrameOverlay();
            return _visualApplier.ApplyVisualState(visualState);
        }

        public void SetMaterialMode(TrainCarVisualMaterialMode materialMode)
        {
            // previewや削除表示など継続するmaterial状態を指定する
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

        private static TrainCarRailPositionVisualApplier ResolveVisualApplier(GameObject targetObject)
        {
            var visualApplier = targetObject.GetComponent<TrainCarRailPositionVisualApplier>();
            if (visualApplier == null)
            {
                throw new InvalidOperationException($"TrainCarRailPositionVisualApplier is missing on train root. Object:{targetObject.name}");
            }

            // Prefab rootに置かれた再帰applierをrailposition描画の入口にする
            // Use the recursive applier on the Prefab root as the railposition drawing entry point
            return visualApplier;
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

            // 次フレームまで要求が続かなければ継続material状態へ戻す
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

            // 通常表示ではshared materialへ戻し、runtime materialを破棄する
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
