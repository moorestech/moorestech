using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Model.FluidsModule;

namespace Core.Master.Validator
{
    public static class FluidMasterUtil
    {
        public static bool Validate(Fluids fluids, out string errorLogs)
        {
            // FluidMasterは外部キー依存がないため、バリデーション成功を返す
            // FluidMaster has no external key dependencies, so return success
            errorLogs = "";
            return true;
        }

        public static void Initialize(
            Fluids fluids,
            Guid mixedFluidGuid,
            out Dictionary<FluidId, FluidMasterElement> fluidElementTableById,
            out Dictionary<Guid, FluidId> fluidGuidToFluidId)
        {
            // guidでソート
            // Sort by GUID
            var sortedFluidElements = fluids.Data
                .OrderBy(e => e.FluidGuid)
                .ToList();

            // 予約されている混ざった液体を追加
            // Add reserved mixed fluid
            sortedFluidElements.Add(new FluidMasterElement("MixedFluid", mixedFluidGuid));

            // FluidID 0は空の液体として予約しているので、1から始める
            // Fluid ID 0 is reserved for empty fluid, so start from 1
            fluidElementTableById = new Dictionary<FluidId, FluidMasterElement>();
            fluidGuidToFluidId = new Dictionary<Guid, FluidId>();
            for (var i = 0; i < sortedFluidElements.Count; i++)
            {
                var fluidId = new FluidId(i + 1);
                var element = sortedFluidElements[i];

                fluidElementTableById.Add(fluidId, element);
                fluidGuidToFluidId.Add(element.FluidGuid, fluidId);
            }
        }
    }
}
