using Mooresmaster.Loader.PlaceSystemModule;
using Mooresmaster.Model.PlaceSystemModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    public class PlaceSystemMaster
    {
        public readonly PlaceSystem PlaceSystem;
        
        public PlaceSystemMaster(JToken jToken)
        {
            PlaceSystem = PlaceSystemLoader.Load(jToken);
        }
    }
}