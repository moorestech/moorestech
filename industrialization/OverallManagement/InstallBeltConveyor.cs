using System;
using industrialization.Core;
using industrialization.Core.Installation.BeltConveyor.util;
using industrialization.OverallManagement.DataStore;

namespace industrialization.OverallManagement
{
    public class InstallBeltConveyor
    {
        
        public static void Create(int id,int x,int y, int fromId, int toId)
        {
            //機械の生成
            var beltConveyor  = BeltConveyorFactory.Create(id, IntId.NewIntId(),
                WorldInstallationInventoryDatastore.GetInstallation(toId));
            
            //機械のコネクターを変更する
            beltConveyor.ChangeConnector(WorldInstallationInventoryDatastore.GetInstallation(fromId));
            WorldInstallationInventoryDatastore.GetInstallation(toId).ChangeConnector(beltConveyor);
            
            //ワールドデータに登録
            WorldInstallationInventoryDatastore.AddInstallation(beltConveyor,beltConveyor.IntId);
            WorldInstallationDatastore.AddInstallation(beltConveyor,x,y);
        }
    }
}