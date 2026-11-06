using System;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Stage
{
    /// <summary>
    /// 1 つの層の中に「実画面いっぱい」と「中央の 1280×720 の舞台」の 2 つの子を作る。寸法と配置は UiStageFrame.uss が持つ
    /// Builds the two children of one layer, the full-screen viewport and the centered 1280x720 stage; sizes and placement live in UiStageFrame.uss
    /// </summary>
    public sealed class UiStageFrame
    {
        private const string ViewportClassName = "ui-stage-frame__viewport";
        private const string StageClassName = "ui-stage-frame__stage";

        private readonly VisualElement _viewport;
        private readonly VisualElement _stage;

        private UiStageFrame(VisualElement viewport, VisualElement stage)
        {
            _viewport = viewport;
            _stage = stage;
        }

        public static UiStageFrame Build(VisualElement layerContainer)
        {
            // 実画面の子を先、舞台を後に足す（同じ層の中では舞台が前面）
            // Add the viewport child first and the stage after it (the stage is in front within a layer)
            var viewport = CreateChild("viewport", ViewportClassName);
            var stage = CreateChild("stage", StageClassName);
            layerContainer.Add(viewport);
            layerContainer.Add(stage);
            return new UiStageFrame(viewport, stage);

            #region Internal

            VisualElement CreateChild(string suffix, string className)
            {
                // 族の子もクリックを受けず、空の場所はワールドへ抜ける
                // Family children take no clicks either, so empty spots fall through to the world
                var child = new VisualElement { name = $"{layerContainer.name}-{suffix}", pickingMode = PickingMode.Ignore };
                child.AddToClassList(className);
                return child;
            }

            #endregion
        }

        public VisualElement Get(UiFamily family)
        {
            return family switch
            {
                UiFamily.Stage => _stage,
                UiFamily.Viewport => _viewport,
                _ => throw new ArgumentOutOfRangeException(nameof(family), family, null),
            };
        }
    }
}
