using Client.Game.InGame.Block;
using Client.Game.InGame.FluidNetwork;
using Core.Master;
using UnityEngine;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    /// <summary>
    ///     開いたパイプの中身の変化を検出する
    ///     パイプの中身はブロック状態イベントで届かずクライアント側のコピーが毎tick進めるので、連続値と同じ間隔で見て、量か流体の種類が前回の配信から変わった回だけ出し直す
    ///     Detects changes of the open pipe's contents (amount and fluid id) against the last published values
    ///     Pipe contents no longer arrive as block state events but advance in the client-side copy every tick, so they are sampled at the continuous-sample interval and republished only when the amount or fluid id changed since the last publish
    /// </summary>
    internal class OpenPipeContentsTracker
    {
        private readonly FluidNetworkClientCache _fluidCache;
        private bool _tracking;
        private Vector3Int _pipePosition;
        private long _publishedPipeAmount;
        private FluidId _publishedPipeFluidId;

        internal OpenPipeContentsTracker(FluidNetworkClientCache fluidCache)
        {
            _fluidCache = fluidCache;
        }

        // 追跡するブロックを切り替える。nullで追跡をやめる
        // Switch the tracked block; null stops tracking
        internal void Track(BlockGameObject block)
        {
            if (block == null)
            {
                _tracking = false;
                return;
            }

            // 開いた時点でコピーに載っていないパイプは、載るまで中身を出さない(調べ直さない)。載っていれば今の中身を配信済みとして控え、最初の配信は通常の開く経路に任せる
            // A pipe not yet in the copy when opened shows no contents until reopened (no re-check); when present, the current contents count as published and the first publish goes through the normal open path
            _pipePosition = block.BlockPosInfo.OriginalPos;
            _tracking = _fluidCache.TryGetNodeIndex(_pipePosition, out var pipeNode);
            if (!_tracking) return;
            _publishedPipeAmount = _fluidCache.Replica.Network.NodeAmounts[pipeNode];
            _publishedPipeFluidId = _fluidCache.Replica.Network.NodeFluidIds[pipeNode];
        }

        // 複製の量・流体IDが変われば記録しtrue
        // Record changed replica amount/fluid ID and return true; otherwise return false
        internal bool HasChangedSincePublish()
        {
            if (!_tracking || !_fluidCache.TryGetNodeIndex(_pipePosition, out var pipeNode)) return false;
            var network = _fluidCache.Replica.Network;
            var amount = network.NodeAmounts[pipeNode];
            var fluidId = network.NodeFluidIds[pipeNode];
            if (amount == _publishedPipeAmount && fluidId == _publishedPipeFluidId) return false;
            _publishedPipeAmount = amount;
            _publishedPipeFluidId = fluidId;
            return true;
        }
    }
}
