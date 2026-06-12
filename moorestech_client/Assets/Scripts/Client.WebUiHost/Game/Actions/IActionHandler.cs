using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// Web からの action を処理するハンドラ
    /// Handler for actions sent from the web UI
    /// </summary>
    public interface IActionHandler
    {
        // ドット区切りの action 種別名（例: inventory.move_item）
        // Dot-separated action type name (e.g. inventory.move_item)
        string ActionType { get; }

        // メインスレッドで呼ばれる。payload は null の可能性あり
        // Invoked on the main thread; payload may be null
        UniTask<ActionResult> ExecuteAsync(JObject payload);
    }

    /// <summary>
    /// action の実行結果
    /// Result of an action execution
    /// </summary>
    public readonly struct ActionResult
    {
        public readonly bool Ok;
        public readonly string Error;

        private ActionResult(bool ok, string error)
        {
            Ok = ok;
            Error = error;
        }

        public static ActionResult Success()
        {
            return new ActionResult(true, null);
        }

        public static ActionResult Fail(string error)
        {
            return new ActionResult(false, error);
        }
    }
}
