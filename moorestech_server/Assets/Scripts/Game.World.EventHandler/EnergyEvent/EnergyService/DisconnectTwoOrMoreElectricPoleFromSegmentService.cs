using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using Game.EnergySystem;
using Mooresmaster.Model.BlocksModule;

namespace Game.World.EventHandler.EnergyEvent.EnergyService
{
    public static class DisconnectTwoOrMoreElectricPoleFromSegmentService<TSegment, TConsumer, TGenerator, TTransformer>
        where TSegment : EnergySegment, new()
        where TConsumer : IElectricConsumer
        where TGenerator : IElectricGenerator
        where TTransformer : IElectricTransformer
    {
        public static void Disconnect(IElectricTransformer removedElectricPole,
            EnergyServiceDependencyContainer<TSegment> container)
        {
            //データを取得
            var removedSegment = container.WorldEnergySegmentDatastore.GetEnergySegment(removedElectricPole);
            
            //自身が所属していたセグメントの電柱のリストを取る
            var connectedElectricPoles = new List<IElectricTransformer>();
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
                (var newElectricPoles, var newBlocks, var newGenerators) =
                    GetElectricPoles(
                        connectedElectricPoles[0],
                        removedElectricPole,
                        new Dictionary<BlockInstanceId, IElectricTransformer>(),
                        new Dictionary<BlockInstanceId, IElectricConsumer>(),
                        new Dictionary<BlockInstanceId, IElectricGenerator>(), container);
                
                
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
        private static (Dictionary<BlockInstanceId, IElectricTransformer>, Dictionary<BlockInstanceId, IElectricConsumer>,
            Dictionary<BlockInstanceId, IElectricGenerator>)
            GetElectricPoles(
                IElectricTransformer electricPole,
                IElectricTransformer removedElectricPole,
                Dictionary<BlockInstanceId, IElectricTransformer> electricPoles,
                Dictionary<BlockInstanceId, IElectricConsumer> blockElectrics,
                Dictionary<BlockInstanceId, IElectricGenerator> powerGenerators,
                EnergyServiceDependencyContainer<TSegment> container)
        {
            var pos = ServerContext.WorldBlockDatastore.GetBlockPosition(electricPole.BlockInstanceId);
            var block = ServerContext.WorldBlockDatastore.GetBlock(pos);
            var poleConfig = block.BlockMasterElement.BlockParam as ElectricPoleBlockParam;
            
            
            //周辺の機械、発電機を取得
            var (newBlocks, newGenerators) =
                FindMachineAndGeneratorFromPeripheralService.Find(pos, poleConfig);
            //ブロックと発電機を追加
            foreach (var newBlock in newBlocks)
            {
                if (blockElectrics.ContainsKey(newBlock.BlockInstanceId)) continue;
                blockElectrics.Add(newBlock.BlockInstanceId, newBlock);
            }
            
            foreach (var generator in newGenerators)
            {
                if (powerGenerators.ContainsKey(generator.BlockInstanceId)) continue;
                powerGenerators.Add(generator.BlockInstanceId, generator);
            }
            
            
            //周辺の電柱を取得
            var peripheralElectricPoles = FindElectricPoleFromPeripheralService.Find(pos, poleConfig);
            //削除された電柱は除く
            peripheralElectricPoles.Remove(removedElectricPole);
            //自身の電柱は追加する
            electricPoles.Add(electricPole.BlockInstanceId, electricPole);
            //周辺に電柱がない場合は終了
            if (peripheralElectricPoles.Count == 0) return (electricPoles, blockElectrics, powerGenerators);
            
            
            //周辺の電柱を再帰的に取得する
            foreach (var peripheralElectricPole in peripheralElectricPoles)
            {
                //もしもすでに追加されていた電柱ならスキップ
                if (electricPoles.ContainsKey(peripheralElectricPole.BlockInstanceId)) continue;
                //追加されていない電柱なら追加
                (electricPoles, blockElectrics, powerGenerators) =
                    GetElectricPoles(peripheralElectricPole, removedElectricPole, electricPoles, blockElectrics,
                        powerGenerators, container);
            }
            
            return (electricPoles, blockElectrics, powerGenerators);
        }
    }
}