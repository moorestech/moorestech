using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Newtonsoft.Json;
using UnityEngine;

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

            var node = _fluidPipeComponent.Node;
            var jsonObject = new FluidPipeSaveJsonObject
            {
                FluidGuidStr = MasterHolder.FluidMaster.GetFluidGuid(node.FluidId).ToString(),
                Amount = node.Amount,
            };

            // 正準側として所有する面の速度を方向つきで保存する（波の状態をロード後も引き継ぐ）
            // Persist velocities of faces owned as the canonical side, keyed by direction, so wave state survives a reload
            var faceVelocities = new List<(Vector3Int direction, double velocity)>();
            FluidNetworkDatastore.CollectOwnedFaceVelocities(_fluidPipeComponent, faceVelocities);
            foreach (var (direction, velocity) in faceVelocities)
            {
                jsonObject.FaceVelocities.Add(new FluidPipeSaveJsonObject.FaceVelocityJsonObject
                {
                    X = direction.x,
                    Y = direction.y,
                    Z = direction.z,
                    Velocity = velocity,
                });
            }

            return JsonConvert.SerializeObject(jsonObject);
        }
    }
}
