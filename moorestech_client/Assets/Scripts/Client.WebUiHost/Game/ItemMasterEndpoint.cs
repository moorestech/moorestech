using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Client.WebUiHost.Common;
using Core.Master;
using Microsoft.AspNetCore.Http;

namespace Client.WebUiHost.Game
{
    /// <summary>
    /// GET /api/master/items でアイテムマスタ（名前・スタック上限）を配信する
    /// Serves item master data (name, max stack) at GET /api/master/items
    /// </summary>
    public static class ItemMasterEndpoint
    {
        public const string Path = "/api/master/items";

        private static string _cachedJson;

        public static void ClearCache()
        {
            _cachedJson = null;
        }

        public static async Task HandleAsync(HttpContext context)
        {
            // マスタロード完了前のリクエストは 503 を返す
            // Requests arriving before master data is loaded get a 503
            if (MasterHolder.ItemMaster == null)
            {
                context.Response.StatusCode = 503;
                return;
            }

            _cachedJson ??= BuildJson();
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsync(_cachedJson, CancellationToken.None);
        }

        private static string BuildJson()
        {
            var dto = new ItemMasterListDto { Items = new List<ItemMasterDto>() };
            foreach (var itemId in MasterHolder.ItemMaster.GetItemAllIds())
            {
                var master = MasterHolder.ItemMaster.GetItemMaster(itemId);
                dto.Items.Add(new ItemMasterDto
                {
                    ItemId = itemId.AsPrimitive(),
                    Name = master.Name,
                    MaxStack = master.MaxStack,
                });
            }
            return WebUiJson.Serialize(dto);
        }
    }

    /// <summary>
    /// /api/master/items の配信 DTO
    /// Payload DTO for /api/master/items
    /// </summary>
    public class ItemMasterListDto
    {
        public List<ItemMasterDto> Items;
    }

    public class ItemMasterDto
    {
        public int ItemId;
        public string Name;
        public int MaxStack;
    }
}
