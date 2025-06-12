using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Network.API;
using MessagePack;
using Server.Event.EventReceive;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.BlockSystem
{
    /// <summary>
    /// This class now acts as a bridge between GameStateManager and BlockGameObjectDataStore.
    /// It observes block state changes from GameStateManager and updates the visual representation.
    /// </summary>
    public class BlockStateEventHandler : IPostStartable
    {
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        
        public BlockStateEventHandler(BlockGameObjectDataStore blockGameObjectDataStore)
        {
            _blockGameObjectDataStore = blockGameObjectDataStore;
            
            // Note: Block state events are now handled by GameStateManager.Blocks
            // This handler only needs to listen for state changes and update visual processors
            ClientContext.VanillaApi.Event.SubscribeEventResponse(ChangeBlockStateEventPacket.EventTag,
                payload =>
                {
                    var data = MessagePackSerializer.Deserialize<BlockStateMessagePack>(payload);
                    UpdateVisualState(data);
                });
        }
        
        public void PostStart()
        {
            // Initial states are now handled by GameStateManager during initialization
            // We only need to ensure visual processors are ready
        }
        
        private void UpdateVisualState(BlockStateMessagePack state)
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