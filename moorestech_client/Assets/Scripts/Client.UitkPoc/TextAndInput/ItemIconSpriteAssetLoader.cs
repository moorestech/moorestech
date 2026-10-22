using System.Collections.Generic;
using System.IO;
using Client.UitkPoc.Shell;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.TextCore.Text;

namespace Client.UitkPoc.TextAndInput
{
    // items.jsonの全アイテムのアイコンを集めて、文中アイコン用のSpriteAssetにする
    // Gathers every item icon from items.json into a SpriteAsset for inline icons
    public static class ItemIconSpriteAssetLoader
    {
        public static SpriteAsset LoadAllItems(UitkPocContext context)
        {
            var items = JObject.Parse(File.ReadAllText(Path.Combine(context.ModDirectory, "master", "items.json")))["data"];
            var icons = new List<(string name, Texture2D icon)>();
            foreach (var item in items)
            {
                // アイコンの無いアイテムはLoadItemIconが警告済みなので、ここでは載せないだけにする
                // Items without icons were already warned by LoadItemIcon, so they are simply left out
                var name = (string)item["name"];
                var icon = context.LoadItemIcon(name);
                if (icon != null) icons.Add((name, icon));
            }
            return RuntimeSpriteAssetBuilder.Build(icons);
        }
    }
}
