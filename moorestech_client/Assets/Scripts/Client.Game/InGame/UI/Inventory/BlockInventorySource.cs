using System;
using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Block;
using Core.Item.Interface;
using Cysharp.Threading.Tasks;
using Server.Util.MessagePack;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory
{
    /// <summary>
    /// ブロックインベントリのソース情報を提供
    /// Provides block inventory source information
    /// </summary>
    public class BlockInventorySource : IInventorySource
    {
        private readonly Vector3Int _blockPosition;
        private readonly BlockGameObject _blockGameObject;
        
        public BlockInventorySource(Vector3Int blockPosition, BlockGameObject blockGameObject)
        {
            _blockPosition = blockPosition;
            _blockGameObject = blockGameObject;
        }
        
        public InventoryType GetInventoryType()
        {
            return InventoryType.Block;
        }
        
        public InventoryIdentifierMessagePack GetIdentifier()
        {
            return InventoryIdentifierMessagePack.CreateBlockMessage(_blockPosition);
        }
        
        public Type GetViewType()
        {
            return typeof(IBlockInventoryView);
        }
        
        public string GetAddressablePath()
        {
            return _blockGameObject.BlockMasterElement.BlockUIAddressablesPath;
        }
        
        public async UniTask<List<IItemStack>> FetchInventoryData(CancellationToken ct)
        {
            return await ClientContext.VanillaApi.Response.GetBlockInventory(_blockPosition, ct);
        }
    }
}
