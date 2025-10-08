using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.World.EventHandler.EnergyEvent.EnergyService;
using Game.World.Interface.DataStore;
using Mooresmaster.Model.BlocksModule;
using UniRx;
using UnityEngine;

namespace Game.World.EventHandler.EnergyEvent
{
    /// <summary>
    ///     電力を生産もしくは消費するブロックが設置されたときに、そのブロックを電柱に接続する
    /// </summary>
    public class ConnectMachineToElectricSegment<TSegment, TConsumer, TGenerator, TTransformer>
        where TSegment : EnergySegment, new()
        where TConsumer : IElectricConsumer
        where TGenerator : IElectricGenerator
        where TTransformer : IElectricTransformer
    {
        private readonly int _maxMachineConnectionHorizontalRange;
        private readonly int _maxMachineConnectionHeightRange;
        private readonly IWorldEnergySegmentDatastore<TSegment> _worldEnergySegmentDatastore;
        
        
        public ConnectMachineToElectricSegment(IWorldEnergySegmentDatastore<TSegment> worldEnergySegmentDatastore, MaxElectricPoleMachineConnectionRange maxElectricPoleMachineConnectionRange)
        {
            _worldEnergySegmentDatastore = worldEnergySegmentDatastore;
            _maxMachineConnectionHorizontalRange = maxElectricPoleMachineConnectionRange.GetHorizontal();
            _maxMachineConnectionHeightRange = maxElectricPoleMachineConnectionRange.GetHeight();
            ServerContext.WorldBlockUpdateEvent.OnBlockPlaceEvent.Subscribe(OnBlockPlace);
        }
        
        private void OnBlockPlace(BlockUpdateProperties updateProperties)
        {
            var machinePos = updateProperties.Pos;

            if (!IsElectricMachine(machinePos)) return;

            foreach (var polePos in EnumerateNearbyPoles(machinePos)) ConnectToElectricPole(polePos, machinePos);

            #region Internal

            IEnumerable<Vector3Int> EnumerateNearbyPoles(Vector3Int center)
            {
                var worldBlockDatastore = ServerContext.WorldBlockDatastore;
                var horizontalRange = Mathf.Max(_maxMachineConnectionHorizontalRange, 1);
                var heightRange = Mathf.Max(_maxMachineConnectionHeightRange, 1);

                var startX = center.x - horizontalRange / 2;
                var startZ = center.z - horizontalRange / 2;
                var startY = center.y - heightRange / 2;

                var endX = startX + horizontalRange;
                var endZ = startZ + horizontalRange;
                var endY = startY + heightRange;

                for (var x = startX; x < endX; x++)
                for (var y = startY; y < endY; y++)
                for (var z = startZ; z < endZ; z++)
                {
                    var polePos = new Vector3Int(x, y, z);
                    if (!worldBlockDatastore.ExistsComponent<IElectricTransformer>(polePos)) continue;

                    yield return polePos;
                }
            }

            #endregion
        }
        
        private bool IsElectricMachine(Vector3Int pos)
        {
            return ServerContext.WorldBlockDatastore.ExistsComponent<TGenerator>(pos) ||
                   ServerContext.WorldBlockDatastore.ExistsComponent<TConsumer>(pos);
        }
        
        
        /// <summary>
        ///     電柱のセグメントに機械を接続する
        /// </summary>
        private void ConnectToElectricPole(Vector3Int polePos, Vector3Int machinePos)
        {
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            
            //電柱を取得
            var block = worldBlockDatastore.GetBlock(polePos);
            var pole = block.GetComponent<TTransformer>();
            var configParam = (ElectricPoleBlockParam)block.BlockMasterElement.BlockParam;

            if (!IsWithinConnectionRange(machinePos, polePos, configParam)) return;

            var segment = _worldEnergySegmentDatastore.GetEnergySegment(pole);
            if (worldBlockDatastore.ExistsComponent<TGenerator>(machinePos))
                segment.AddGenerator(worldBlockDatastore.GetBlock<TGenerator>(machinePos));
            else if (worldBlockDatastore.ExistsComponent<TConsumer>(machinePos))
                segment.AddEnergyConsumer(worldBlockDatastore.GetBlock<TConsumer>(machinePos));

            #region Internal

            bool IsWithinConnectionRange(Vector3Int machine, Vector3Int polePosition, ElectricPoleBlockParam param)
            {
                return IsWithinHorizontalRange(machine, polePosition, param.MachineConnectionRange) &&
                       IsWithinHeightRange(machine, polePosition, param.MachineConnectionHeightRange);
            }

            bool IsWithinHorizontalRange(Vector3Int machine, Vector3Int polePosition, int range)
            {
                if (range <= 0) return machine.x == polePosition.x && machine.z == polePosition.z;
                var half = range / 2;
                var minX = polePosition.x - half;
                var minZ = polePosition.z - half;
                var maxX = minX + range - 1;
                var maxZ = minZ + range - 1;
                return machine.x >= minX && machine.x <= maxX && machine.z >= minZ && machine.z <= maxZ;
            }

            bool IsWithinHeightRange(Vector3Int machine, Vector3Int polePosition, int heightRange)
            {
                if (heightRange <= 0) return machine.y == polePosition.y;
                var half = heightRange / 2;
                var minY = polePosition.y - half;
                var maxY = minY + heightRange - 1;
                return machine.y >= minY && machine.y <= maxY;
            }

            #endregion
        }
    }
}
