// debug.echo は疎通確認用のため、本番ビルドへ混入しないようエディタ/開発ビルド限定にする
// debug.echo is connectivity-only, so gate it to editor/development builds to keep it out of production
#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
#endif
