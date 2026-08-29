using Client.Playtest.Input;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Playtest.Operations
{
    /// <summary>
    ///     右短押し（解除）と右ドラッグ（TPS回転）の注入。両者の違いは押下中の移動の有無だけ
    ///     Injects a right short press (cancel) and a right drag (TPS look); the only difference is pointer movement while held
    /// </summary>
    public static class PlaytestCancelOps
    {
        public static async UniTask RightShortClick(this PlaytestDriver p)
        {
            p.Note("右短押し");
            SemanticInput.MouseButtonDown(1);
            await UniTask.DelayFrame(2);
            SemanticInput.MouseButtonUp(1);
            await UniTask.DelayFrame(2);
        }

        public static async UniTask RightDrag(this PlaytestDriver p, Vector2 deltaPixels)
        {
            p.Note("右ドラッグ");
            var start = SemanticInput.CurrentMousePosition();
            SemanticInput.MouseButtonDown(1);
            await UniTask.DelayFrame(2);
            SemanticInput.MouseMoveTo(start + deltaPixels);
            await UniTask.DelayFrame(2);
            SemanticInput.MouseButtonUp(1);
            await UniTask.DelayFrame(2);
        }
    }
}
