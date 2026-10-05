using System.Collections.Generic;
using System;
using Client.Game.InGame.Block;
using Client.Game.InGame.Block.Removal;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.Train.RailGraph
{
    public class TrainRailStateChangeProcessor : MonoBehaviour, IBlockStateChangeProcessor, IBlockPreviewStateProcessor, IBlockRecreateParamSource
    {
        [SerializeField] private Transform railModel;
        private byte[] _latestStateDetailBytes;
        
        public void Initialize(BlockGameObject blockGameObject) { }
        
        public void OnChangeState(BlockStateMessagePack blockState)
        {
            Process(blockState.CurrentStateDetail);

            // 表示に適用できた最新の状態を撤去時の生成値として保持する
            // Retain the latest applied state as the creation value captured on removal
            _latestStateDetailBytes = blockState.CurrentStateDetail.TryGetValue(RailBridgePierComponentStateDetail.StateDetailKey, out var bytes)
                ? (byte[])bytes.Clone()
                : null;
        }

        public bool TryGetBlockRecreateParams(out BlockCreateParam[] createParams)
        {
            // 初期状態の到着前は記録不能として false を返す
            // Before initial state arrives, return false so the caller records a failed capture
            createParams = null;
            if (_latestStateDetailBytes == null) return false;
            createParams = new[] { new BlockCreateParam(RailBridgePierComponentStateDetail.StateDetailKey, (byte[])_latestStateDetailBytes.Clone()) };
            return true;
        }
        
        public void SetPreviewStateDetail(PlaceInfo placeInfo)
        {
            // CreateParamsからDictionaryに変換
            // Convert CreateParams to Dictionary
            Process(placeInfo.CreateParamDictionary);
        }
        
        private void Process(Dictionary<string, byte[]> stateDetails)
        {
            var railState = stateDetails.GetStateDetail<RailBridgePierComponentStateDetail>(RailBridgePierComponentStateDetail.StateDetailKey);
            
            var railVector = railState.RailBlockDirection.Vector3;
            railModel.localRotation = Quaternion.LookRotation(railVector);
        }
    }
}
