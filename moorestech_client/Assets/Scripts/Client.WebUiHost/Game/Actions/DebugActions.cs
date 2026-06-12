using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// 疎通確認用のダミー action
    /// Dummy action for connectivity verification
    /// </summary>
    public class EchoActionHandler : IActionHandler
    {
        public string ActionType => "debug.echo";

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            Debug.Log($"[WebUiHost] debug.echo: {payload?.ToString(Formatting.None)}");
            return UniTask.FromResult(ActionResult.Success());
        }
    }
}
