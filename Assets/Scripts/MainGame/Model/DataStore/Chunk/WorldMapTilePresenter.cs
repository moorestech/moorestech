using MainGame.Basic;
using MainGame.Network.Event;
using UnityEngine;
using VContainer.Unity;

namespace MainGame.Model.DataStore.Chunk
{
    public class WorldMapTilePresenter : IInitializable
    {
        public WorldMapTilePresenter(INetworkReceivedChunkDataEvent networkReceivedChunkDataEvent)
        {
            networkReceivedChunkDataEvent.Subscribe(OnChunkUpdate,p => {});
        }


        private void OnChunkUpdate(OnChunkUpdateEventProperties properties)
        {
            var chunkPos = properties.ChunkPos;
            
            for (int i = 0; i < ChunkConstant.ChunkSize; i++)
            {
                for (int j = 0; j < ChunkConstant.ChunkSize; j++)
                {
                    var pos = chunkPos + new Vector2Int(i, j);
                    var id = properties.MapTileIds[i, j];
                    //todo イベントにする_worldMapTileGameObjectDataStore.GameObjectBlockPlace(pos,id);
                }
            }
        }

        public void Initialize() { }
    }
}