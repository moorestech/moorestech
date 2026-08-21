using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Client.WebUiHost.Common;
using Core.Master;
using Microsoft.AspNetCore.Http;

namespace Client.WebUiHost.Game
{
    /// <summary>
    /// GET /api/master/fluids で液体マスタ（Guid・背面フィル色）を配信する
    /// Serves fluid master data (Guid, fill color) at GET /api/master/fluids
    /// </summary>
    public static class FluidMasterEndpoint
    {
        public const string Path = "/api/master/fluids";

        public static async Task HandleAsync(HttpContext context)
        {
            // マスタロード完了前のリクエストは 503 を返す
            // Requests arriving before master data is loaded get a 503
            if (MasterHolder.FluidMaster == null)
            {
                context.Response.StatusCode = 503;
                return;
            }

            var json = WebUiJson.Serialize(BuildResponse());

            // FluidId は非永続のためブラウザにキャッシュさせない
            // FluidIds are not persistent, so tell the browser not to cache this
            context.Response.Headers["Cache-Control"] = "no-store";
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsync(json, CancellationToken.None);
        }

        public static FluidMasterListDto BuildResponse()
        {
            var dto = new FluidMasterListDto { Fluids = new List<FluidMasterDto>() };
            foreach (var fluidId in MasterHolder.FluidMaster.GetAllFluidIds())
            {
                var master = MasterHolder.FluidMaster.GetFluidMaster(fluidId);
                dto.Fluids.Add(new FluidMasterDto
                {
                    FluidId = fluidId.AsPrimitive(),
                    FluidGuid = master.FluidGuid.ToString("D"),
                    Color = master.Color,
                });
            }
            return dto;
        }
    }

    /// <summary>
    /// /api/master/fluids の配信 DTO
    /// Payload DTO for /api/master/fluids
    /// </summary>
    public class FluidMasterListDto
    {
        public List<FluidMasterDto> Fluids;
    }

    public class FluidMasterDto
    {
        public int FluidId;
        public string FluidGuid;
        public string Color;
    }
}
