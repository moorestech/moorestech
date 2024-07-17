// using System;
// using System.Collections.Generic;
// using mooresmaster.Common;
// using Newtonsoft.Json.Linq;
//
// public class ItemConfig
// {
//     public List<ItemsElement> Items { get; init; }
// }
//
// public class ItemsElement
// {
//     public ItemId ItemId { get; init; }
//     public int MaxStack { get; init; }
// }
//
// public struct ItemId
// {
//     private readonly Guid value;
//
//     public ItemId(Guid value)
//     {
//         this.value = value;
//     }
// }
//
// public static class ItemLoader
// {
//     public static ItemConfig LoadItemConfig(List<ModConfigInfo> sortedConfigs)
//     {
//         List<ItemsElement> items = new List<ItemsElement>();
//
//         foreach (var config in sortedConfigs)
//         {
//             if (!config.ConfigJsons.TryGetValue("item", out var jsonText)) continue;
//
//             dynamic jsonObject = JObject.Parse(jsonText);
//
//
//             foreach (var itemsJsonElement in jsonObject.items)
//             {
//                 string ItemIdStr = itemsJsonElement.itemId;
//                 ItemId ItemId = new ItemId(new Guid(ItemIdStr));
//
//                 int MaxStack = itemsJsonElement.maxStack;
//
//                 ItemsElement itemsElementObject = new ItemsElement()
//                 {
//                     ItemId = ItemId,
//                     MaxStack = MaxStack,
//                 };
//
//                 items.Add(itemsElementObject);
//             }
//         }
//
//         return new ItemConfig()
//         {
//             Items = items,
//         };
//     }
// }


