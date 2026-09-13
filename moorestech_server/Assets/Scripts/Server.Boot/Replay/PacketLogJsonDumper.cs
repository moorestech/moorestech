using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.SaveLoad.Snapshot;
using MessagePack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Server.Protocol;

namespace Server.Boot.Replay
{
    // パケットログを人とエージェントが読める JSON Lines にする。tag は ProtocolMessagePackBase の Key(0)
    // Turns a packet log into JSON Lines readable by people and agents; tag is Key(0) of ProtocolMessagePackBase
    public static class PacketLogJsonDumper
    {
        public static int Dump(IEnumerable<string> segmentFilePaths, string outputJsonlPath)
        {
            var records = ReceivedPacketLogReader.ReadAll(segmentFilePaths);
            var builder = new StringBuilder();
            foreach (var record in records)
            {
                var tag = MessagePackSerializer.Deserialize<ProtocolMessagePackBase>(record.Payload).Tag;
                var json = MessagePackSerializer.ConvertToJson(record.Payload);
                var line = new JObject { ["tick"] = record.Tick, ["tag"] = tag, ["json"] = JToken.Parse(json) };
                builder.Append(line.ToString(Formatting.None)).Append('\n');
            }
            File.WriteAllText(outputJsonlPath, builder.ToString());
            return records.Count;
        }
    }
}
