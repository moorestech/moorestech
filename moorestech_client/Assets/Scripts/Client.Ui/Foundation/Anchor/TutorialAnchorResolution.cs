using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Anchor
{
    // アンカー解決の結果。missing・unique・duplicateの判別union
    // Anchor resolution result: a discriminated union of missing, unique and duplicate
    public abstract class TutorialAnchorResolution
    {
        public sealed class Missing : TutorialAnchorResolution
        {
        }

        public sealed class Unique : TutorialAnchorResolution
        {
            public readonly VisualElement Element;

            public Unique(VisualElement element)
            {
                Element = element;
            }
        }

        public sealed class Duplicate : TutorialAnchorResolution
        {
            public readonly int Count;

            public Duplicate(int count)
            {
                Count = count;
            }
        }
    }
}
