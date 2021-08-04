using System;
using industrialization.Core.Installation.BeltConveyor.Generally;

namespace industrialization.Core.Installation.BeltConveyor.util
{
    public static class BeltConveyorFactory
    {
        public static GenericBeltConveyor Create(int installationId,int intId, IInstallationInventory connect)
        {
            return new GenericBeltConveyor(installationId,intId, new GenericBeltConveyor(0,new GenericBeltConveyorConnector(connect)));
        } 
    }
}