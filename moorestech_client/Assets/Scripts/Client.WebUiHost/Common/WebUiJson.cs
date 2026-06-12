using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Client.WebUiHost.Common
{
    /// <summary>
    /// Web UI 向け JSON シリアライズ設定の一元管理
    /// Centralized JSON serialization settings for the Web UI
    /// </summary>
    public static class WebUiJson
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
        };

        public static string Serialize(object obj)
        {
            return JsonConvert.SerializeObject(obj, Settings);
        }
    }
}
