using System.Linq;
using Client.Game.InGame.Control;
using Client.Input;
using Client.Playtest.Input;
using Client.Playtest.UiElements;
using Client.Playtest.UiElements.GameUi;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Root;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.EditModeInPlayingTest.Ui
{
    /// <summary>
    /// 実ゲームで DSL の UITK ロケータとポインタ判定が本番のパネル換算で一致するかを確かめる（R-3・R-11 の L3）
    /// Verifies in the real game that the DSL UITK locator and the pointer check agree through the production panel mapping (L3 of R-3, R-11)
    /// </summary>
    public static class GameUiPointerWiringChecks
    {
        private const string GameUiPanelSettingsName = "GameUiPanelSettings";

        // GameUi の PanelSettings を他の UIDocument が共有すると、probe がその文書の要素でも true を返す
        // If another UIDocument shared the GameUi PanelSettings, the probe would also answer true over that document's elements
        public static void AssertGameUiPanelSettingsIsNotShared()
        {
            var documents = Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var document in documents)
            {
                Debug.Log($"[GameUiInputWiring] UIDocument '{document.gameObject.name}' panelSettings={(document.panelSettings == null ? "null" : document.panelSettings.name)}");
            }
            var sharing = documents.Count(d => d.panelSettings != null && d.panelSettings.name == GameUiPanelSettingsName);
            Assert.AreEqual(1, sharing, $"{GameUiPanelSettingsName} を使う UIDocument が 1 枚でない");
        }

        public static async UniTask<Vector2> LocatorFindsFieldAndPointerIsOverIt(GameTextField field)
        {
            // 本番の GameUiRoot のパネルを、DSL と同じロケータで引く
            // Look up the production GameUiRoot panel with the same locator the DSL uses
            var locator = new GameUiElementLocator();
            var snapshot = await locator.Find(field.Field.name, 1f);
            Debug.Log($"[GameUiInputWiring] locator status={snapshot.Status} visible={snapshot.Visible} clickable={snapshot.Clickable} screenRect={snapshot.ScreenRect} worldBound={field.Field.worldBound} screen={Screen.width}x{Screen.height}");
            Assert.AreEqual(UiElementLookupStatus.Found, snapshot.Status, "ロケータが入力欄を見つけない");
            Assert.IsTrue(snapshot.Visible, "ロケータが入力欄を不可視と答えた");
            Assert.IsTrue(snapshot.Clickable, "ロケータが入力欄をクリック不可と答えた");

            // ロケータのスクリーン矩形の中心へマウスを動かすと、probe の逆換算で同じ要素に当たる（往復）
            // Moving the mouse to the locator's screen-rect center lands on the same element through the probe's inverse mapping (round trip)
            var center = snapshot.ScreenRect.center;
            await MoveMouseTo(center);
            var overGameUi = UiInputExclusivity.IsPointerOverGameUi(center);
            var overAnyUi = UiPointerHitTest.IsPointerOverAnyUi();
            Debug.Log($"[GameUiInputWiring] pointer over field mouse={HybridInput.GetMousePosition()} overGameUi={overGameUi} overAnyUi={overAnyUi} lock={UnityEngine.Cursor.lockState}");
            Assert.IsTrue(overGameUi, "ロケータの中心で probe が GameUi を拾わない（換算の往復がずれている）");
            Assert.IsTrue(overAnyUi, "入力欄の上で IsPointerOverAnyUi が false");

            // SetText で値が入る
            // SetText puts the value in
            var setText = await locator.SetText(field.Field.name, "wiring-check", 1f);
            Debug.Log($"[GameUiInputWiring] setText status={setText.Status} actual='{setText.ActualValue}' field='{field.Field.value}'");
            Assert.AreEqual(UiSetTextStatus.Applied, setText.Status, "SetText が Applied を返さない");
            Assert.AreEqual("wiring-check", field.Field.value, "SetText の値が入力欄に入っていない");
            return center;
        }

        public static async UniTask PointerIsNotOverAfterRemoval(VisualElement removed, Vector2 position)
        {
            // 要素を外すと同じ位置で false（空の層は拾わない）
            // After removing the element the same position is false (empty layers pick nothing)
            // ゲーム画面へ戻るとカーソルがロックされ判定が無条件に false になるので、解放してから測る
            // Returning to the game screen locks the cursor and forces false, so release it before measuring
            InputManager.MouseCursorVisible(true);
            removed.RemoveFromHierarchy();
            await MoveMouseTo(position);
            Assert.AreEqual(CursorLockMode.None, UnityEngine.Cursor.lockState, "カーソルがロックされていて判定が空振りになる");
            var overGameUi = UiInputExclusivity.IsPointerOverGameUi(position);
            var overAnyUi = UiPointerHitTest.IsPointerOverAnyUi();
            Debug.Log($"[GameUiInputWiring] pointer after removal overGameUi={overGameUi} overAnyUi={overAnyUi} webPointer={UiInputExclusivity.IsPointerOverWebUi}");
            Assert.IsFalse(overGameUi, "要素を外しても probe が GameUi を拾う");
            Assert.IsFalse(overAnyUi, "要素を外しても IsPointerOverAnyUi が true");
        }

        // EventSystem が新しい位置で当たり直すまで数フレーム注入を続ける
        // Keep injecting for a few frames so the EventSystem re-raycasts at the new position
        private static async UniTask MoveMouseTo(Vector2 screenPosition)
        {
            for (var i = 0; i < 5; i++)
            {
                SemanticInput.MouseMoveTo(screenPosition);
                await UniTask.Yield();
            }
            await UniTask.DelayFrame(2);
        }
    }
}
