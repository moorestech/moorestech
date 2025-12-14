using Core.Master.Validator;
using Mooresmaster.Loader.PlaceSystemModule;
using Mooresmaster.Model.PlaceSystemModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    public class PlaceSystemMaster : IMasterValidator
    {
        public readonly PlaceSystem PlaceSystem;

        public PlaceSystemMaster(JToken jToken)
        {
            PlaceSystem = PlaceSystemLoader.Load(jToken);
        }

        public bool Validate(out string errorLogs)
        {
            return PlaceSystemMasterUtil.Validate(PlaceSystem, out errorLogs);
        }

        public void Initialize()
        {
            PlaceSystemMasterUtil.Initialize(PlaceSystem);
        }
    }
}