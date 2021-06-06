using System;
using industrialization.Core.Installation.BeltConveyor.util;

namespace industrialization.OverallManagement
{
    public class InstallBeltConveyor
    {
        
        public static void Create(int id,int x,int y, Guid from, Guid to)
        {
            //機械の生成
            var beltConveyor  = BeltConveyorFactory.Create(id, Guid.NewGuid(),
                WorldInstallationInventoryDatastore.GetInstallation(to));
            
            //機械のコネクターを変更する
            beltConveyor.ChangeConnector(WorldInstallationInventoryDatastore.GetInstallation(from));
            WorldInstallationInventoryDatastore.GetInstallation(to).ChangeConnector(beltConveyor);
            
            //ワールドデータに登録
            WorldInstallationInventoryDatastore.AddInstallation(beltConveyor,beltConveyor.Guid);
            WorldInstallationDatastore.AddInstallation(beltConveyor,x,y);
        }
    }
}