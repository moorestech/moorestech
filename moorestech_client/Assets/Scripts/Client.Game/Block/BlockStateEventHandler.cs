using Client.Game.Context;
using Client.Network.API;
using Game.Block.Interface.BlockConfig;
using MainGame.UnityView.Chunk;
using MessagePack;
using Server.Event.EventReceive;
using ServerServiceProvider;
using UnityEngine;
using VContainer.Unity;

namespace MainGame.Presenter.Block
{
    public class BlockStateEventHandler : IInitializable
    {
        private readonly IBlockConfig _blockConfig;

        private readonly ChunkBlockGameObjectDataStore _chunkBlockGameObjectDataStore;


        public BlockStateEventHandler(ChunkBlockGameObjectDataStore chunkBlockGameObjectDataStore, MoorestechServerServiceProvider moorestechServerServiceProvider)
        {
            _blockConfig = moorestechServerServiceProvider.BlockConfig;
            _chunkBlockGameObjectDataStore = chunkBlockGameObjectDataStore;
            MoorestechContext.VanillaApi.Event.RegisterEventResponse(ChangeBlockStateEventPacket.EventTag, OnStateChange);
        }

        public void Initialize()
        {
        }

        private void OnStateChange(byte[] payload)
        {
            var data = MessagePackSerializer.Deserialize<ChangeBlockStateEventMessagePack>(payload);
            
            var pos = data.Position.Vector2Int;
            if (!_chunkBlockGameObjectDataStore.BlockGameObjectDictionary.TryGetValue(pos, out var _))
            {
                Debug.Log("ブロックがない : " + pos);
            }
            else
            {
                var blockObject = _chunkBlockGameObjectDataStore.BlockGameObjectDictionary[pos];
                blockObject.BlockStateChangeProcessor.OnChangeState(data.CurrentState, data.PreviousState, data.CurrentStateJsonData);

                var blockConfig = _blockConfig.GetBlockConfig(blockObject.BlockId);

            }
        }
    }
}