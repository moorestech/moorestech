using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using MessagePack;
using UnityEngine;

namespace Game.Block.Blocks.TrainRail
{
    public class RailBridgePierComponent : IBlockStateDetail, IReceiveCreateParam
    {
        private Vector3 _railBlockDirection;
        
        public void OnCreate(BlockCreateParam[] blockCreateParams)
        {
            var state = blockCreateParams.GetStateDetail<RailBridgePierComponentStateDetail>(RailBridgePierComponentStateDetail.StateDetailKey);
            _railBlockDirection = state.RailBlockDirection.Vector3;
        }
        
        public BlockStateDetail[] GetBlockStateDetails()
        {
            var bytes = MessagePackSerializer.Serialize(new RailBridgePierComponentStateDetail(_railBlockDirection));
            
            return new BlockStateDetail[] {new (RailBridgePierComponentStateDetail.StateDetailKey, bytes)};
        }
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}