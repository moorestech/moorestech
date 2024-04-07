using Client.Game.Context;
using Game.Context;
using MainGame.UnityView.Chunk;
using MessagePack;
using Server.Event.EventReceive;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.BlockSystem
{
    public class BlockStateEventHandler : IInitializable
    {
        private readonly ChunkBlockGameObjectDataStore _chunkBlockGameObjectDataStore;

        public BlockStateEventHandler(ChunkBlockGameObjectDataStore chunkBlockGameObjectDataStore)
        {
            _chunkBlockGameObjectDataStore = chunkBlockGameObjectDataStore;
            MoorestechContext.VanillaApi.Event.RegisterEventResponse(ChangeBlockStateEventPacket.EventTag, OnStateChange);
        }

        public void Initialize()
        {
        }

        private void OnStateChange(byte[] payload)
        {
            var data = MessagePackSerializer.Deserialize<ChangeBlockStateEventMessagePack>(payload);

            var pos = data.Position;
            if (!_chunkBlockGameObjectDataStore.BlockGameObjectDictionary.TryGetValue(pos, out var _))
            {
                Debug.Log("ブロックがない : " + pos);
            }
            else
            {
                var blockObject = _chunkBlockGameObjectDataStore.BlockGameObjectDictionary[pos];
                blockObject.BlockStateChangeProcessor.OnChangeState(data.CurrentState, data.PreviousState, data.CurrentStateJsonData);

                var blockConfig = ServerContext.BlockConfig.GetBlockConfig(blockObject.BlockId);
            }
        }
    }
}