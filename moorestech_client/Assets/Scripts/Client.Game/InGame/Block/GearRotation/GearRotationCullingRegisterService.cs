using Client.Game.InGame.Block.DistanceCulling;
using Client.Game.InGame.BlockSystem.StateProcessor;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.Block.GearRotation
{
    /// <summary>
    /// ブロックを歯車回転の距離判定へ占有セルの範囲で登録する
    /// Registers blocks with the gear-rotation distance culling using their occupied cells
    /// </summary>
    public sealed class GearRotationCullingRegisterService : IStartable, IBlockDistanceCullingSource
    {
        private readonly BlockDistanceCullingRegistrar _registrar;

        public GearRotationCullingRegisterService(BlockGameObjectDataStore dataStore, GearRotationCullingManager manager)
        {
            _registrar = new BlockDistanceCullingRegistrar(dataStore, manager.Culling, this);
        }

        public void Start()
        {
            _registrar.Initialize();
        }

        public BlockDistanceCullingEntry CreateEntry(BlockGameObject block)
        {
            // 歯車を持たないブロックも空の対象で登録し、登録経路を一本に保つ
            // Blocks without gears register an empty target so the registration path stays single
            var posInfo = block.BlockPosInfo;
            var bounds = new Bounds();
            bounds.SetMinMax(posInfo.MinPos, posInfo.MaxPos + Vector3Int.one);
            var processors = block.GetComponentsInChildren<GearStateChangeProcessor>(true);
            return new BlockDistanceCullingEntry(bounds, new GearRotationCullingTarget(processors));
        }
    }
}
