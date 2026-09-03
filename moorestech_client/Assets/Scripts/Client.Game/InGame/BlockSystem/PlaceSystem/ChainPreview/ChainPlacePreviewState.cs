using System;
using System.Collections.Generic;
using Core.Master;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ChainPreview
{
    /// <summary>
    ///     連結レイアウト全体が置けることを要求する共有状態
    ///     Shared state requiring the whole chain layout to fit; written by the tutorial, read by placement checks and the ghosts
    /// </summary>
    public class ChainPlacePreviewState
    {
        // 定義はチュートリアルごとに独立し完了した分だけ下りる
        // Each tutorial owns an independent definition; only its own completion removes it
        private readonly Dictionary<Guid, ChainDefinition> _definitions = new();
        
        public void SetChain(Guid tutorialGuid, BlockId placingBlockId, IReadOnlyList<ChainGhost> chain)
        {
            // 同一設置ブロックの定義が2件並ぶとTryGetChainの代表選択が辞書の列挙順任せになるので、適用時点で落とす
            // Two definitions for one placing block would leave TryGetChain's pick to dictionary order, so fail at apply time
            foreach (var pair in _definitions)
            {
                if (pair.Key == tutorialGuid) continue;
                if (pair.Value.PlacingBlockId != placingBlockId) continue;
                throw new InvalidOperationException($"Duplicate chain definition for BlockId:{placingBlockId} tutorials:{pair.Key} and {tutorialGuid}");
            }

            // 実体参照を漏らさないよう配列へ写す
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
        ///     連結対象なら一緒に置くゴースト群と定義元を返す
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
