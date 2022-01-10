using System.Collections.Generic;
using System.Linq;
using Core.Block.Blocks;
using Core.Block.Config;
using Core.Block.Config.LoadConfig.Param;
using Core.Electric;
using Game.World.EventHandler.Service;
using Game.World.Interface.DataStore;
using Game.World.Interface.Event;

namespace Game.World.EventHandler
{
    public class DisconnectElectricPoleToFromElectricSegment
    {
        private readonly IWorldBlockComponentDatastore<IElectricPole> _electricPoleDatastore;
        private readonly IWorldBlockComponentDatastore<IBlockElectric> _electricDatastore;
        private readonly IWorldBlockComponentDatastore<IPowerGenerator> _powerGeneratorDatastore;
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        private readonly IBlockConfig _blockConfig;
        private readonly IWorldElectricSegmentDatastore _worldElectricSegmentDatastore;

        public DisconnectElectricPoleToFromElectricSegment(
            IBlockRemoveEvent blockRemoveEvent,
            IWorldBlockComponentDatastore<IElectricPole> electricPoleDatastore, 
            IBlockConfig blockConfig,
            IWorldElectricSegmentDatastore worldElectricSegmentDatastore)
        {
            _electricPoleDatastore = electricPoleDatastore;
            _blockConfig = blockConfig;
            _worldElectricSegmentDatastore = worldElectricSegmentDatastore;
            blockRemoveEvent.Subscribe(OnBlockRemove);
        }
        
        private void OnBlockRemove(BlockRemoveEventProperties blockRemoveEvent)
        {
            var x = blockRemoveEvent.Coordinate.X;
            var y = blockRemoveEvent.Coordinate.Y;
            
            //電柱かどうか判定
            //電柱だったら接続範囲内周りにある電柱を取得する
            if (!_electricPoleDatastore.ExistsComponentBlock(x, y))return;
            var poleConfig =
                _blockConfig.GetBlockConfig(blockRemoveEvent.Block.GetBlockId()).Param as ElectricPoleConfigParam;
            
            
            //接続範囲内の電柱を取得
            var electricPoles = 
                new FindElectricPoleFromPeripheralService().Find(
                    x,y,
                    poleConfig,
                    _electricPoleDatastore);
            var pole = _electricPoleDatastore.GetBlock(x, y);
            
            //電柱のセグメントを取得
            var segment = _worldElectricSegmentDatastore.GetElectricSegment(pole);
            
            //周りに電柱がないとき
            if (electricPoles.Count == 0)
            {
                //セグメントを削除する
                _worldElectricSegmentDatastore.RemoveElectricSegment(segment);
                return;
            }
            
            //周りの電柱が1つの時
            if (electricPoles.Count == 1)
            {
                //セグメントから電柱の接続状態を解除
                segment.RemoveElectricPole(pole);
                
                //周辺の機械、発電機を取得
                var (blocks, generators) = 
                    new FindMachineAndGeneratorFromPeripheralService().
                        Find(x,y,poleConfig,_electricDatastore,_powerGeneratorDatastore);
                
                //周辺の機械、発電機を接続状態から解除する
                blocks.ForEach(segment.RemoveBlockElectric);
                generators.ForEach(segment.RemoveGenerator);
                
                
                
                //繋がっていた1つの電柱の周辺の機械と発電機を探索
                var (connectedX,connectedY) = _worldBlockDatastore.GetBlockPosition(electricPoles[0].GetIntId());
                var connectedPoleConfig =
                    _blockConfig.GetBlockConfig(((IBlock)electricPoles[0]).GetBlockId()).Param as ElectricPoleConfigParam;
                var (connectedBlocks, connectedGenerators) = 
                    new FindMachineAndGeneratorFromPeripheralService().
                        Find(connectedX,connectedY,
                            connectedPoleConfig,_electricDatastore,_powerGeneratorDatastore);
                
                //セグメントに追加する
                connectedBlocks.ForEach(segment.AddBlockElectric);
                connectedGenerators.ForEach(segment.AddGenerator);
                
                return;
            }

            //周りの電柱が2つ以上の時
            if (2 <= electricPoles.Count)
            {
                //自身が所属していたセグメントの電柱のリストを取る
                var connectedElectricPoles = new List<IElectricPole>();
                foreach (var onePole in segment.GetElectricPoles())
                {
                    connectedElectricPoles.Add(onePole.Value);
                }

                //元のセグメントを消す
                _worldElectricSegmentDatastore.RemoveElectricSegment(segment);
                
                //それらの電柱を全て探索し、電力セグメントを再構成する
                
                //1個ずつ電柱を取り出し、電力セグメントに追加する
                //電柱のリストが空になるまで繰り返す
                while (connectedElectricPoles.Count != 0)
                {
                    var (newElectricPoles,newBlocks, newGenerators) = 
                        GetElectricPoles(
                            connectedElectricPoles[0],
                            new Dictionary<int, IElectricPole>(),
                            new Dictionary<int, IBlockElectric>(),
                            new Dictionary<int, IPowerGenerator>());
                    
                    
                    //新しいセグメントに電柱、ブロック、発電機を追加する
                    var newElectricSegment = _worldElectricSegmentDatastore.CreateElectricSegment();
                    foreach (var newElectric in newElectricPoles)
                    {
                        newElectricSegment.AddElectricPole(newElectric.Value);
                        //今までの電柱リストから削除する
                        connectedElectricPoles.Remove(newElectric.Value);
                    }
                    foreach (var newBlock in newBlocks)
                    {
                        newElectricSegment.AddBlockElectric(newBlock.Value);
                    }
                    foreach (var newGenerator in newGenerators)
                    {
                        newElectricSegment.AddGenerator(newGenerator.Value);
                    }
                }
            }
            
            
        }
        //TODO これのテストにループしている電柱、複数の電柱を追加する
        
