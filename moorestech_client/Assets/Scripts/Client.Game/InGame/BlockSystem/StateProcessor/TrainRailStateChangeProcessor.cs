using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
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
        
        public void SetPreviewStateDetail(PlaceInfo placeInfo)
        {
            // CreateParamsからDictionaryに変換
            // Convert CreateParams to Dictionary
            Process(placeInfo.CreateParamDictionary);
        }
        
        private void Process(Dictionary<string, byte[]> stateDetails)
        {
            var railState = stateDetails.GetStateDetail<RailComponentStateDetail>(RailComponentStateDetail.StateDetailKey);
            
            var railVector = railState.RailBlockDirection.Vector3;
            railModel.localRotation = Quaternion.LookRotation(railVector);
        }
    }
}