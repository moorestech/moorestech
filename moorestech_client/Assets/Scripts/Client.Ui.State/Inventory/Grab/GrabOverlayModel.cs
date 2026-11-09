using Client.Ui.State.Routing;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Inventory.Grab
{
    /// <summary>
    /// 掴みと画面のどちらが変わっても掴み絵の表示を決め直す（前例 ModeHudModel・CursorTooltipModel。D-23）
    /// Re-decides the grab picture whenever the grab or the screen changes (precedent ModeHudModel / CursorTooltipModel; D-23)
    /// </summary>
    public sealed class GrabOverlayModel : IInitializable
    {
        private readonly PlayerGrabModel _grab;
        private readonly UiRoutingState _routing;
        private readonly ReactiveProperty<GrabOverlayViewModel> _current = new(GrabOverlayViewModel.Hidden);

        public IReadOnlyReactiveProperty<GrabOverlayViewModel> Current => _current;

        public GrabOverlayModel(PlayerGrabModel grab, UiRoutingState routing)
        {
            _grab = grab;
            _routing = routing;
        }

        public void Initialize()
        {
            // 2 本を別々に購読して同じ判断をやり直す（CombineLatest は Func の合成を要るので使わない）
            // Subscribe to both separately and redo the same decision (CombineLatest needs a Func selector)
            _grab.Current.Subscribe(_ => Rebuild());
            _routing.Current.Subscribe(_ => Rebuild());
        }

        private void Rebuild()
        {
            _current.Value = GrabOverlayViewModelFactory.Create(_routing.Current.Value.Screen, _grab.Current.Value);
        }
    }
}
