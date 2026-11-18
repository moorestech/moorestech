using System;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Stage
{
    /// <summary>
    /// 1層に実画面と舞台の2子を作る
    /// Builds viewport and stage children of one layer
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
            // 実画面を先、舞台を後に足す
            // Add viewport first, stage after
            var viewport = CreateChild("viewport", ViewportClassName);
            var stage = CreateChild("stage", StageClassName);
            layerContainer.Add(viewport);
            layerContainer.Add(stage);
            return new UiStageFrame(viewport, stage);

            #region Internal

            VisualElement CreateChild(string suffix, string className)
            {
                // 族の子もクリックを受けない
                // Family children take no clicks
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
