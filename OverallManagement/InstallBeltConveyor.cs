using System;
using industrialization.Core;
using industrialization.Core.Block.BeltConveyor.util;
using industrialization.OverallManagement.DataStore;

namespace industrialization.OverallManagement
{
    public class InstallBeltConveyor
    {
        
        public static void Create(int id,int x,int y, int fromId, int toId)
        {
            //機械の生成
            var beltConveyor  = BeltConveyorFactory.Create(id, IntId.NewIntId(),
                WorldBlockInventoryDatastore.GetBlock(toId));
            
            //機械のコネクターを変更する
            beltConveyor.ChangeConnector(WorldBlockInventoryDatastore.GetBlock(fromId));
            WorldBlockInventoryDatastore.GetBlock(toId).ChangeConnector(beltConveyor);
            
            //ワールドデータに登録
            WorldBlockInventoryDatastore.AddBlock(beltConveyor,beltConveyor.GetIntId());
            WorldBlockDatastore.AddBlock(beltConveyor,x,y);
        }
    }
}