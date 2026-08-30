using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.ChainPreview;
using Core.Master;
using Game.Block.Interface;
using Mooresmaster.Model.ChallengesModule;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.Tutorial.PlacementGuide
{
    /// <summary>
    ///     チュートリアルの連結レイアウト定義を共有状態へ書き、完了で下ろす。判定と描画は設置システム側が担う
    ///     Writes the tutorial's chain layout into the shared state and clears it on completion; checks and rendering live in the placement system
    /// </summary>
    public class ChainBlockPlacePreviewTutorialManager : MonoBehaviour, ITutorialView, ITutorialViewManager
    {
        public string TutorialType => TutorialsElement.TutorialTypeConst.chainBlockPlacePreview;
        
        private ChainPlacePreviewState _state;
        private Guid _appliedTutorialGuid;
        
        [Inject]
        public void Construct(ChainPlacePreviewState state)
        {
            _state = state;
        }
        
        public ITutorialView ApplyTutorial(TutorialsElement tutorial)
        {
            var param = (ChainBlockPlacePreviewTutorialParam)tutorial.TutorialParam;
            _appliedTutorialGuid = tutorial.TutorialGuid;
            
            var anchorBlockId = MasterHolder.BlockMaster.GetBlockId(param.AnchorBlockGuid);
            var chain = new List<ChainGhost>();
            foreach (var element in param.ChainBlocks)
            {
                var blockId = MasterHolder.BlockMaster.GetBlockId(element.BlockGuid);
                chain.Add(new ChainGhost(blockId, element.Offset, Enum.Parse<BlockDirection>(element.BlockDirection)));
            }
            
            _state.SetChain(_appliedTutorialGuid, anchorBlockId, chain);
            return this;
        }
        
        public void CompleteTutorial()
        {
            _state.Clear(_appliedTutorialGuid);
        }
    }
}
