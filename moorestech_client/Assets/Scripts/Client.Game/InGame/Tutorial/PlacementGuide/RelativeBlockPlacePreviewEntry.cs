using System;
using Core.Master;
using Game.Block.Interface;
using Mooresmaster.Model.ChallengesModule;
using UnityEngine;

namespace Client.Game.InGame.Tutorial.PlacementGuide
{
    /// <summary>
    ///     相対ゴースト1件分の状態。完了はmanager経由で自分のエントリだけを畳む
    ///     State for one relative ghost; completing it folds only this entry via the manager
    /// </summary>
    public class RelativeBlockPlacePreviewEntry : ITutorialView
    {
        public Guid TutorialGuid { get; }
        public string TutorialGuidString { get; }
        public Guid AnchorBlockGuid { get; }
        public BlockId TargetBlockId { get; }
        public BlockDirection LocalDirection { get; }
        public Vector3Int Offset { get; }
        public Vector3Int TargetBlockSize { get; }
        public Vector3Int? TargetCell { get; private set; }
        
        private readonly RelativeBlockPlacePreviewTutorialManager _manager;
        
        public RelativeBlockPlacePreviewEntry(TutorialsElement tutorial, RelativeBlockPlacePreviewTutorialManager manager)
        {
            var param = (RelativeBlockPlacePreviewTutorialParam)tutorial.TutorialParam;
            TutorialGuid = tutorial.TutorialGuid;
            TutorialGuidString = tutorial.TutorialGuid.ToString("D");
            AnchorBlockGuid = param.AnchorBlockGuid;
            TargetBlockId = MasterHolder.BlockMaster.GetBlockId(param.BlockGuid);
            LocalDirection = Enum.Parse<BlockDirection>(param.BlockDirection);
            Offset = param.Offset;
            TargetBlockSize = MasterHolder.BlockMaster.GetBlockMaster(TargetBlockId).BlockSize;
            _manager = manager;
        }
        
        public void SetTargetCell(Vector3Int? targetCell)
        {
            TargetCell = targetCell;
        }
        
        public void CompleteTutorial()
        {
            _manager.Complete(TutorialGuid);
        }
    }
}
