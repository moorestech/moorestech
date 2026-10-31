using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Migration.Steps.V7ToV8
{
    /// <summary>
    /// save[節][配列] を辿った結果。無い（キー欠損か JSON の null）・配列ありを派生型で分け、配列は配列ありの型だけが持つ
    /// The result of walking save[section][array]; absent (key missing or JSON null) and present are separate subtypes, and only the present one carries the array
    /// </summary>
    public abstract class NestedSaveArray
    {
    }

    // 節か配列のキーが無い、またはどちらかが JSON の null（読み手に区別する受益者がいない）
    // A section or array key is absent, or either is JSON null (no reader benefits from telling them apart)
    public sealed class AbsentNestedSaveArray : NestedSaveArray
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
