using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Network.API;
using MessagePack;
using Server.Event.EventReceive;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.BlockSystem
{
    public class BlockStateEventHandler : IPostStartable
    {
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        private readonly InitialHandshakeResponse _initialHandshakeResponse;
        
        public BlockStateEventHandler(BlockGameObjectDataStore blockGameObjectDataStore, InitialHandshakeResponse initialHandshakeResponse)
        {
            _blockGameObjectDataStore = blockGameObjectDataStore;
            _initialHandshakeResponse = initialHandshakeResponse;
            
            ClientContext.VanillaApi.Event.SubscribeEventResponse(ChangeBlockStateEventPacket.EventTag,
                payload =>
                {
                    var data = MessagePackSerializer.Deserialize<BlockStateMessagePack>(payload);
                    ChangeState(data);
                });
        }
        
        public void PostStart()
        {
            foreach (var state in _initialHandshakeResponse.BlockStates) ChangeState(state);
        }
        
        private void ChangeState(BlockStateMessagePack state)
        {
            var pos = state.Position;
            if (!_blockGameObjectDataStore.BlockGameObjectDictionary.TryGetValue(pos, out var _))
            {
                Debug.Log("ブロックがない : " + pos);
            }
            else
            {
                var blockObject = _blockGameObjectDataStore.BlockGameObjectDictionary[pos];
                foreach (var processor in blockObject.BlockStateChangeProcessors)
                {
                    processor.OnChangeState(state);
                }
            }
        }
    }
}