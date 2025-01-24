using System;
using Mooresmaster.Loader.FluidsModule;
using Mooresmaster.Model.FluidsModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    public class FluidMaster
    {
        public readonly Fluids Fluids;
        
        public FluidMaster(JToken jToken)
        {
            Fluids = FluidsLoader.Load(jToken);
        }
        
        public FluidMasterElement GetFluidElement(Guid guid)
        {
            return Array.Find(Fluids.Data, x => x.FluidGuid == guid);
        }
    }
}