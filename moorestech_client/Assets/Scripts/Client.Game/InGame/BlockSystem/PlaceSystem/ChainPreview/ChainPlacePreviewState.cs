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
        // チュートリアルごとに独立した定義を持ち、完了した本人の分だけが下りる
        // Each tutorial owns an independent definition; only its own completion removes it
        private readonly Dictionary<Guid, ChainDefinition> _definitions = new();
        
        public void SetChain(Guid tutorialGuid, BlockId placingBlockId, IReadOnlyList<ChainGhost> chain)
        {
            // 実体参照を外へ漏らさないよう配列へ写して凍結する
            // Copy into an array so the live list never leaks outside
            var frozen = new ChainGhost[chain.Count];
            for (var i = 0; i < chain.Count; i++) frozen[i] = chain[i];
            _definitions[tutorialGuid] = new ChainDefinition(placingBlockId, frozen);
        }
        
        public void Clear(Guid tutorialGuid)
        {
            _definitions.Remove(tutorialGuid);
        }
        
        /// <summary>
        ///     そのブロックが連結対象なら、一緒に置くべきゴースト群と定義元チュートリアルを返す
        ///     Returns the chain ghosts and their owning tutorial when the held block anchors a chain layout
        /// </summary>
        public bool TryGetChain(BlockId holdingBlockId, out IReadOnlyList<ChainGhost> chain, out Guid tutorialGuid)
        {
            foreach (var pair in _definitions)
            {
                if (pair.Value.PlacingBlockId != holdingBlockId) continue;
                chain = pair.Value.Ghosts;
                tutorialGuid = pair.Key;
                return true;
            }
            
            chain = Array.Empty<ChainGhost>();
            tutorialGuid = Guid.Empty;
            return false;
        }
        
        private readonly struct ChainDefinition
        {
            public readonly BlockId PlacingBlockId;
            public readonly ChainGhost[] Ghosts;
            
            public ChainDefinition(BlockId placingBlockId, ChainGhost[] ghosts)
            {
                PlacingBlockId = placingBlockId;
                Ghosts = ghosts;
            }
        }
    }
}
