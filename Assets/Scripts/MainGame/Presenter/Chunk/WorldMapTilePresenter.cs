using MainGame.Basic;
using MainGame.Model.Network.Event;
using MainGame.UnityView.WorldMapTile;
using UnityEngine;
using VContainer.Unity;

namespace MainGame.Presenter.Chunk
{
    public class WorldMapTilePresenter : IInitializable
    {
        private readonly WorldMapTileGameObjectDataStore _worldMapTileGameObjectDataStore;

        public WorldMapTilePresenter(NetworkReceivedChunkDataEvent networkReceivedChunkDataEvent,WorldMapTileGameObjectDataStore worldMapTileGameObjectDataStore)
        {
            _worldMapTileGameObjectDataStore = worldMapTileGameObjectDataStore;
            networkReceivedChunkDataEvent.OnChunkUpdateEvent += OnChunkUpdate;
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
                    _worldMapTileGameObjectDataStore.GameObjectBlockPlace(pos,id);
                }
            }
        }

        public void Initialize() { }
    }
}