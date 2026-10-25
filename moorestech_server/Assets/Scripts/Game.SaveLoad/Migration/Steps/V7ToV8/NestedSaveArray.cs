using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Migration.Steps.V7ToV8
{
    /// <summary>
    /// save[節][配列] を辿った結果。無い・JSON の null・配列ありを派生型で分け、配列は配列ありの型だけが持つ
    /// The result of walking save[section][array]; absent, JSON null and present are separate subtypes, and only the present one carries the array
    /// </summary>
    public abstract class NestedSaveArray
    {
    }

    // 節か配列のキーが無い
    // A section or array key is absent
    public sealed class MissingNestedSaveArray : NestedSaveArray
    {
    }

    // 節か配列のどちらかが JSON の null
    // Either the section or the array is JSON null
    public sealed class ExplicitNullNestedSaveArray : NestedSaveArray
    {
    }

    // 配列がある
    // The array exists
    public sealed class PresentNestedSaveArray : NestedSaveArray
    {
        public readonly JArray Array;

        public PresentNestedSaveArray(JArray array)
        {
            Array = array;
        }
    }
}
