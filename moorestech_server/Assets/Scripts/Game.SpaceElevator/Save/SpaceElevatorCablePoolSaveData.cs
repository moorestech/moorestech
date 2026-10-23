using System;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.SpaceElevator.Save
{
    public class SpaceElevatorCablePoolSaveData
    {
        [JsonProperty("cableCount", Required = Required.Always)]
        [JsonConverter(typeof(SpaceElevatorCableCountJsonConverter))]
        public int CableCount;
    }

    // 外部セーブの数値を暗黙変換せず、整数としてだけ受け取る
    // Accept only integer values from external saves without implicit numeric conversion
    internal sealed class SpaceElevatorCableCountJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(int);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            // 小数・文字列・範囲外を残量に変換すると、破損した原本を上書きしてしまう
            // Converting fractions, strings or out-of-range numbers would overwrite the corrupt original with a fabricated amount
            if (reader.TokenType == JsonToken.Integer)
            {
                if (reader.Value is int count) return count;
                if (reader.Value is long wideCount && wideCount >= int.MinValue && wideCount <= int.MaxValue) return (int)wideCount;
            }
            var reason = $"spaceElevatorCablePool.cableCount はint範囲の整数が必須です path:{reader.Path} value:{reader.Value}";
            Debug.LogError(reason);
            throw new JsonSerializationException(reason);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue((int)value);
        }
    }
}
