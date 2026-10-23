using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.Tests.EditModeInPlayingTest.BlockInventory
{
    // 起動用Modに検証対象の定義だけを加え、共有の原本を保つ
    // Add only the tested definitions to a private boot mod, preserving shared originals
    internal static class SpaceElevatorActionTestMod
    {
        internal static string Create()
        {
            var root = Path.Combine(Path.GetTempPath(), $"elevator-action-{Guid.NewGuid()}");
            var source = Util.EditModeInPlayingTestUtil.EditModeInPlayingTestServerDirectoryPath;
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories).Where(p => !p.EndsWith(".meta")))
            {
                var destination = Path.Combine(root, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(file, destination);
            }
            var serverMaster = Path.GetFullPath(Path.Combine(Application.dataPath, "../../moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master"));
            var targetMaster = Directory.GetFiles(root, "blocks.json", SearchOption.AllDirectories).Single();
            var targetItemsPath = Path.Combine(Path.GetDirectoryName(targetMaster), "items.json");
            var originalItems = JObject.Parse(File.ReadAllText(Path.Combine(serverMaster, "items.json")));
            var items = JObject.Parse(File.ReadAllText(targetItemsPath));
            var baseBlock = JObject.Parse(File.ReadAllText(Path.Combine(serverMaster, "blocks.json")))["data"]
                .Single(b => (string)b["blockType"] == "SpaceElevatorBase");
            baseBlock["blockPrefabAddressablesPath"] = "Vanilla/Block/SpaceElevatorBase";
            var param = baseBlock["blockParam"];
            var required = ((JArray)param["carriedPayloadItemGuids"]).Select(g => (string)g).Append((string)param["cableItemGuid"]).ToHashSet();
            foreach (var payload in originalItems["orbitalPayloads"].Where(p => required.Contains((string)p["payloadItemGuid"])))
            {
                ((JArray)items["orbitalPayloads"]).Add(payload.DeepClone());
                foreach (var output in payload["arrivalOutputs"]) required.Add((string)output["itemGuid"]);
            }
            foreach (var item in originalItems["data"].Where(i => required.Contains((string)i["itemGuid"])))
            {
                ((JArray)items["data"]).Add(item.DeepClone());
                var tableGuid = (string)item["stackLevelTableGuid"];
                if (items["itemStackLevelTables"].Any(t => (string)t["tableGuid"] == tableGuid)) continue;
                ((JArray)items["itemStackLevelTables"]).Add(originalItems["itemStackLevelTables"].Single(t => (string)t["tableGuid"] == tableGuid).DeepClone());
            }
            var blocks = JObject.Parse(File.ReadAllText(targetMaster));
            baseBlock["category"] = blocks["data"][0]["category"].DeepClone();
            baseBlock["subCategory"] = blocks["data"][0]["subCategory"].DeepClone();
            ((JArray)blocks["data"]).Add(baseBlock.DeepClone());
            File.WriteAllText(targetMaster, blocks.ToString());
            File.WriteAllText(targetItemsPath, items.ToString());
            return root;
        }
    }
}
