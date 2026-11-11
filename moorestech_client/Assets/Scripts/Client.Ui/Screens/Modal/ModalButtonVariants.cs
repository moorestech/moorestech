using System;
using Client.Ui.Components.Controls;
using Client.Ui.State.Modal.Logic;

namespace Client.Ui.Screens.Modal
{
    /// <summary>
    /// 確定ボタンの色の向き→見た目の唯一の写し
    /// The single mapping from confirm tone to button look
    /// </summary>
    public static class ModalButtonVariants
    {
        public static FormButtonVariant From(ModalButtonTone tone)
        {
            return tone switch
            {
                ModalButtonTone.Blue => FormButtonVariant.Filled,
                ModalButtonTone.Red => FormButtonVariant.Danger,
                _ => throw new ArgumentOutOfRangeException(nameof(tone), tone, null),
            };
        }
    }
}
