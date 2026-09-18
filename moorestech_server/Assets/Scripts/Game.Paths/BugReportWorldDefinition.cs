using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Game.Paths
{
    // manifest.worldDefinition の値。書き側（クライアント）と読み側（再現ツール）が同じ型を見る。ワイヤの語は EnumMember が綴る（ADR 0064）
    // 生成ワールドは world.json だけを入れ、再現側は同梱スナップショット／共有キャッシュから地形を引き当てる。ワールドを取り込めなかった箱は NotCaptured を名乗り、null の第3状態を作らない
    // Values of manifest.worldDefinition; the writer (client) and the reader (reproduction tools) share this type and EnumMember spells the wire words (ADR 0064)
    // A generated world ships only world.json and the reproducer restores its terrain from the bundled snapshot / shared cache; a box whose world was not captured says NotCaptured instead of leaving null as a third state
    [JsonConverter(typeof(StringEnumConverter))]
    public enum BugReportWorldDefinition
    {
        [EnumMember(Value = "not-captured")] NotCaptured,
        [EnumMember(Value = "full")] Full,
        [EnumMember(Value = "generated-world-json-only")] GeneratedWorldJsonOnly,
    }
}
