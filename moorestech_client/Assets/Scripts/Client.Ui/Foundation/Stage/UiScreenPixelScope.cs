using System;
using Client.Ui.Foundation.Stage.ScaleReading;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Stage
{
    /// <summary>
    /// 倍率を打ち消し実画面 px で置く箱
    /// Box that cancels scale and places in real-screen px
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
            // 配置はパネルの倍率の物理画素へ丸まり、箱の中では 0.667 刻みになる（look-exception: LX-170）
            // Placement snaps to the panel scale's physical pixels, so inside the box it steps by 0.667 (look-exception: LX-170)
            style.scale = new Scale(new Vector3(1f / stageScale, 1f / stageScale, 1f));
        }
    }
}
