using Client.Network.NewApi;
using Game.Block.Interface.BlockConfig;
using MainGame.Network.Event;
using MainGame.UnityView.Chunk;
using MessagePack;
using Server.Event.EventReceive;
using SinglePlay;
using UnityEngine;
using VContainer.Unity;

namespace MainGame.Presenter.Block
{
    public class BlockStateEventHandler : IInitializable
    {
        private readonly IBlockConfig _blockConfig;

        private readonly ChunkBlockGameObjectDataStore _chunkBlockGameObjectDataStore;


        public BlockStateEventHandler(ChunkBlockGameObjectDataStore chunkBlockGameObjectDataStore, SinglePlayInterface singlePlayInterface)
        {
            _blockConfig = singlePlayInterface.BlockConfig;
            _chunkBlockGameObjectDataStore = chunkBlockGameObjectDataStore;
            VanillaApi.RegisterEventResponse(ChangeBlockStateEventPacket.EventTag, OnStateChange);
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