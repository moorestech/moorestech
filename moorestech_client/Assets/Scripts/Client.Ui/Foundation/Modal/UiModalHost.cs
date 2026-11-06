using System;
using Client.Ui.Foundation.Stage;
using Client.Ui.Foundation.Stage.ScaleReading;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Modal
{
    /// <summary>
    /// モーダルの宿主（Web features/modal/ModalHost の Mantine Modal の Portal）。幕と中央の中身を実画面 px で置く。同時 1 件
    /// Modal host (the Mantine Modal portal of Web features/modal/ModalHost); places the backdrop and the centred contents in real-screen px, one at a time
    /// </summary>
    /// <remarks>
    /// フォーカスは閉じ込めない（Web trapFocus={false}）。ESC と幕の押下で閉じるかは画面の状態が決める
    /// Focus is not trapped (Web trapFocus={false}); whether ESC or a backdrop press closes is decided by the screen state
    /// </remarks>
    public sealed class UiModalHost
    {
        private const string ShownClass = "ui-modal-host--shown";

        private readonly UiScreenPixelScope _scope;
        private readonly VisualElement _center;
        private readonly Subject<Unit> _onBackdropClicked = new();
        private VisualElement _content;

        public IObservable<Unit> OnBackdropClicked => _onBackdropClicked;

        public UiModalHost(StageScaleSource stageScale)
        {
            _scope = new UiScreenPixelScope("modal-scope", stageScale);
            _scope.AddToClassList("ui-modal-host");

            // 幕は押下を受け、中央の箱は素通しにして中身だけが受ける
            // The backdrop takes presses; the centre box passes through so only the contents take them
            var backdrop = new VisualElement { name = "modal-backdrop" };
            backdrop.AddToClassList("ui-modal-host__backdrop");
            backdrop.RegisterCallback<ClickEvent>(_ => _onBackdropClicked.OnNext(Unit.Default));
            _center = new VisualElement { name = "modal-center", pickingMode = PickingMode.Ignore };
            _center.AddToClassList("ui-modal-host__center");
            _scope.Add(backdrop);
            _scope.Add(_center);
        }

        public void Mount(VisualElement layer)
        {
            layer.Add(_scope);
        }

        // 新しい表示は前の中身を差し替える（Web ModalHost は store の 1 件だけを描く）
        // A new show replaces the previous contents (Web ModalHost renders the single store entry)
        public void Show(VisualElement content)
        {
            _content?.RemoveFromHierarchy();
            _content = content;
            _center.Add(content);
            ApplyShown();
        }

        public void Hide()
        {
            _content?.RemoveFromHierarchy();
            _content = null;
            ApplyShown();
        }

        // 表示の有無は中身の有無 1 つで決まり、状態クラスはその写しだけ（二重に持たない）
        // Shown-ness is decided by having contents alone; the state class only mirrors it (no second flag)
        private void ApplyShown()
        {
            _scope.EnableInClassList(ShownClass, _content != null);
        }
    }
}
