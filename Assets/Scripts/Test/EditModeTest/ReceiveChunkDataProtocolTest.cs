using System.Collections.Generic;
using MainGame.Constant;
using MainGame.GameLogic.Interface;
using MainGame.Network;
using MainGame.Network.Receive;
using MainGame.Network.Util;
using NUnit.Framework;
using Test.TestModule;
using UnityEngine;

namespace EditModeTest
{
    public class ReceiveChunkDataProtocolTest
    {
        [Test]
        public void ReceiveChunkDataProtocolToRegisterDataStoreTest()
        {
            var dataStore = new TestDataStore();
            var protocol = new ReceiveChunkDataProtocol(dataStore);
            //チャンクの原点0,20に設定
            var chunkPosition = new Vector2Int(0, 20);
            
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

        //上記のテストをAllReceivePacketAnalysisServiceを介して実行する
        
        
        [Test]
        public void ReceivePacketAnalysisViaAnalysisTest()
        {
            var dataStore = new TestDataStore();
            var packetAnalysis = new AllReceivePacketAnalysisService(dataStore);
            var chunkPosition = new Vector2Int(1000, 1240);
            
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


        //0,0 0,20 20,0 20,20 10,14にブロックを設置する
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
                        case (0,20):
                            //0,20の時はshortの最大値のIDのブロックを設置
                            bits.Add(true);
                            bits.Add(false);
                            bits.Add(true);
                            bits.AddRange(ToBitList.Convert(short.MaxValue));
                            break;
                        case (20,00):
                            //20,00の時はintの最大値のIDのブロックを設置
                            bits.Add(true);
                            bits.Add(true);
                            bits.Add(false);
                            bits.AddRange(ToBitList.Convert(int.MaxValue));
                            break;
                        case (20,20):
                            //20,20の時はbyteの最大値+1のIDのブロックを設置
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
        
        //0,0 0,20 20,0 20,20 10,14が設置したブロックに対応しているかチェックする
        private bool CheckBlockChunk(int x, int y,int id)
        {
            
            switch (x,y)
            {
                case (0,0):
                    //0,0の時はbyteの最大値のIDのブロックを設置済み
                    return id == byte.MaxValue;
                case (0,20):
                    //0,20の時はshortの最大値のIDのブロックを設置済み
                    return id == short.MaxValue;
                case (20,00):
                    //20,00の時はintの最大値のIDのブロックを設置済み
                    return id == int.MaxValue;
                case (20,20):
                    //20,20の時はbyteの最大値+1のIDのブロックを設置済み
                    return id == byte.MaxValue + 1;
                case (10,14):
                    //10,14の時はshortの最大値+1のIDのブロックを設置済み
                    return id == short.MaxValue + 1;
                default:
                    //それ以外はデフォルトの空気ブロック
                    return id == BlockConstant.NullBlockId;
                    break;
            }
        }
    }
}