using System.Collections.Generic;
using MainGame.Basic;
using MainGame.Network;
using MainGame.Network.Event;
using MainGame.Network.Receive;
using MainGame.Network.Util;
using NUnit.Framework;
using Test.TestModule;
using UnityEngine;

namespace Test.EditModeTest.Network.Receive
{
    public class ReceiveChunkDataProtocolTest
    {
        /// <summary>
        /// ReceiveChunkDataProtocolの単体のテスト
        /// </summary>
        [Test]
        public void ReceiveChunkDataProtocolToRegisterDataStoreTest()
        {
            var chunkUpdateEvent = new NetworkReceivedChunkDataEvent();
            var protocol = new ReceiveChunkDataProtocol(chunkUpdateEvent);

            //イベントをサブスクライブする
            var dataStore = new TestChunkDataStore();
            chunkUpdateEvent.Subscribe(dataStore.OnUpdateChunk,dataStore.OnUpdateBlock);
            
            //チャンクの原点0,19に設定
            var chunkPosition = new Vector2Int(0, 19);
            
            
            
            
            //チャンクデータを受信
            protocol.Analysis(CreateBlockDataList(chunkPosition));
            
            //データの検証
            //そのチャンクのデータを受けているか
            Assert.True(dataStore.Data.ContainsKey(chunkPosition));
            //配列の大きさの検証
            Assert.AreEqual(ChunkConstant.ChunkSize, dataStore.Data[chunkPosition].GetLength(0));
            Assert.AreEqual(ChunkConstant.ChunkSize, dataStore.Data[chunkPosition].GetLength(1));
            
            
            //ブロックデータの検証
            for (int i = 0; i <ChunkConstant.ChunkSize; i++)
            {
                for (int j = 0; j < ChunkConstant.ChunkSize; j++)
                {
                    Assert.True(CheckBlockChunk( i, j,dataStore.Data[chunkPosition][i, j]));
                }
            }
        }

        /// <summary>
        /// 上記のテストをAllReceivePacketAnalysisServiceを介して実行する
        /// </summary>

        [Test]
        public void ChunkDataAnalysisViaAllReceivePacketAnalysisServiceTest()
        {
            var chunkUpdateEvent = new NetworkReceivedChunkDataEvent();
            var packetAnalysis = new AllReceivePacketAnalysisService(chunkUpdateEvent,new PlayerInventoryUpdateEvent());
            var chunkPosition = new Vector2Int(1000, 1240);

            //イベントをサブスクライブする
            var dataStore = new TestChunkDataStore();
            chunkUpdateEvent.Subscribe(dataStore.OnUpdateChunk,dataStore.OnUpdateBlock);
            
            
            
            //データの受信
            packetAnalysis.Analysis(CreateBlockDataList(chunkPosition).ToArray());
            
            
            
            
            //データの検証
            //そのチャンクのデータを受けているか
            Assert.True(dataStore.Data.ContainsKey(chunkPosition));
            //配列の大きさの検証
            Assert.AreEqual(ChunkConstant.ChunkSize, dataStore.Data[chunkPosition].GetLength(0));
            Assert.AreEqual(ChunkConstant.ChunkSize, dataStore.Data[chunkPosition].GetLength(1));
            
            //ブロックデータの検証
            for (int i = 0; i <ChunkConstant.ChunkSize; i++)
            {
                for (int j = 0; j < ChunkConstant.ChunkSize; j++)
                {
                    Assert.True(CheckBlockChunk( i, j,dataStore.Data[chunkPosition][i, j]));
                }
            }
        }


        //0,0 0,19 19,0 19,19 10,14にブロックを設置する
        private List<byte> CreateBlockDataList(Vector2Int chunkPosition)
        {
            
            //チャンクデータのプロトコルを作成
            var bits = new List<bool>();
            //パケットID
            bits.AddRange(ToBitList.Convert((short)1));
            //チャンクの原点
            bits.AddRange(ToBitList.Convert(chunkPosition.x));
            bits.AddRange(ToBitList.Convert(chunkPosition.y));
            //ブロックのデータの作成
            for (int i = 0; i < ChunkConstant.ChunkSize; i++)
            {
                for (int j = 0; j < ChunkConstant.ChunkSize; j++)
                {
                    switch ((i,j))
                    {
                        case (0,0):
                            //0,0の時はbyteの最大値のIDのブロックを設置
                            bits.Add(true);
                            bits.Add(false);
                            bits.Add(false);
                            bits.AddRange(ToBitList.Convert(byte.MaxValue));
                            break;
                        case (0,19):
                            //0,19の時はshortの最大値のIDのブロックを設置
                            bits.Add(true);
                            bits.Add(false);
                            bits.Add(true);
                            bits.AddRange(ToBitList.Convert(short.MaxValue));
                            break;
                        case (19,00):
                            //19,00の時はintの最大値のIDのブロックを設置
                            bits.Add(true);
                            bits.Add(true);
                            bits.Add(false);
                            bits.AddRange(ToBitList.Convert(int.MaxValue));
                            break;
                        case (19,19):
                            //19,19の時はbyteの最大値+1のIDのブロックを設置
                            bits.Add(true);
                            bits.Add(false);
                            bits.Add(true);
                            bits.AddRange(ToBitList.Convert((short)(byte.MaxValue+1)));
                            break;
                        case (10,14):
                            //10,14の時はshortの最大値+1のIDのブロックを設置
                            bits.Add(true);
                            bits.Add(true);
                            bits.Add(false);
                            bits.AddRange(ToBitList.Convert((int)(short.MaxValue+1)));
                            break;
                        default:
                            //それ以外はデフォルトの空気ブロックを入れる
                            bits.Add(false);
                            break;
                    }
                }
            }

            return BitListToByteList.Convert(bits);
        }
        
        //0,0 0,19 19,0 19,19 10,14が設置したブロックに対応しているかチェックする
        private bool CheckBlockChunk(int x, int y,int id)
        {
            
            switch (x,y)
            {
                case (0,0):
                    //0,0の時はbyteの最大値のIDのブロックを設置済み
                    return id == byte.MaxValue;
                case (0,19):
                    //0,19の時はshortの最大値のIDのブロックを設置済み
                    return id == short.MaxValue;
                case (19,00):
                    //19,00の時はintの最大値のIDのブロックを設置済み
                    return id == int.MaxValue;
                case (19,19):
                    //19,19の時はbyteの最大値+1のIDのブロックを設置済み
                    return id == byte.MaxValue + 1;
                case (10,14):
                    //10,14の時はshortの最大値+1のIDのブロックを設置済み
                    return id == short.MaxValue + 1;
                default:
                    //それ以外はデフォルトの空気ブロック
                    return id == BlockConstant.NullBlockId;
            }
        }
    }
}