using System;
using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.Entity.Object;
using Client.Game.InGame.UI.Inventory.Train;
using Core.Item.Interface;
using Cysharp.Threading.Tasks;
using Server.Util.MessagePack;

namespace Client.Game.InGame.UI.Inventory
{
    /// <summary>
    /// 列車インベントリのソース情報を提供
    /// Provides train inventory source information
    /// </summary>
    public class TrainInventorySource : IInventorySource
    {
        private const string AddressablePath = "InGame/UI/Inventory/TrainInventoryView";
        
        private readonly Guid _trainId;
        private readonly TrainEntityObject _trainEntity;
        
        public TrainInventorySource(Guid trainId, TrainEntityObject trainEntity)
        {
            _trainId = trainId;
            _trainEntity = trainEntity;
        }
        
        public InventoryType GetInventoryType()
        {
            return InventoryType.Train;
        }
        
        public InventoryIdentifierMessagePack GetIdentifier()
        {
            return new InventoryIdentifierMessagePack(_trainId);
        }
        
        public Type GetViewType()
        {
            return typeof(ITrainInventoryView);
        }
        
        public string GetAddressablePath()
        {
            return AddressablePath;
        }
        
        public async UniTask<List<IItemStack>> FetchInventoryData(CancellationToken ct)
        {
            return await ClientContext.VanillaApi.Response.GetTrainInventory(_trainId, ct);
        }
    }
}

