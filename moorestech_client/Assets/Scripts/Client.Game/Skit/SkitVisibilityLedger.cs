using System.Collections.Generic;
using CommandForgeGenerator.Command;

namespace Client.Game.Skit
{
    // スキットが表示を切り替えられる窓口
    // Visibility entry points a skit can toggle
    internal enum SkitVisibilityWindow
    {
        Background,
        Block,
        WorldObject,
        Entity,
    }

    /// <summary>
    ///     非表示にした窓口だけを記録し、スキット終了時に記録分を一括で戻す台帳
    ///     Ledger that records only the windows switched off, then restores exactly those when the skit ends
    /// </summary>
    internal class SkitVisibilityLedger : ISkitEnvironmentRoot, ISkitBlockObjectControl, ISkitWorldObjectControl, ISkitEntityObjectControl
    {
        private readonly ISkitEnvironmentRoot _environmentRoot;
        private readonly ISkitBlockObjectControl _blockObjectControl;
        private readonly ISkitWorldObjectControl _worldObjectControl;
        private readonly ISkitEntityObjectControl _entityObjectControl;

        private readonly List<SkitVisibilityWindow> _hiddenWindows = new();

        internal SkitVisibilityLedger(
            ISkitEnvironmentRoot environmentRoot,
            ISkitBlockObjectControl blockObjectControl,
            ISkitWorldObjectControl worldObjectControl,
            ISkitEntityObjectControl entityObjectControl)
        {
            _environmentRoot = environmentRoot;
            _blockObjectControl = blockObjectControl;
            _worldObjectControl = worldObjectControl;
            _entityObjectControl = entityObjectControl;
        }

        // コマンドからの4窓口は同一の台帳を通り、どれを消したかがここへ集まる
        // All four entry points reach the same ledger, so what a skit hid is recorded in one place
        void ISkitEnvironmentRoot.SetActive(bool enable)
        {
            SetWindowActive(SkitVisibilityWindow.Background, enable);
        }

        void ISkitBlockObjectControl.SetActive(bool enable)
        {
            SetWindowActive(SkitVisibilityWindow.Block, enable);
        }

        void ISkitWorldObjectControl.SetActive(bool enable)
        {
            SetWindowActive(SkitVisibilityWindow.WorldObject, enable);
        }

        void ISkitEntityObjectControl.SetActive(bool enable)
        {
            SetWindowActive(SkitVisibilityWindow.Entity, enable);
        }

        // 中断でも完走でも、非表示にした窓口だけを1ループで戻す
        // Whether the skit finished or aborted, one loop restores exactly the windows that were switched off
        internal void RestoreHiddenWindows()
        {
            foreach (var hiddenWindow in _hiddenWindows) ApplyToWindow(hiddenWindow, true);
            _hiddenWindows.Clear();
        }

        // 反映より先に台帳へ書くため、fan-out途中で失敗しても戻し漏れが出ない
        // Recording before applying keeps a mid fan-out failure from leaving a window unrestorable
        private void SetWindowActive(SkitVisibilityWindow window, bool enable)
        {
            if (enable) _hiddenWindows.Remove(window);
            else if (!_hiddenWindows.Contains(window)) _hiddenWindows.Add(window);

            ApplyToWindow(window, enable);
        }

        private void ApplyToWindow(SkitVisibilityWindow window, bool enable)
        {
            switch (window)
            {
                case SkitVisibilityWindow.Background:
                    _environmentRoot.SetActive(enable);
                    break;
                case SkitVisibilityWindow.Block:
                    _blockObjectControl.SetActive(enable);
                    break;
                case SkitVisibilityWindow.WorldObject:
                    _worldObjectControl.SetActive(enable);
                    break;
                case SkitVisibilityWindow.Entity:
                    _entityObjectControl.SetActive(enable);
                    break;
            }
        }
    }
}
