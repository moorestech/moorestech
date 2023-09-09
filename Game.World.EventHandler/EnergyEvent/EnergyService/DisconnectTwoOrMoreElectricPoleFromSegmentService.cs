using System.Collections.Generic;
using Core.Block.Blocks;
using Core.Block.Config.LoadConfig.Param;
using Core.Electric;
using Core.EnergySystem;
using Game.World.EventHandler.Service;

namespace Game.World.EventHandler.EnergyEvent.EnergyService
{
    public static class DisconnectTwoOrMoreElectricPoleFromSegmentService<TSegment,TConsumer,TGenerator,TTransformer> 
        where TSegment : EnergySegment, new()
        where TConsumer : IBlockElectricConsumer
        where TGenerator : IPowerGenerator
        where TTransformer : IEnergyTransformer
    { 
        public static void Disconnect(IEnergyTransformer removedElectricPole,EnergyServiceDependencyContainer<TSegment> container)
        {
            //データを取得
            var removedSegment = container.WorldEnergySegmentDatastore.GetEnergySegment(removedElectricPole);
            
            //自身が所属していたセグメントの電柱のリストを取る
            var connectedElectricPoles = new List<IEnergyTransformer>();
            foreach (var onePole in removedSegment.EnergyTransformers) connectedElectricPoles.Add(onePole.Value);
            //この電柱のリストをもとに電力セグメントを再構成するため、削除した電柱はリストから削除する
            connectedElectricPoles.Remove(removedElectricPole);
            
            
            //元のセグメントを消す
            container.WorldEnergySegmentDatastore.RemoveEnergySegment(removedSegment);


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
                        new Dictionary<int, IPowerGenerator>(),container);


                //新しいセグメントに電柱、ブロック、発電機を追加する
                var newElectricSegment = container.WorldEnergySegmentDatastore.CreateEnergySegment();
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
        private static (Dictionary<int, IEnergyTransformer>, Dictionary<int, IBlockElectricConsumer>, Dictionary<int, IPowerGenerator>)
            GetElectricPoles(
                IEnergyTransformer electricPole,
                IEnergyTransformer removedElectricPole,
                Dictionary<int, IEnergyTransformer> electricPoles,
                Dictionary<int, IBlockElectricConsumer> blockElectrics,
                Dictionary<int, IPowerGenerator> powerGenerators,EnergyServiceDependencyContainer<TSegment> container)
        {
            var (x, y) = container.WorldBlockDatastore.GetBlockPosition(electricPole.EntityId);
            var poleConfig =
                container.BlockConfig.GetBlockConfig(((IBlock) electricPole).BlockId).Param as ElectricPoleConfigParam;


            //周辺の機械、発電機を取得
            var (newBlocks, newGenerators) =
                FindMachineAndGeneratorFromPeripheralService.Find(x, y, poleConfig, container.WorldBlockDatastore);
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
            var peripheralElectricPoles = FindElectricPoleFromPeripheralService.Find(x, y, poleConfig, container.WorldBlockDatastore);
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
                        powerGenerators,container);
            }

            return (electricPoles, blockElectrics, powerGenerators);
        }
    }
}