using System;
using UniRx;

namespace Client.Game.InGame.Player
{
    /// <summary>
    /// Web UIの文字入力欄にフォーカスがある間、自機の移動を止める
    /// 打鍵した文字（WASD・Space・Shift）がそのまま移動・ジャンプ・ダッシュとして漏れるのを防ぐ
    /// Stops player movement while a Web UI text field owns focus
    /// Keeps typed characters (WASD, Space, Shift) from leaking out as walk, jump and sprint
    /// </summary>
    public class TextInputMovementLockApplier
    {
        private readonly IPlayerObjectController _playerObjectController;

        public TextInputMovementLockApplier(IPlayerObjectController playerObjectController)
        {
            _playerObjectController = playerObjectController;
        }

        // 購読してから現在値を適用し、購読前に起きた変化の取りこぼしを防ぐ
        // Subscribe first, then apply the current value, so a change made before subscribing is never missed
        public void Initialize(IObservable<bool> textInputFocusedChanged, bool isTextInputFocused)
        {
            textInputFocusedChanged.Subscribe(ApplyTextInputFocus);
            ApplyTextInputFocus(isTextInputFocused);
        }

        private void ApplyTextInputFocus(bool isTextInputFocused)
        {
            _playerObjectController.SetMovementLock(PlayerMovementLockReason.TextInput, isTextInputFocused);
        }
    }
}
