using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Boot
{
    /// <summary>
    /// Web クライアントから届く WS メッセージの共通 DTO
    /// Common DTO for WS messages arriving from the web client
    /// </summary>
    public class WsClientMessage
    {
        public string Op;
        public List<string> Topics;
        public string Topic;
        public string Type;
        public string RequestId;
        public JObject Payload;
    }
}
