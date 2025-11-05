using System.Collections.Generic;
using Client.Game.InGame.Entity.Object;
using Core.Item.Interface;
using Mooresmaster.Model.TrainModule;

namespace Client.Game.InGame.UI.Inventory.Train
{
    /// <summary>
    /// 列車インベントリビューのインターフェース
    /// Train inventory view interface
    /// </summary>
    public interface ITrainInventoryView : ISubInventoryView
    {
        public void Initialize(TrainCarEntityObject trainCarEntity);
    }
}