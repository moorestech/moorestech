using System;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Game.Block.Blocks.TrainRail;
using Server.Event.EventReceive;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor
{
    public class TrainRailStateChangeProcessor : MonoBehaviour, IBlockStateChangeProcessor, IBlockPreviewStateProcessor
    {
        [SerializeField] private Transform railModel;
        
        public void Initialize(BlockGameObject blockGameObject) { }
        
        public void OnChangeState(BlockStateMessagePack blockState)
        {
            Process(blockState.CurrentStateDetail);
        }
        
        public void SetPreviewStateDetail(PreviewPlaceInfo previewPlaceInfo)
        {
            Process(previewPlaceInfo.CurrentStateDetail);
        }
        
        private void Process(Dictionary<string, byte[]> stateDetails)
        {
            var railState = stateDetails.GetStateDetail<RailComponentStateDetail>(RailComponentStateDetail.StateDetailKey);
            
            var railVector = railState.RailBlockDirection.Vector3;
            railModel.localRotation = Quaternion.LookRotation(railVector);
        }
    }
}