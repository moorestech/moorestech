using Client.WebUiHost.Game.Actions;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.EditModeInPlayingTest.BlockInventory
{
    /// <summary>
    ///     Web UIと同じ登録済みハンドラをhubから引いて実行する
    ///     Runs the very handler the Web UI uses, resolved from the hub instead of reconstructed
    /// </summary>
    public static class WebUiActionInvoker
    {
        public static async UniTask<ActionResult> ExecuteAsync(string actionType, JObject payload)
        {
            // hub未起動ならWebUiGameBinderの配線を通っていないため、緑にせず落とす
            // A missing hub means WebUiGameBinder never wired anything; fail instead of passing vacuously
            var hub = Client.WebUiHost.Boot.WebUiHost.Hub;
            Assert.IsNotNull(hub, "WebUiHost hub is not running; no action is registered");

            var handler = hub.ResolveAction(actionType);
            Assert.IsNotNull(handler, $"action '{actionType}' is not registered by WebUiGameBinder");

            return await handler.ExecuteAsync(payload);
        }
    }
}
