using System;
using System.Collections.Generic;
using Core.Master.Validator;
using Mooresmaster.Loader.FluidsModule;
using Mooresmaster.Model.FluidsModule;
using Newtonsoft.Json.Linq;
using UnitGenerator;

namespace Core.Master
{
    /// <summary>
    ///     通信用の液体ID
    /// </summary>
    /// <remarks>
    ///     このIDは永続化されることはなく、メモリ上、ネットワーク通信上でのみ使用する値
    /// </remarks>
    [UnitOf(typeof(int), UnitGenerateOptions.MessagePackFormatter | UnitGenerateOptions.Comparable)]
    public partial struct FluidId
    {
    }
    
    public class FluidMaster : IMasterValidator
    {
        public static readonly FluidId EmptyFluidId = new(0);
        public static readonly Guid MixedFluidGuid = Guid.Parse("00000000-0000-0000-0000-000000000001");

        public readonly Fluids Fluids;

        private Dictionary<FluidId, FluidMasterElement> _fluidElementTableById;
        private Dictionary<Guid, FluidId> _fluidGuidToFluidId;

        public FluidMaster(JToken jToken)
        {
            Fluids = FluidsLoader.Load(jToken);
        }

        public bool Validate(out string errorLogs)
        {
            return FluidMasterUtil.Validate(Fluids, out errorLogs);
        }

        public void Initialize()
        {
            FluidMasterUtil.Initialize(Fluids, MixedFluidGuid, out _fluidElementTableById, out _fluidGuidToFluidId);
        }

        public FluidMasterElement GetFluidMaster(FluidId fluidId)
        {
            if (!_fluidElementTableById.TryGetValue(fluidId, out var element))
            {
                throw new InvalidOperationException($"FluidElement not found. FluidId:{fluidId}");
            }
            
            return element;
        }
        
        public FluidMasterElement GetFluidMaster(Guid fluidGuid)
        {
            var fluidId = GetFluidId(fluidGuid);
            return GetFluidMaster(fluidId);
        }
        
        public FluidId GetFluidId(Guid fluidGuid)
        {
            if (fluidGuid == Guid.Empty)
            {
                return EmptyFluidId;
            }

            if (!_fluidGuidToFluidId.TryGetValue(fluidGuid, out var id))
            {
                throw new InvalidOperationException($"FluidElement not found. FluidGuid:{fluidGuid}");
            }

            return id;
        }

        public FluidId? GetFluidIdOrNull(Guid fluidGuid)
        {
            if (fluidGuid == Guid.Empty)
            {
                return EmptyFluidId;
            }

            if (!_fluidGuidToFluidId.TryGetValue(fluidGuid, out var id))
            {
                return null;
            }

            return id;
        }
        
        public IEnumerable<FluidId> GetAllFluidIds()
        {
            return _fluidElementTableById.Keys;
        }
    }
}