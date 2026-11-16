using System.Collections.Generic;
using Client.Common.Asset;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using UnityEngine;

namespace Client.Game.InGame.Block.Preview.PlacementFrame
{
    /// <summary>
    ///     設置モードの枠の辺を使い回す。数は減らさず、空きが無ければ作る
    ///     Reuses the build-mode frame edges; never shrinks and creates one when none is idle
    /// </summary>
    public sealed class BlockPreviewEdgeFramePool
    {
        private const string EdgeFrameAddressablePath = "Vanilla/Block/Util/BlockPreviewBoundingBox";

        private readonly Stack<BlockPreviewBoundingBox> _idleFrames = new();
        private Transform _poolRoot;
        private GameObject _edgeFramePrefab;

        public BlockPreviewBoundingBox Rent(BlockGameObject owner)
        {
            var frame = _idleFrames.Count > 0 ? _idleFrames.Pop() : CreateFrame();

            // ブロックの姿勢と占有範囲に合わせる。ブロックは動かないので貸す時だけでよい
            // Match the block pose and footprint; blocks never move, so only on rent
            var ownerTransform = owner.transform;
            frame.transform.SetPositionAndRotation(ownerTransform.position, ownerTransform.rotation);
            frame.transform.localScale = ownerTransform.lossyScale;
            frame.SetBoundingBox(owner.BlockMasterElement.BlockSize);
            frame.gameObject.SetActive(true);
            return frame;

            #region Internal

            BlockPreviewBoundingBox CreateFrame()
            {
                // 初回の貸し出しでプレハブを1度だけ読み、親を作る
                // Load the prefab once and create the parent on the first rent
                if (_poolRoot == null)
                {
                    _poolRoot = new GameObject("BlockPreviewEdgeFramePool").transform;
                    _edgeFramePrefab = AddressableLoader.LoadDefault<GameObject>(EdgeFrameAddressablePath);
                }
                return Object.Instantiate(_edgeFramePrefab, _poolRoot).GetComponent<BlockPreviewBoundingBox>();
            }

            #endregion
        }

        public void Return(BlockPreviewBoundingBox frame)
        {
            // silent-ok: シーン破棄でプールの枠が先に壊れた時だけ起きる / 終了処理中で以後の貸し出しは無い
            // Only happens when scene teardown destroyed the frame first; no rent follows during shutdown
            if (frame == null) return;
            frame.gameObject.SetActive(false);
            _idleFrames.Push(frame);
        }
    }
}
