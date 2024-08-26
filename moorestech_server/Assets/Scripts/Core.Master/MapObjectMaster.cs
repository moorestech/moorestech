using System;
using Mooresmaster.Model.MapObjectsModule;

namespace Core.Master
{
    public class MapObjectMaster
    {
        public static MapObjectElement GetMapObjectElement(Guid guid)
        {
            return Array.Find(MasterHolder.MapObjects.Data, x => x.MapObjectGuid == guid);
        }
    }
}