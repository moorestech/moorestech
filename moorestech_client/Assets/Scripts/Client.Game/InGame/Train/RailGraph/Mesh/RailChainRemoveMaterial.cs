using Client.Common;
using Client.Game.InGame.Block;
using UnityEngine;

namespace Client.Game.InGame.Train.RailGraph
{
    // レール撤去プレビューの材質とGPU色を管理する
    // Manage material and GPU color for rail removal preview
    public sealed class RailChainRemoveMaterial
    {
        private RendererMaterialReplacerController _controller;

        public void Invalidate()
        {
            _controller = null;
        }

        public void SetRed(GameObject railObject, bool useGpuDeform, BezierRailChainSegments segments)
        {
            if (useGpuDeform)
            {
                segments.SetPreviewColor(MaterialConst.NotPlaceableColor);
                return;
            }

            // Rebuildの早期終了後でも必要時に置換器を作る
            // Create the replacer on demand even after an early Rebuild return
            _controller ??= new RendererMaterialReplacerController(railObject);
            _controller.CopyAndSetMaterial(MaterialConst.GetPreviewPlaceBlockMaterial());
            _controller.SetColor(MaterialConst.PreviewColorPropertyName, MaterialConst.NotPlaceableColor);
        }

        public void Reset(bool useGpuDeform, Color originalColor, BezierRailChainSegments segments)
        {
            if (useGpuDeform) segments.SetPreviewColor(originalColor);
            else _controller?.ResetMaterial();
        }
    }
}
