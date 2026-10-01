using System;
using UniRx;

namespace Client.Game.InGame.Player
{
    /// <summary>
    /// 文字入力中は自機の移動を止める
    /// Stops player movement while a Web UI text field owns focus
    /// </summary>
    public class TextInputMovementLockApplier
    {
        private readonly IPlayerObjectController _playerObjectController;

        public TextInputMovementLockApplier(IPlayerObjectController playerObjectController)
        {
            _playerObjectController = playerObjectController;
        }

        // 現在値と変化が1本の購読で届くので、現在値を別に読む窓（その間の変化を落とす窓）が無い
        // Current value and changes arrive through one subscription, so there is no separate read whose window could drop a change
        public void Initialize(IObservable<bool> textInputFocused)
        {
            textInputFocused.Subscribe(ApplyTextInputFocus);

            #region Internal

            void ApplyTextInputFocus(bool isTextInputFocused)
            {
                _playerObjectController.SetMovementLock(PlayerMovementLockReason.TextInput, isTextInputFocused);
            }

            #endregion
        }
    }
}
