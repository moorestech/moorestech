using System.Collections.Generic;
using System.Threading;
using Core.Item;
using Cysharp.Threading.Tasks;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Network.NewApi
{
    public class VanillaApi
    {
        private readonly ServerConnector _serverConnector;
        private readonly ItemStackFactory _itemStackFactory;
        
        public VanillaApi(ServerConnector serverConnector, ItemStackFactory itemStackFactory)
        {
            _serverConnector = serverConnector;
            _itemStackFactory = itemStackFactory;
        }
        
        /// <summary>
        /// TODO この呼び出しタイミングを考える
        /// </summary>
        public async UniTask<List<MapObjectsInfoMessagePack>> GetMapObjectInfo(CancellationToken ct)
        {
            var request = new RequestMapObjectInfosMessagePack();
            var response = await _serverConnector.GetInformationData<ResponseMapObjectInfosMessagePack>(request ,ct);
            return response?.MapObjects;
        }
        
        
        public async UniTask<List<IItemStack>> GetBlockInventory(Vector2Int blockPos,CancellationToken ct)
        {
            var request = new RequestBlockInventoryRequestProtocolMessagePack(blockPos.x, blockPos.y);
            
            var response = await _serverConnector.GetInformationData<BlockInventoryResponseProtocolMessagePack>(request ,ct);
                        
            var items = new List<IItemStack>(response.ItemIds.Length);
            for (int i = 0; i < response.ItemIds.Length; i++)
            {
                var id = response.ItemIds[i];
                var count = response.ItemCounts[i];
                items.Add(_itemStackFactory.Create(id, count));
            }
            
            return items;
        }
    }
}