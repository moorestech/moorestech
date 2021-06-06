using System;
using industrialization.Core.Installation.BeltConveyor.Generally;

namespace industrialization.Core.Installation.BeltConveyor.util
{
    public class BeltConveyorFactory
    {
        public static GenericBeltConveyor Create(int installationId,Guid guid, IInstallationInventory connect)
        {
            return new GenericBeltConveyor(installationId,guid, new GenericBeltConveyorInventory(new GenericBeltConveyorConnector(connect)));
        } 
    }
}