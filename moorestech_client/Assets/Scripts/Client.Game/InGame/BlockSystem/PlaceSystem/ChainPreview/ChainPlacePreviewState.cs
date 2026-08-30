using System;
using System.Collections.Generic;
using Core.Master;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ChainPreview
{
    /// <summary>
    ///     「このブロックを置くときは連結レイアウト全体が置けること」という共有状態。書き手はチュートリアル、読み手は設置判定と連結ゴースト表示
    ///     Shared "placing this block requires the whole chain layout to fit" state; written by the tutorial, read by placement checks and the chain ghosts
    /// </summary>
    public class ChainPlacePreviewState
    {
        // 制限を入れたチュートリアル。解除は入れた本人だけに許し、入れ替わり時の取り違えを防ぐ
        // The tutorial that set the chain; only it may clear, so an overlapping tutorial cannot drop someone else's
        private Guid? _ownerTutorialGuid;
        private BlockId _anchorBlockId;
        private readonly List<ChainGhost> _chain = new();
        
        public void SetChain(Guid tutorialGuid, BlockId anchorBlockId, IReadOnlyList<ChainGhost> chain)
        {
            _ownerTutorialGuid = tutorialGuid;
            _anchorBlockId = anchorBlockId;
            _chain.Clear();
            _chain.AddRange(chain);
        }
        
        public void Clear(Guid tutorialGuid)
        {
            if (_ownerTutorialGuid != tutorialGuid) return;
            
            _ownerTutorialGuid = null;
            _chain.Clear();
        }
        
        /// <summary>
        ///     そのブロックが連結対象なら、一緒に置くべきゴースト群を返す
        ///     Returns the chain ghosts when the held block anchors a chain layout
        /// </summary>
        public bool TryGetChain(BlockId holdingBlockId, out IReadOnlyList<ChainGhost> chain)
        {
            chain = _chain;
            return _ownerTutorialGuid.HasValue && _anchorBlockId == holdingBlockId;
        }
    }
}
