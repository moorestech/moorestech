using System.Collections.Generic;
using Client.Game.InGame.Tutorial;
using UnityEngine;

namespace Client.Ui.State.Tutorial.Overlay
{
    /// <summary>
    /// 演出データから枠と DragGuide だけを取り出す。keyControl は KeyControlHint の持ち物
    /// Extracts only outlines and drag guides from presentation data; keyControl belongs to KeyControlHint
    /// </summary>
    public sealed class TutorialOverlayProjection
    {
        private readonly TutorialTextFactory _textFactory = new();

        public TutorialOverlayViewModel Project(TutorialPresentationData data)
        {
            var outlines = new List<TutorialOutlineCue>();
            var dragGuides = new List<TutorialDragGuideCue>();
            foreach (var session in data.Sessions)
            {
                foreach (var element in session.Elements)
                {
                    switch (element)
                    {
                        case TutorialOutlineElementData outline:
                            outlines.Add(CreateOutline(session.TutorialSessionId, outline));
                            break;
                        case TutorialDragGuideElementData dragGuide:
                            dragGuides.Add(new TutorialDragGuideCue(TutorialElementKey.Of(session.TutorialSessionId, dragGuide.ElementId), dragGuide.FromAnchorId, dragGuide.ToAnchorId));
                            break;
                        // keyControl は KeyControlHintProjection の持ち物なのでここでは読まない
                        // keyControl belongs to KeyControlHintProjection, so it is not read here
                        case TutorialKeyControlElementData _:
                            break;
                        default:
                            Debug.LogError($"[TutorialOverlayProjection] unsupported tutorial element: {element.GetType().Name}");
                            break;
                    }
                }
            }
            return new TutorialOverlayViewModel(outlines, dragGuides);

            #region Internal

            TutorialOutlineCue CreateOutline(string sessionId, TutorialOutlineElementData outline)
            {
                // ラベルの GUID が無いか読めなければ枠だけ（読めない時は TutorialTextFactory が LogError）
                // Without a label GUID, or with an unreadable one, show the outline alone (TutorialTextFactory logs the latter)
                if (string.IsNullOrEmpty(outline.LabelTutorialGuid)) return new TutorialOutlineCue.Plain(sessionId, outline.ElementId, outline.AnchorId, outline.PaddingPx);
                var context = $"labelTutorialGuid {TutorialElementKey.Of(sessionId, outline.ElementId)}";
                if (!_textFactory.TryCreate(outline.LabelTutorialGuid, context, out var label)) return new TutorialOutlineCue.Plain(sessionId, outline.ElementId, outline.AnchorId, outline.PaddingPx);
                return new TutorialOutlineCue.Labeled(sessionId, outline.ElementId, outline.AnchorId, outline.PaddingPx, label);
            }

            #endregion
        }
    }
}
