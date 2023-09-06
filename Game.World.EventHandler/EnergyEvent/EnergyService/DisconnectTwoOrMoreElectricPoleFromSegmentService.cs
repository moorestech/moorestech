using System.Collections.Generic;
using Core.Block.Blocks;
using Core.Block.Config;
using Core.Block.Config.LoadConfig.Param;
using Core.Electric;
using Core.EnergySystem;
using Game.World.Interface.DataStore;

namespace Game.World.EventHandler.Service
{
    public class DisconnectTwoOrMoreElectricPoleFromSegmentService<TSegment> where TSegment : EnergySegment, new()
    { 
        private readonly IWorldBlockComponentDatastore<IEnergyTransformer> _electricPoleDatastore;
        private readonly IWorldBlockComponentDatastore<IBlockElectricConsumer> _electricDatastore;
        private readonly IWorldBlockComponentDatastore<IPowerGenerator> _powerGeneratorDatastore;
        private readonly IWorldEnergySegmentDatastore<TSegment> _worldEnergySegmentDatastore;
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        private readonly IBlockConfig _blockConfig;

        public DisconnectTwoOrMoreElectricPoleFromSegmentService(IWorldBlockComponentDatastore<IEnergyTransformer> electricPoleDatastore, IWorldBlockComponentDatastore<IBlockElectricConsumer> electricDatastore, IWorldBlockComponentDatastore<IPowerGenerator> powerGeneratorDatastore, IWorldEnergySegmentDatastore<TSegment> worldEnergySegmentDatastore, IWorldBlockDatastore worldBlockDatastore, IBlockConfig blockConfig)
        {
            _electricPoleDatastore = electricPoleDatastore;
            _electricDatastore = electricDatastore;
            _powerGeneratorDatastore = powerGeneratorDatastore;
            _worldEnergySegmentDatastore = worldEnergySegmentDatastore;
            _worldBlockDatastore = worldBlockDatastore;
            _blockConfig = blockConfig;
        }

        public void Disconnect(IEnergyTransformer removedElectricPole)
        {
            //データを取得
            var removedSegment = _worldEnergySegmentDatastore.GetEnergySegment(removedElectricPole);
            
            //自身が所属していたセグメントの電柱のリストを取る
            var connectedElectricPoles = new List<IEnergyTransformer>();
            foreach (var onePole in removedSegment.EnergyTransformers) connectedElectricPoles.Add(onePole.Value);
            //この電柱のリストをもとに電力セグメントを再構成するため、削除した電柱はリストから削除する
            connectedElectricPoles.Remove(removedElectricPole);
            
            
            //元のセグメントを消す
            _worldEnergySegmentDatastore.RemoveEnergySegment(removedSegment);


            //電柱を全て探索し、電力セグメントを再構成する
            //1個ずつ電柱を取り出し、電力セグメントに追加する
            //電力セグメントに追加した電柱はリストから削除する
            //電柱のリストが空になるまで繰り返す
            while (connectedElectricPoles.Count != 0)
            {
                var (newElectricPoles, newBlocks, newGenerators) =
                    GetElectricPoles(
                        connectedElectricPoles[0],
                        removedElectricPole,
                        new Dictionary<int, IEnergyTransformer>(),
                        new Dictionary<int, IBlockElectricConsumer>(),
                        new Dictionary<int, IPowerGenerator>());


                //新しいセグメントに電柱、ブロック、発電機を追加する
                var newElectricSegment = _worldEnergySegmentDatastore.CreateEnergySegment();
                foreach (var newElectric in newElectricPoles)
                {
                    newElectricSegment.AddEnergyTransformer(newElectric.Value);
                    //今までの電柱リストから削除する
                    connectedElectricPoles.Remove(newElectric.Value);
                }

                foreach (var newBlock in newBlocks) newElectricSegment.AddEnergyConsumer(newBlock.Value);
                foreach (var newGenerator in newGenerators) newElectricSegment.AddGenerator(newGenerator.Value);
            }
        }

        //再帰的に電柱を探索する 
        private (Dictionary<int, IEnergyTransformer>, Dictionary<int, IBlockElectricConsumer>, Dictionary<int, IPowerGenerator>)
            GetElectricPoles(
                IEnergyTransformer electricPole,
                IEnergyTransformer removedElectricPole,
                Dictionary<int, IEnergyTransformer> electricPoles,
                Dictionary<int, IBlockElectricConsumer> blockElectrics,
                Dictionary<int, IPowerGenerator> powerGenerators)
        {
            var (x, y) = _worldBlockDatastore.GetBlockPosition(electricPole.EntityId);
            var poleConfig =
                _blockConfig.GetBlockConfig(((IBlock) electricPole).BlockId).Param as ElectricPoleConfigParam;


            //周辺の機械、発電機を取得
            var (newBlocks, newGenerators) =
                new FindMachineAndGeneratorFromPeripheralService().Find(x, y, poleConfig, _electricDatastore,
                    _powerGeneratorDatastore);
            //ブロックと発電機を追加
            foreach (var block in newBlocks)
            {
                if (blockElectrics.ContainsKey(block.EntityId)) continue;
                blockElectrics.Add(block.EntityId, block);
            }
            foreach (var generator in newGenerators)
            {
                if (powerGenerators.ContainsKey(generator.EntityId)) continue;
                powerGenerators.Add(generator.EntityId, generator);
            }
            
            
            

            //周辺の電柱を取得
            var peripheralElectricPoles =
                new FindElectricPoleFromPeripheralService().Find(x, y, poleConfig, _electricPoleDatastore);
            //削除された電柱は除く
            peripheralElectricPoles.Remove(removedElectricPole);
            //自身の電柱は追加する
            electricPoles.Add(electricPole.EntityId, electricPole);
            //周辺に電柱がない場合は終了
            if (peripheralElectricPoles.Count == 0) return (electricPoles, blockElectrics, powerGenerators);


            
            
            //周辺の電柱を再帰的に取得する
            foreach (var peripheralElectricPole in peripheralElectricPoles)
            {
                //もしもすでに追加されていた電柱ならスキップ
                if (electricPoles.ContainsKey(peripheralElectricPole.EntityId)) continue;
                //追加されていない電柱なら追加
                (electricPoles, blockElectrics, powerGenerators) =
                    GetElectricPoles(peripheralElectricPole, removedElectricPole, electricPoles, blockElectrics,
                        powerGenerators);
            }

            return (electricPoles, blockElectrics, powerGenerators);
        }
    }
}