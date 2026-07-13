using UnityEngine;

namespace MapGenerator
{
    /// <summary>
    /// フィールドのInspector表示名を上書きする。日本語ラベル等に使う。
    /// </summary>
    public class LabelAttribute : PropertyAttribute
    {
        public string DisplayName { get; }
        public LabelAttribute(string displayName) => DisplayName = displayName;
    }
}
