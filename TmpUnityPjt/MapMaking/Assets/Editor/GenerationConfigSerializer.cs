using System;
using System.Collections.Generic;
using System.Reflection;
using MapGenerator.Pipeline.Config;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MapGenerator.EditorExport
{
    // TerrainGenerationConfig(SO)群を generation.yml スキーマ形状の JSON へ写経するリフレクション変換器。
    // Reflection serializer transcribing the TerrainGenerationConfig SO graph into generation.yml-shaped JSON.
    public class GenerationConfigSerializer
    {
        private readonly Dictionary<string, string> _objectGuids;
        private readonly Dictionary<string, string> _veinGuids;

        // 空アセット欄と未解決プレハブを収集し、実行後に警告ログで列挙する
        // Collects empty asset fields and unresolved prefabs to warn-log after the run.
        public readonly List<string> EmptyAssetFields = new List<string>();
        public readonly List<string> UnmatchedPrefabs = new List<string>();

        public GenerationConfigSerializer(Dictionary<string, string> objectGuids, Dictionary<string, string> veinGuids)
        {
            _objectGuids = objectGuids;
            _veinGuids = veinGuids;
        }

        // 公開インスタンスフィールドを走査し、型別ルールでキーと値を確定する
        // Walks public instance fields, deciding key and value per type-driven rules.
        public JObject SerializeObject(object owner)
        {
            var jo = new JObject();
            foreach (var f in owner.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                EmitField(jo, owner, f);

            // WorldOreConfig は fluidEntries を持たないためスキーマ必須欄を空配列で補う
            // WorldOreConfig lacks fluidEntries, so add the schema-required key as an empty array.
            if (owner is WorldOreConfig)
                jo["fluidEntries"] = new JArray();
            return jo;
        }

        private void EmitField(JObject jo, object owner, FieldInfo f)
        {
            var t = f.FieldType;
            var name = f.Name;
            var value = f.GetValue(owner);

            // PlacementNoise.texture は全プリセット未使用のためスキーマから削除済み
            // PlacementNoise.texture is removed from the schema (unused across presets).
            if (owner is PlacementNoise && name == "texture") return;

            // 単一 TerrainLayer/Texture2D は addressablePath 文字列(P1では空)へ置換
            // Single TerrainLayer/Texture2D becomes an addressablePath string (empty in P1).
            if (t == typeof(TerrainLayer) || t == typeof(Texture2D))
            {
                jo[name + "AddressablePath"] = "";
                RecordEmptyAsset(owner, name);
                return;
            }

            // TerrainLayer[]（alpine debug）は addressablePaths 文字列配列へ
            // TerrainLayer[] (alpine debug) becomes an addressablePaths string array.
            if (t == typeof(TerrainLayer[]))
            {
                var arr = new JArray();
                var layers = (TerrainLayer[])value;
                if (layers != null)
                    foreach (var _ in layers) { arr.Add(""); RecordEmptyAsset(owner, name); }
                var key = name.EndsWith("s") ? name.Substring(0, name.Length - 1) : name;
                jo[key + "AddressablePaths"] = arr;
                return;
            }

            // GameObject prototypeMesh は detail のメッシュ addressable(P1では空)
            // GameObject prototypeMesh is a detail mesh addressable (empty in P1).
            if (t == typeof(GameObject) && name == "prototypeMesh")
            {
                jo["prototypeMeshAddressablePath"] = "";
                RecordEmptyAsset(owner, name);
                return;
            }

            // OreEntry.prefab は鉱脈識別のため veinGuid(mapVeins) へ置換
            // OreEntry.prefab is replaced with veinGuid (mapVeins) identifying the vein.
            if (t == typeof(GameObject))
            {
                jo["veinGuid"] = ResolveVeinGuid((GameObject)value);
                return;
            }

            // GameObject[] は mapObjectGuid 参照配列へ。木配置のみキー名 prefabs→mapObjects
            // GameObject[] becomes a mapObjectGuid ref array; tree placement renames prefabs to mapObjects.
            if (t == typeof(GameObject[]))
            {
                var arr = new JArray();
                var prefabs = (GameObject[])value;
                if (prefabs != null)
                    foreach (var go in prefabs)
                        arr.Add(new JObject { ["mapObjectGuid"] = ResolveObjectGuid(go) });
                var key = owner is TreePrototypeEntry ? "mapObjects" : name;
                jo[key] = arr;
                return;
            }

            jo[name] = SerializeValue(value, t);
        }

        private JToken SerializeValue(object value, Type t)
        {
            // ベクトル・色は配列表現（vector2/3/4）。AnimationCurve は keyframe 配列
            // Vectors/colors as arrays (vector2/3/4); AnimationCurve as a keyframe array.
            if (t == typeof(Vector2)) { var v = (Vector2)value; return new JArray(v.x, v.y); }
            if (t == typeof(Vector3)) { var v = (Vector3)value; return new JArray(v.x, v.y, v.z); }
            if (t == typeof(Vector4)) { var v = (Vector4)value; return new JArray(v.x, v.y, v.z, v.w); }
            if (t == typeof(Color)) { var c = (Color)value; return new JArray(c.r, c.g, c.b, c.a); }
            if (t == typeof(AnimationCurve)) return SerializeCurve((AnimationCurve)value);

            // enum: [Flags]（BiomeFlags）は名前配列、それ以外はオプション名文字列
            // enum: [Flags] (BiomeFlags) to a name array, otherwise the option-name string.
            if (t.IsEnum)
            {
                if (t.IsDefined(typeof(FlagsAttribute), false)) return SerializeFlags(value, t);
                return value.ToString();
            }

            if (t == typeof(bool) || t == typeof(int) || t == typeof(float) || t == typeof(string))
                return new JValue(value);

            // 配列は要素型で再帰。要素がクラスなら SerializeObject
            // Arrays recurse per element type; class elements go through SerializeObject.
            if (t.IsArray)
            {
                var arr = new JArray();
                var elemType = t.GetElementType();
                if (value is Array a)
                    foreach (var e in a)
                        arr.Add(IsLeaf(elemType) ? SerializeValue(e, elemType) : SerializeObject(e));
                return arr;
            }

            // 残りは入れ子の [Serializable] クラス。null なら既定インスタンスを写経
            // Remaining is a nested [Serializable] class; a null field is transcribed from a default instance.
            if (value == null) value = Activator.CreateInstance(t);
            return SerializeObject(value);
        }

        private JArray SerializeCurve(AnimationCurve curve)
        {
            var arr = new JArray();
            if (curve != null)
                foreach (var k in curve.keys)
                    arr.Add(new JObject
                    {
                        ["time"] = k.time, ["value"] = k.value,
                        ["inTangent"] = k.inTangent, ["outTangent"] = k.outTangent
                    });
            return arr;
        }

        private JArray SerializeFlags(object value, Type t)
        {
            var arr = new JArray();
            var mask = Convert.ToInt64(value);
            foreach (var name in Enum.GetNames(t))
            {
                var bit = Convert.ToInt64(Enum.Parse(t, name));
                if (bit != 0 && (mask & bit) == bit) arr.Add(name);
            }
            return arr;
        }

        private static bool IsLeaf(Type t)
        {
            return t == typeof(bool) || t == typeof(int) || t == typeof(float)
                   || t == typeof(string) || t.IsEnum;
        }

        private string ResolveVeinGuid(GameObject go)
        {
            if (go != null && _veinGuids.TryGetValue(go.name, out var guid)) return guid;
            if (go != null) UnmatchedPrefabs.Add($"vein:{go.name}");
            return "";
        }

        private string ResolveObjectGuid(GameObject go)
        {
            if (go != null && _objectGuids.TryGetValue(go.name, out var guid)) return guid;
            if (go != null) UnmatchedPrefabs.Add($"mapObject:{go.name}");
            return "";
        }

        private void RecordEmptyAsset(object owner, string field)
        {
            EmptyAssetFields.Add($"{owner.GetType().Name}.{field}");
        }
    }
}
