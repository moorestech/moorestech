using System;
using Client.Ui.Foundation.Stage.ScaleReading;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Stage
{
    /// <summary>
    /// 舞台の倍率を打ち消し、中身を実画面 px で置く箱。Web で --ui-scale を掛けない Portal（Mantine Modal・言語ゲート）の中身を入れる（設計書 A-41）
    /// A box cancelling the stage scale so its contents sit in real-screen px; holds the contents of Web portals without --ui-scale (Mantine Modal, the language gate) (design A-41)
    /// </summary>
    public sealed class UiScreenPixelScope : VisualElement
    {
        private readonly StageScaleSource _stageScale;
        private IDisposable _subscription;

        public UiScreenPixelScope(string testid, StageScaleSource stageScale)
        {
            name = testid;
            _stageScale = stageScale;
            pickingMode = PickingMode.Ignore;
            AddToClassList("ui-screen-pixel-scope");

            // 購読はパネルに載っている間だけ
            // Subscribe only while attached to a panel
            RegisterCallback<AttachToPanelEvent>(_ => _subscription = _stageScale.Scale.Subscribe(ApplyStageScale));
            RegisterCallback<DetachFromPanelEvent>(_ => _subscription.Dispose());
        }

        private void ApplyStageScale(float stageScale)
        {
            // 寸法を倍率倍に広げてから 1/倍率 に縮める。見かけは親いっぱいのまま、中身の 1px が実画面の 1px になる
            // Grow the size by the scale, then shrink by 1/scale; it still looks parent-sized while one content px is one real-screen px
            style.width = Length.Percent(100f * stageScale);
            style.height = Length.Percent(100f * stageScale);
            style.scale = new Scale(new Vector3(1f / stageScale, 1f / stageScale, 1f));
        }
    }
}
