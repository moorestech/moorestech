using System.Reflection;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Client.UitkStage0Probe
{
    // PoCビルドの無人計測用: タイトルが落ち着いてから「ローカルで開始」ボタンを押す
    // For unattended PoC-build runs: press the start-local button once the title settles
    public static class UitkAutoStartLocal
    {
        public static void PressSoon()
        {
            PressAsync().Forget();

            #region Internal

            async UniTaskVoid PressAsync()
            {
                await UniTask.Delay(System.TimeSpan.FromSeconds(3), ignoreTimeScale: true);
                // StartLocal は asmdef を持たない Assembly-CSharp にあり参照できないので、型名で引く
                // StartLocal lives in Assembly-CSharp without an asmdef and cannot be referenced, so resolve it by name
                var startLocalType = System.Type.GetType("Client.MainMenu.StartLocal, Assembly-CSharp");
                var startLocal = Object.FindFirstObjectByType(startLocalType);
                var field = startLocalType.GetField("startLocalButton", BindingFlags.NonPublic | BindingFlags.Instance);
                var button = (Button)field.GetValue(startLocal);
                Debug.Log("[UitkProbe] auto start-local: invoking the title button");
                button.onClick.Invoke();
            }

            #endregion
        }
    }
}
