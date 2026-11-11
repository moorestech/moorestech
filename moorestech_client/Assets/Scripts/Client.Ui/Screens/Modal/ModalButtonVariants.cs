using System;
using Client.Ui.Components.Controls;
using Client.Ui.State.Modal.Logic;

namespace Client.Ui.Screens.Modal
{
    /// <summary>
    /// 確定ボタンの色の向き → ボタンの見た目の唯一の写し（設計書 A-28。Web は blue・red）
    /// The single mapping from confirm-button tone to button look (design A-28; the Web uses blue and red)
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
