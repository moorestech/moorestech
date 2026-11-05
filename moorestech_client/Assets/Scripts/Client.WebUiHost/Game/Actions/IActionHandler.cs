using Client.Ui.State.Commands;
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
        // ドット区切りの種別名
        // Dot-separated action type name
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

        // 型付きコマンドの結果を失敗契約へ写す正本
        // The single mapping from a typed command result to the failure contract
        public static ActionResult FromCommandResult(UiCommandResult result)
        {
            return result switch
            {
                UiCommandAccepted => Success(),
                UiCommandRejected rejected => Fail(rejected.ToWireCode()),
                _ => throw new System.ArgumentOutOfRangeException(nameof(result), result, null),
            };
        }

        public static ActionResult Fail(string error)
        {
            return new ActionResult(false, error ?? "unknown_error");
        }
    }
}
