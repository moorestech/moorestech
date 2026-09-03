using Client.Playtest.Input;
using Cysharp.Threading.Tasks;
using UnityEngine.InputSystem;

namespace Client.Playtest.Operations
{
    /// <summary>
    ///     インタラクト（F/E）の共有操作。録画のアクションログに残す
    ///     Shared interact operations (F/E) recorded in the action log
    /// </summary>
    public static class PlaytestInteractOps
    {
        public static async UniTask PressInteract(this PlaytestDriver p)
        {
            p.Note("インタラクト(F)");
            await SemanticInput.TapKey(Key.F);
        }

        public static async UniTask PressRide(this PlaytestDriver p)
        {
            p.Note("乗車(E)");
            await SemanticInput.TapKey(Key.E);
        }

        // 採掘はF長押しで進捗が溜まるので、指定秒だけ押し続ける
        // Mining accumulates while F is held, so keep it down for the given seconds
        public static async UniTask HoldInteract(this PlaytestDriver p, float seconds)
        {
            p.Note($"インタラクト長押し {seconds}s");
            SemanticInput.KeyDown(Key.F);
            await UniTask.Delay(System.TimeSpan.FromSeconds(seconds));
            SemanticInput.KeyUp(Key.F);
            await UniTask.DelayFrame(2);
        }
    }
}
