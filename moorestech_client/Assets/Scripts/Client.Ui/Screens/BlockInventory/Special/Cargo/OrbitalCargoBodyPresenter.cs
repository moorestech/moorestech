using System.Collections.Generic;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.Special.Registration;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.Special;
using Client.Ui.State.BlockInventory.Special.Cargo;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.BlockInventory.Special.Selectors;
using Client.Ui.State.Items;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.Cargo
{
    /// <summary>
    /// 発射台・拠点の本文の Presenter。一覧の局所状態を持ち、本文の行・見出しの押下で進め、一覧を出すかを決めて本文に描かせる。選ぶコマンドは View が送る（前例 MachineSectionPresenter・BeltThroughputSectionPresenter）
    /// Presenter of the launch pad and elevator base bodies; owns the cargo list's local state, advances it on the body's row and headline presses and has the body draw with the decided mode; the view sends the select command (precedents MachineSectionPresenter, BeltThroughputSectionPresenter)
    /// </summary>
    public sealed class OrbitalCargoBodyPresenter : BlockInventorySectionBase
    {
        private readonly IOrbitalCargoBodyView _view;
        private readonly ISpecialDetailSelector<OrbitalCargoDetail> _cargoSelector;
        private readonly ISpecialBlockDetailModel _model;
        private readonly IBlockInventoryPanelSource _panels;
        private OrbitalCargoSelectionModeState _mode = new();
        private IReadOnlyList<SlotItem> _slots;
        private SpecialBlockDetailViewModel _detail;

        public override VisualElement Root { get; }

        public OrbitalCargoBodyPresenter(VisualElement root, IOrbitalCargoBodyView view, ISpecialDetailSelector<OrbitalCargoDetail> cargoSelector, ISpecialBlockDetailModel model, IBlockInventoryPanelSource panels)
        {
            Root = root;
            _view = view;
            _cargoSelector = cargoSelector;
            _model = model;
            _panels = panels;
        }

        public override void Attach(OpenBlockInventoryPanel panel)
        {
            // 同じパネルの枠と詳細が届くたびに、最後の値を覚えて描く
            // Remember the latest slots and detail of the same panel and draw on every update
            AddSubscription(SpecialPanelDetailStream.Of(_panels, _model, panel).Subscribe(pair =>
            {
                _slots = pair.slots;
                _detail = pair.detail;
                Apply();
            }));

            // 見出しの押下は一覧を開き、行の押下は要求を覚えて描き直す（送信は View。前例 MachineSectionPresenter・D-24・D-33）
            // A headline press opens the list; a row press remembers the request and redraws (the view sends; precedent MachineSectionPresenter, D-24, D-33)
            AddSubscription(_view.OnChangeCargoRequested.Subscribe(_ =>
            {
                _mode.OpenSelection();
                Apply();
            }));
            AddSubscription(_view.OnCargoRowPressed.Subscribe(payloadItemId =>
            {
                _mode.RememberRequest(payloadItemId);
                Apply();
            }));
        }

        protected override void ResetSectionStateOnDetach()
        {
            // 外れたら一覧の開閉と最後の値を捨てる（前のパネルの状態を次のパネルへ持ち越さない。前例 BeltThroughputSectionPresenter）
            // On detach drop the list state and the latest values (never carry the previous panel over; precedent BeltThroughputSectionPresenter)
            _mode = new OrbitalCargoSelectionModeState();
            _slots = null;
            _detail = null;
        }

        private void Apply()
        {
            // 一覧を出すかは積荷の詳細が届いたときだけ局所状態で決める。未着は本文が取得待ちを語るので、本文はいつも出す（前例 WholeSpecialDetailSelector）
            // Decide the list mode from local state only once the cargo detail arrives; while pending the body shows loading, so the body always shows (precedent WholeSpecialDetailSelector)
            var showsSelection = _cargoSelector.TryGet(_detail, out var cargo) && _mode.ResolveShowsSelection(cargo);
            _view.Render(_slots, _detail, showsSelection);
            SetApplicable(true);
        }
    }
}
