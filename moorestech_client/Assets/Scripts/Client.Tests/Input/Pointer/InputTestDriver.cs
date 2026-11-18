using System.Reflection;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Client.Tests.Input.Pointer
{
    /// <summary>
    /// internal の InputSystem 操作を呼ぶ
    /// Calls internal InputSystem operations
    /// </summary>
    public static class InputTestDriver
    {
        public static void RunUpdate(InputUpdateType updateType)
        {
            typeof(InputSystem).GetMethod("Update", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(InputUpdateType) }, null).Invoke(null, new object[] { updateType });
        }

        public static void LoseGameFocus(InputTestFixture fixture)
        {
            // runtime も InputTestRuntime も internal なので、取り出しと呼び出しの両方を反射で行う
            // Both runtime and InputTestRuntime are internal, so fetch and call through reflection
            var runtime = typeof(InputTestFixture).GetProperty("runtime", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(fixture);

            // Fixture は焦点が外れると入力を捨てる Player の挙動なので、本物の Editor の条件へ戻す
            // The fixture uses player behaviour that flushes input on focus loss, so restore the real Editor conditions
            // 入力設定（StarterAssets.inputsettings.asset）ではポインタとキーボードだけが Game View の焦点に従う
            // In the input settings (StarterAssets.inputsettings.asset) only pointers and keyboards follow Game View focus
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.PointersAndKeyboardsRespectGameViewFocus;
            // Editor は Game View に焦点が無くても常にプレイヤーループを回す（InputManager.OnFocusChanged）
            // The Editor always runs the player loop without Game View focus (InputManager.OnFocusChanged)
            runtime.GetType().GetProperty("runInBackground", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(runtime, true);
            runtime.GetType().GetMethod("PlayerFocusLost", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(runtime, null);
        }
    }
}
