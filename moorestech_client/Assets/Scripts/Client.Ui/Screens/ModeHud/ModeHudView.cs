using Client.Ui.Foundation.Input;
using Client.Ui.Screens.ModeHud.Parts;
using Client.Ui.State.ModeHud;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.ModeHud
{
    /// <summary>
    /// 操作モード HUD の画面ルート。型で出し分ける
    /// Screen root of the operation-mode HUD; branches by type
    /// </summary>
    public sealed class ModeHudView : VisualElement
    {
        private readonly PlacementModeHudPanel _placement = new();
        private readonly DeleteModeWarningBands _deleteBands = new();
        private readonly DeleteBoxHeightHud _deleteHeight = new();

        public ModeHudView()
        {
            AddToClassList("mode-hud");
            // 拾わない指定は組み終えてから掛ける
            // Apply picking-ignore once the group is built
            UiPickingIgnore.ApplyToSubtree(this);
            UiPickingIgnore.ApplyToSubtree(_placement);
            UiPickingIgnore.ApplyToSubtree(_deleteBands);
            UiPickingIgnore.ApplyToSubtree(_deleteHeight);
        }

        public void Render(ModeHudViewModel viewModel)
        {
            // 文を渡してから載せる。出さない組は display ではなく木から外す（文の無い LocalizedLabel が載ると 1 フレーム後に LocalizedTextBinding が LogError。前例 GamePanel.SetTitle）
            // Give text before attaching; unmount hidden groups instead of hiding them (an attached textless LocalizedLabel logs an error a frame later; precedent GamePanel.SetTitle)
            switch (viewModel)
            {
                case ModeHudPlacement placement:
                    _placement.Show(placement);
                    MountOnly(_placement);
                    break;
                case ModeHudDelete delete:
                    _deleteHeight.SetHeight(delete.Height);
                    MountOnly(_deleteBands, _deleteHeight);
                    break;
                case ModeHudHidden:
                    MountOnly();
                    break;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(viewModel), viewModel, "unknown ModeHud view model");
            }

            // 帯は層の中で前へ
            // Bring the bands to the front of the layer
            if (viewModel is not ModeHudHidden) BringToFront();

            #region Internal

            void MountOnly(params VisualElement[] shown)
            {
                // 並びは帯→配置→削除箱で固定
                // Order is fixed: bands, placement, delete box
                foreach (var group in new VisualElement[] { _deleteBands, _placement, _deleteHeight })
                {
                    var wanted = 0 <= System.Array.IndexOf(shown, group);
                    if (wanted && group.parent != this) Add(group);
                    if (!wanted && group.parent == this) Remove(group);
                }
            }

            #endregion
        }
    }
}
