using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.VeinRestriction;
using Core.Master;
using Mooresmaster.Model.ChallengesModule;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.Tutorial.PlacementGuide
{
    /// <summary>
    ///     チャレンジ中だけ「このブロックはこの鉱脈にしか置けない」制限を共有状態へ書く。表示と判定は設置側が読む
    ///     Writes the "this block only on this vein" restriction into the shared state for the challenge's lifetime; placement reads it
    /// </summary>
    public class VeinRestrictedPlacementTutorialManager : MonoBehaviour, ITutorialView, ITutorialViewManager
    {
        public string TutorialType => TutorialsElement.TutorialTypeConst.veinRestrictedPlacement;

        private VeinRestrictedPlacementState _state;
        private Guid _appliedTutorialGuid;

        [Inject]
        public void Construct(VeinRestrictedPlacementState state)
        {
            _state = state;
        }

        public ITutorialView ApplyTutorial(TutorialsElement tutorial)
        {
            var param = (VeinRestrictedPlacementTutorialParam)tutorial.TutorialParam;
            var blockId = MasterHolder.BlockMaster.GetBlockId(param.BlockGuid);
            _appliedTutorialGuid = tutorial.TutorialGuid;
            _state.SetRestriction(_appliedTutorialGuid, param.VeinGuid, blockId);
            return this;
        }

        public void CompleteTutorial()
        {
            _state.Clear(_appliedTutorialGuid);
        }
    }
}
