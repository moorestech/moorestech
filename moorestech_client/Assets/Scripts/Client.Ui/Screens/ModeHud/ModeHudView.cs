using Client.Ui.Screens.ModeHud.Parts;
using Client.Ui.State.ModeHud;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.ModeHud
{
    /// <summary>
    /// 操作モード HUD の画面ルート。渡された表示状態の派生の型で配置 HUD・削除の帯と高さを出し分ける。Model は購読しない（Presenter が Render を呼ぶ）
    /// Screen root of the operation-mode HUD; shows the placement HUD or the delete bands and height by the state's derived type; never subscribes (the presenter calls Render)
    /// </summary>
    public sealed class ModeHudView : VisualElement
    {
        private readonly PlacementModeHudPanel _placement = new();
        private readonly DeleteModeWarningBands _deleteBands = new();
        private readonly DeleteBoxHeightHud _deleteHeight = new();

        public ModeHudView()
        {
            AddToClassList("mode-hud");
            // 組は出すときだけ木に載せるので、拾わない指定は組ごとに組み終えた時点で掛ける
            // Groups are attached only while shown, so apply picking-ignore to each group once it is built
            ModeHudPickingIgnore.Apply(this);
            ModeHudPickingIgnore.Apply(_placement);
            ModeHudPickingIgnore.Apply(_deleteBands);
            ModeHudPickingIgnore.Apply(_deleteHeight);
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
                default:
                    MountOnly();
                    break;
            }

            // Web の帯は同じ viewportOverlay の中で後から描かれるので、出すときは層の中で前へ
            // Web draws the bands later in the same viewportOverlay, so bring the HUD to the front of its layer when shown
            if (viewModel is not ModeHudHidden) BringToFront();
        }

        private void MountOnly(params VisualElement[] shown)
        {
            // 組の並び（帯 → 配置 → 削除箱）は固定。載せる順もこの順
            // Group order (bands, placement, delete box) is fixed and groups are attached in this order
            foreach (var group in new VisualElement[] { _deleteBands, _placement, _deleteHeight })
            {
                var wanted = System.Array.IndexOf(shown, group) >= 0;
                if (wanted && group.parent != this) Add(group);
                if (!wanted && group.parent == this) Remove(group);
            }
        }
    }
}
