using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Playtest.Input
{
    internal sealed class PlaytestInputScope : IDisposable
    {
        private readonly InputDevice[] physical;
        private readonly bool[] enabled;
        private readonly Keyboard keyboard;
        private readonly Mouse mouse;
        private readonly InputSettings original;
        private readonly InputSettings temporary;
        internal PlaytestInputScope()
        {
            // Editor非フォーカスでも注入イベントを通常のゲームUpdateへ届ける。
            // Route injected events through normal game updates even without editor focus.
            physical = InputSystem.devices.Where(d => d is Keyboard || d is Mouse).ToArray();
            enabled = physical.Select(d => d.enabled).ToArray();
            original = InputSystem.settings;
            temporary = UnityEngine.Object.Instantiate(original);
            temporary.hideFlags = HideFlags.HideAndDontSave;
            temporary.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            temporary.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = temporary;
            // OS機器のノイズを一時隔離し、注入専用機器を通常InputActionへ接続する。
            // Isolate physical noise temporarily and feed dedicated devices through normal InputActions.
            foreach (var device in physical) InputSystem.DisableDevice(device);
            keyboard = InputSystem.AddDevice<Keyboard>("PlaytestKeyboard");
            mouse = InputSystem.AddDevice<Mouse>("PlaytestMouse");
        }
        public void Dispose()
        {
            // 共有assetは編集せず、成功・失敗の両方で元設定と解放入力へ戻す。
            // Leave the shared asset intact and restore settings and released inputs on either outcome.
            SemanticInput.ReleaseAllKeys();
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(mouse);
            InputSystem.settings = original;
            for (int i = 0; i < physical.Length; i++)
            {
                if (enabled[i]) InputSystem.EnableDevice(physical[i]);
                else InputSystem.DisableDevice(physical[i]);
            }
            UnityEngine.Object.DestroyImmediate(temporary);
        }
    }
}
