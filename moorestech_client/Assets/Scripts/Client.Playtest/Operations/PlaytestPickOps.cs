using Client.Playtest.Input;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Playtest.Operations
{
    /// <summary>
    ///     スポイト操作の共有定義。シナリオごとの写経を防ぎ録画のアクションログにも残す
    ///     Shared eyedrop operations: keeps scenarios from copying it and records them in the action log
    /// </summary>
    public static class PlaytestPickOps
    {
        public static async UniTask MiddleClick(this PlaytestDriver p)
        {
            p.Note("ミドルクリック");
            SemanticInput.MouseButtonDown(2);
            await UniTask.DelayFrame(2);
            SemanticInput.MouseButtonUp(2);
            await UniTask.DelayFrame(2);
        }

        /// <summary>
        ///     通常モードのスポイトを実プレイヤー操作どおり左Altホールドで行う
        ///     Runs a normal-mode eyedrop with the left-Alt hold, as a real player would
        /// </summary>
        public static async UniTask PickWithAltHold(this PlaytestDriver p, Vector3 worldPosition)
        {
            p.Note($"Altスポイト: {worldPosition}");
            SemanticInput.KeyDown(Key.LeftAlt);

            // 押下がGetKeyDownとして拾われワープが済むまで待つ
            // Wait until the press is observed as GetKeyDown and the warp lands
            await UniTask.DelayFrame(3);
            await p.AimAt(worldPosition);
            await p.MiddleClick();
            SemanticInput.KeyUp(Key.LeftAlt);
            await UniTask.DelayFrame(3);
        }
    }
}
