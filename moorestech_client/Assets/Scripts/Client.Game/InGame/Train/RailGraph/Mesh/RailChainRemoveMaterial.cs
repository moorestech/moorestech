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

        // 赤用の複製材質をレンダラーから外して解放し、参照も捨てる
        // Detach and destroy the red clone materials, then drop the reference
        public void Release()
        {
            _controller?.ResetMaterial();
            _controller?.DestroyMaterial();
            _controller = null;
        }

        public void Capture(GameObject railObject)
        {
            // 設置アニメ開始前に元の材質を確定する
            // Pin the original materials before placement animation starts
            _controller = new RendererMaterialReplacerController(railObject);
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
