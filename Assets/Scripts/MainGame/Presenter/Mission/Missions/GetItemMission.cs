using Core.Item.Config;
using MainGame.Basic;
using MainGame.Presenter.Inventory.Receive;
using MainGame.UnityView.UI.Mission;
using UniRx;

namespace MainGame.Presenter.Mission.Missions
{
    public class GetItemMission : MissionBase 
    {
        private int _gotItemCount = 0;
        
        public GetItemMission(int priority, string itemName,int count,string missionNameKey,IItemConfig itemConfig,MainInventoryViewPresenter mainInventoryViewPresenter) : 
            base(priority, missionNameKey,itemName,count.ToString())
        {
            Initialize(itemName,count,itemConfig,mainInventoryViewPresenter);
        }
        
        
        public GetItemMission(int priority, string itemName,int count,IItemConfig itemConfig,MainInventoryViewPresenter mainInventoryViewPresenter) : 
            base(priority, $"GetItemMission",itemName,count.ToString())
        {
            Initialize(itemName,count,itemConfig,mainInventoryViewPresenter);
        }
        
        
        private void Initialize(string itemName,int count,IItemConfig itemConfig,MainInventoryViewPresenter mainInventoryViewPresenter)
        {
            var itemId = itemConfig.GetItemId(AlphaMod.ModId, itemName);
            mainInventoryViewPresenter.OnUpdateInventory.Subscribe(invItem =>
            {
                if (base.IsDone)
                {
                    return;
                }
                if (invItem.item.ID != itemId) return;
                
                _gotItemCount +=  invItem.item.Count;
                if (count <= _gotItemCount)
                {
                    base.Done();
                }
            });
        }

        protected override void IfNotDoneUpdate()
        {
        }
    }
}