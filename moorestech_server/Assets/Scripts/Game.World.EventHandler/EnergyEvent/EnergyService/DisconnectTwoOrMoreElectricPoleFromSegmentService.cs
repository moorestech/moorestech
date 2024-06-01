using System.Collections.Generic;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface;
using Game.Context;
using Game.EnergySystem;

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
            foreach (KeyValuePair<int, IElectricTransformer> onePole in removedSegment.EnergyTransformers) connectedElectricPoles.Add(onePole.Value);
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
                (Dictionary<int, IElectricTransformer> newElectricPoles, Dictionary<int, IElectricConsumer> newBlocks, Dictionary<int, IElectricGenerator> newGenerators) =
                    GetElectricPoles(
                        connectedElectricPoles[0],
                        removedElectricPole,
                        new Dictionary<int, IElectricTransformer>(),
                        new Dictionary<int, IElectricConsumer>(),
                        new Dictionary<int, IElectricGenerator>(), container);


                //新しいセグメントに電柱、ブロック、発電機を追加する
                var newElectricSegment = container.WorldEnergySegmentDatastore.CreateEnergySegment();
                foreach (KeyValuePair<int, IElectricTransformer> newElectric in newElectricPoles)
                {
                    newElectricSegment.AddEnergyTransformer(newElectric.Value);
                    //今までの電柱リストから削除する
                    connectedElectricPoles.Remove(newElectric.Value);
                }

                foreach (KeyValuePair<int, IElectricConsumer> newBlock in newBlocks) newElectricSegment.AddEnergyConsumer(newBlock.Value);
                foreach (KeyValuePair<int, IElectricGenerator> newGenerator in newGenerators) newElectricSegment.AddGenerator(newGenerator.Value);
            }
        }

        //再帰的に電柱を探索する 
        private static (Dictionary<int, IElectricTransformer>, Dictionary<int, IElectricConsumer>,
            Dictionary<int, IElectricGenerator>)
            GetElectricPoles(
                IElectricTransformer electricPole,
                IElectricTransformer removedElectricPole,
                Dictionary<int, IElectricTransformer> electricPoles,
                Dictionary<int, IElectricConsumer> blockElectrics,
                Dictionary<int, IElectricGenerator> powerGenerators,
                EnergyServiceDependencyContainer<TSegment> container)
        {
            var pos = ServerContext.WorldBlockDatastore.GetBlockPosition(electricPole.EntityId);
            var block = ServerContext.WorldBlockDatastore.GetBlock(pos);
            var poleConfig = ServerContext.BlockConfig.GetBlockConfig(block.BlockId).Param as ElectricPoleConfigParam;


            //周辺の機械、発電機を取得
            (List<IElectricConsumer> newBlocks, List<IElectricGenerator> newGenerators) =
                FindMachineAndGeneratorFromPeripheralService.Find(pos, poleConfig);
            //ブロックと発電機を追加
            foreach (var newBlock in newBlocks)
            {
                if (blockElectrics.ContainsKey(newBlock.EntityId)) continue;
                blockElectrics.Add(newBlock.EntityId, newBlock);
            }

            foreach (var generator in newGenerators)
            {
                if (powerGenerators.ContainsKey(generator.EntityId)) continue;
                powerGenerators.Add(generator.EntityId, generator);
            }


            //周辺の電柱を取得
            List<IElectricTransformer> peripheralElectricPoles =
                FindElectricPoleFromPeripheralService.Find(pos, poleConfig);
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
                        powerGenerators, container);
            }

            return (electricPoles, blockElectrics, powerGenerators);
        }
    }
}