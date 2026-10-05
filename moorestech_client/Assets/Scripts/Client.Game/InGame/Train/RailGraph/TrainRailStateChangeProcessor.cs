using System.Collections.Generic;
using System;
using Client.Game.InGame.Block;
using Client.Game.InGame.Block.Removal;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Train.SaveLoad;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.Train.RailGraph
{
    public class TrainRailStateChangeProcessor : MonoBehaviour, IBlockStateChangeProcessor, IBlockPreviewStateProcessor, IBlockRecreateParamSource, IBlockRemovalCascadeSource
    {
        [SerializeField] private Transform railModel;
        private BlockGameObject _blockGameObject;
        private byte[] _latestStateDetailBytes;
        
        public void Initialize(BlockGameObject blockGameObject)
        {
            _blockGameObject = blockGameObject;
        }

        public void CollectConnectionDestinations(List<ConnectionDestination> destinations)
        {
            var origin = _blockGameObject.BlockPosInfo.OriginalPos;
            destinations.Add(new ConnectionDestination(origin, 0, true));
            destinations.Add(new ConnectionDestination(origin, 0, false));
        }
        
        public void OnChangeState(BlockStateMessagePack blockState)
        {
            Process(blockState.CurrentStateDetail);

            // 適用済みの最新状態を撤去時の生成値に保持
            // Retain the latest applied state as the creation value on removal
            _latestStateDetailBytes = blockState.CurrentStateDetail.TryGetValue(RailBridgePierComponentStateDetail.StateDetailKey, out var bytes)
                ? (byte[])bytes.Clone()
                : null;
        }

        public bool TryGetBlockRecreateParams(out BlockCreateParam[] createParams)
        {
            // 初期状態の到着前はfalseを返す
            // Return false before the initial state arrives
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