        //再帰的に電柱を探索する 
        //TODO ブロック、発電機も探索する
        private (Dictionary<int,IElectricPole>,Dictionary<int,IBlockElectric>,Dictionary<int,IPowerGenerator>) GetElectricPoles(
            IElectricPole electricPole,
            Dictionary<int,IElectricPole> electricPoles,
            Dictionary<int,IBlockElectric> blockElectrics,
            Dictionary<int,IPowerGenerator> powerGenerators) 
        {
            var (x,y) = _worldBlockDatastore.GetBlockPosition(electricPole.GetIntId());
            var poleConfig =
                _blockConfig.GetBlockConfig(((IBlock)electricPole).GetBlockId()).Param as ElectricPoleConfigParam;
            
            
            //周辺の機械、発電機を取得
            var (newBlocks, newGenerators) = 
                new FindMachineAndGeneratorFromPeripheralService().
                    Find(x,y,poleConfig,_electricDatastore,_powerGeneratorDatastore);
            
            //ブロックと発電機を追加
            foreach (var block in newBlocks)
            {
                if (blockElectrics.ContainsKey(block.GetIntId())) continue;
                blockElectrics.Add(block.GetIntId(),block);
            }
            foreach (var generator in newGenerators)
            {
                if (powerGenerators.ContainsKey(generator.GetIntId())) continue;
                powerGenerators.Add(generator.GetIntId(),generator);
            }
            
            //周辺の電柱を取得
            var newElectricPoles = new FindElectricPoleFromPeripheralService().Find(x,y,poleConfig,_electricPoleDatastore);
            //周辺に電柱がない場合は終了
            if (newElectricPoles.Count == 0)return (electricPoles,blockElectrics,powerGenerators);
            
            
            //周辺の電柱を再帰的に取得する
            foreach (var newElectricPole in newElectricPoles)
            {
                //もしもすでに追加されていた電柱ならスキップ
                if (electricPoles.ContainsKey(newElectricPole.GetIntId()))continue;
                //追加されていない電柱なら追加
                (electricPoles,blockElectrics,powerGenerators) = 
                    GetElectricPoles(newElectricPole,electricPoles,blockElectrics,powerGenerators);
                
            }
            
            return (electricPoles,blockElectrics,powerGenerators);
        }




    }
}