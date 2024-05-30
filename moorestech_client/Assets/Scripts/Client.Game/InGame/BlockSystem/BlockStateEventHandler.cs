using Client.Game.InGame.Chunk;
using Client.Game.InGame.Context;
using Game.Context;
using MessagePack;
using Server.Event.EventReceive;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.BlockSystem
{
    public class BlockStateEventHandler : IInitializable
    {
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        
        public BlockStateEventHandler(BlockGameObjectDataStore blockGameObjectDataStore)
        {
            _blockGameObjectDataStore = blockGameObjectDataStore;
            ClientContext.VanillaApi.Event.RegisterEventResponse(ChangeBlockStateEventPacket.EventTag, OnStateChange);
        }
        
        public void Initialize()
        {
        }
        
        private void OnStateChange(byte[] payload)
        {
            var data = MessagePackSerializer.Deserialize<ChangeBlockStateEventMessagePack>(payload);
            
            var pos = data.Position;
            if (!_blockGameObjectDataStore.BlockGameObjectDictionary.TryGetValue(pos, out var _))
            {
                Debug.Log("ブロックがない : " + pos);
            }
            else
            {
                var blockObject = _blockGameObjectDataStore.BlockGameObjectDictionary[pos];
                blockObject.BlockStateChangeProcessor.OnChangeState(data.CurrentState, data.PreviousState, data.CurrentStateData);
                
                var blockConfig = ServerContext.BlockConfig.GetBlockConfig(blockObject.BlockId);
            }
        }
    }
}