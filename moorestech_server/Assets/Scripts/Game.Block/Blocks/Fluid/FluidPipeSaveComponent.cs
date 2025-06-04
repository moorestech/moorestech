using System;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Fluid;
using Newtonsoft.Json;

namespace Game.Block.Blocks.Fluid
{
    public class FluidPipeSaveComponent : IBlockSaveState
    {
        private readonly FluidPipeComponent _fluidPipeComponent;
        
        public FluidPipeSaveComponent(FluidPipeComponent fluidPipeComponent)
        {
            _fluidPipeComponent = fluidPipeComponent;
        }
        
        public bool IsDestroy { get; private set; }
        
        public void Destroy()
        {
            IsDestroy = true;
        }
        
        public static string SaveKeyStatic { get; } = typeof(FluidPipeSaveComponent).FullName;
        public string SaveKey { get; } = SaveKeyStatic;
        
        public string GetSaveState()
        {
            BlockException.CheckDestroy(this);
            
            var stateDetail = _fluidPipeComponent.GetFluidPipeStateDetail();
            var jsonObject = new FluidPipeSaveJsonObject
            {
                FluidIdValue = stateDetail.FluidId.AsPrimitive(),
                Amount = stateDetail.Amount,
                Capacity = stateDetail.Capacity
            };
            
            return JsonConvert.SerializeObject(jsonObject);
        }
    }
    
    public class FluidPipeSaveJsonObject
    {
        [JsonProperty("fluidId")]
        public int FluidIdValue;
        
        [JsonIgnore]
        public FluidId FluidId => new FluidId(FluidIdValue);
        
        [JsonProperty("amount")]
        public float Amount;
        
        [JsonProperty("capacity")]
        public float Capacity;
    }
}