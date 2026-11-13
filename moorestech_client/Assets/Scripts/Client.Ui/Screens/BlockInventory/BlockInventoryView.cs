using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Ui.Components.Controls;
using Client.Ui.Components.Panels;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Stage.Columns;
using Client.Ui.Foundation.Text;
using Client.Ui.State.BlockInventory.Logic.Panel;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Text;
using Client.Ui.State.Routing;
using Client.Ui.State.Toast;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory
{
    /// <summary>
    /// ブロックインベントリの外殻（GamePanel・題・閉じる・エラー行・本文の器）。購読せず Render で値を受ける（Web BlockInventoryPanel）
    /// Block-inventory shell (GamePanel, title, close, error row, body holder); it never subscribes and takes values through Render (Web BlockInventoryPanel)
    /// </summary>
    public sealed class BlockInventoryView : VisualElement
    {
        internal const string LargeClassName = "block-inventory--large";
        private const string NaturalClassName = "block-inventory--natural";
        private const string BlockErrorName = "block-inventory-error";
        private const string TrainErrorName = "train-inventory-error";

        private readonly GamePanel _panel;
        private readonly VisualElement _titleActionContainer;
        private readonly VisualElement _body;
        private readonly IconButton _close;
        private readonly BlockInventoryPanelBand _band;
        private readonly BlockInventoryNaturalScroll _naturalScroll;
        private LocalizedLabel _error;

        public BlockInventoryView(StageColumnsLayout stage, IBlockInventoryPanelCommands commands, UiCommandResultToaster toaster)
        {
            // 外殻: block-inventory（Viewer 列の子）> GamePanel(block-inventory-panel, Default) > [エラー行, 本文の器]、閉じるは外殻に絶対配置
            // Shell: block-inventory (Viewer column child) > GamePanel(block-inventory-panel, Default) > [error row, body holder]; close is absolutely placed on the shell
            name = "block-inventory";
            AddToClassList("block-inventory");
            AddToClassList(NaturalClassName);
            _panel = new GamePanel("block-inventory-panel", GamePanelFrame.Default);
            _panel.AddToClassList("block-inventory__panel");
            Add(_panel);
            _titleActionContainer = new VisualElement { name = "block-inventory-title-action" };

            // エラー行 _error は文を持つまで作らない（文無しの LocalizedLabel を載せると LogError。前例 GamePanel の題の遅延生成）
            // The error row _error is created only once it has text (a text-less LocalizedLabel logs an error; precedent: GamePanel's lazy title)
            _body = new VisualElement { name = "block-inventory-body" };
            _body.AddToClassList("block-inventory__body");
            _panel.Add(_body);

            // 閉じるは Web と同じく外殻の右上に重ねる。自要素のクリックの結線で、拒否はビルドメニューと同じく Report する（前例 BuildMenuView）
            // Close overlays the shell's top-right as on the Web; wiring its own click, with refusals reported as the build menu does (precedent BuildMenuView)
            _close = new IconButton("block-inventory-close", new IconButtonCloseGlyph());
            _close.AddToClassList("block-inventory__close");
            TutorialAnchor.Mark(_close, AnchorIds.InventoryCloseButton);
            _close.OnClicked.Subscribe(_ => toaster.Report(UiStateTransitionRules.RequestCommandName, commands.Close()));
            Add(_close);

            _band = new BlockInventoryPanelBand(stage, this);
            _naturalScroll = new BlockInventoryNaturalScroll();

            // 生成直後は隠し、Presenter の SetShown が出す（前例 PauseMenuView）
            // Start hidden; the presenter's SetShown reveals it (precedent PauseMenuView)
            style.display = DisplayStyle.None;
        }

        public void SetShown(bool shown)
        {
            style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void Render(BlockInventoryPanelState state, BlockInventoryPanelLayout layout)
        {
            // 閉は前の描画のまま隠れるだけ（Web の return null）
            // Closed only hides the previous render (Web return null)
            if (state is ClosedBlockInventoryPanel) return;
            RenderTitle();
            RenderError();
            EnableInClassList(LargeClassName, layout.IsLarge);
            EnableInClassList(NaturalClassName, !layout.IsLarge);
            _band.SetLarge(layout.IsLarge);

            #region Internal

            void RenderTitle()
            {
                // 題はブロック名、列車は列車インベントリ
                // Title is the block name, or train inventory for trains
                var block = state as OpenBlockInventoryPanel;
                var title = block != null
                    ? UiText.FromContent(ContentLocalizationKeys.BlockName(block.BlockGuid))
                    : new UiText(LocalizationKeys.Ui.BlockInventory.TrainInventory);
                _panel.SetTitle(title);

                // 題の操作の器は題の後に、操作を持つ blockType のときだけ載せる（Web はエラー中も TitleAction があれば題の右を空ける）。中身は BodyHost が付け替える
                // Mount the action holder after the title only for block types with an action (the Web keeps the title reserve even while erroring); BodyHost swaps its contents
                if (block != null && block.Components.TitleAction != BlockInventoryTitleActionKind.None)
                {
                    _panel.SetTitleAction(_titleActionContainer);
                    _panel.SetTitleActionReserve(true);
                }
                else _panel.ClearTitleAction();
            }

            void RenderError()
            {
                // エラー行の出し入れは TryGetLocalizationKey の 1 か所で決める。一度作った行は外さず、文を持ったまま隠す
                // The error row is decided only by TryGetLocalizationKey; once built it is never removed, only hidden with its text kept
                var error = ((OpenInventoryPanel)state).Error;
                if (!BlockInventoryPanelErrorText.TryGetLocalizationKey(error, out var key))
                {
                    if (_error != null) _error.style.display = DisplayStyle.None;
                    return;
                }
                if (_error == null)
                {
                    // Web は素の div なので本文の既定（Mantine md 16px・行 1.55・余白 0）を当てる
                    // The Web uses a bare div, so apply the body default (Mantine md 16px, line 1.55, no margin)
                    _error = new LocalizedLabel();
                    _error.AddToClassList("block-inventory__error");
                    _error.AddToClassList(UiTypographyClass.TextMd);
                }
                _error.name = state is OpenTrainInventoryPanel ? TrainErrorName : BlockErrorName;
                _error.SetKey(key);
                _error.style.display = DisplayStyle.Flex;

                // 文を入れてから初めて載せる
                // Mount only after the text is set
                if (_error.parent == null) _panel.Insert(0, _error);
            }

            #endregion
        }

        public void PlaceBody(VisualElement body, bool natural)
        {
            // 小型は自然高のスクロール器で包み、大型は本文が自分で器を持つ（Web の isLargePanel の分岐）
            // Natural panels wrap the body in the natural-height scroller; large bodies own their scroller (Web isLargePanel branch)
            if (natural)
            {
                _naturalScroll.Wrap(body);
                _body.Add(_naturalScroll.Root);
                return;
            }
            _body.Add(body);
        }

        public void RestartBodyScroll()
        {
            _naturalScroll.Root.SetScrollOffset(0f);
        }

        public void ClearBody()
        {
            _naturalScroll.Unwrap();
            _body.Clear();
        }

        public void PlaceTitleAction(VisualElement action)
        {
            _titleActionContainer.Clear();
            _titleActionContainer.Add(action);
        }

        public void ClearTitleAction()
        {
            _titleActionContainer.Clear();
        }
    }
}
