using System.Collections.Generic;
using Game.PlayerInventory.Interface;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using Server.Util;
using Test.Module.TestConfig;

namespace Test.CombinedTest.Server.PacketTest
{
    public class SendCommandProtocolTest
    {
        [Test]
        public void GiveCommandTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModuleConfigPath.FolderPath);
            
            //送信するパケットの作成
            var commandPacket = GetGiveCommandPacket(10, 2, 5);
            //送信を実行
            packet.GetPacketResponse(commandPacket);
            
            
            //アイテムが正しく入っているかチェック
            
            //プレイヤーインベントリを取得
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(10);
            
            //何もないインベントリに入れたのでスロット0にアイテムが入っているかチェック
            Assert.AreEqual(2,playerInventory.MainOpenableInventory.GetItem(0).Id);
            Assert.AreEqual(5,playerInventory.MainOpenableInventory.GetItem(0).Count);
            
            
            //別のアイテムIDを入れたので、スロット1にアイテムが入っているかチェック
            packet.GetPacketResponse(GetGiveCommandPacket(10, 3, 7));
            Assert.AreEqual(3,playerInventory.MainOpenableInventory.GetItem(1).Id);
            Assert.AreEqual(7,playerInventory.MainOpenableInventory.GetItem(1).Count);
            
            //アイテムID2を入れたので、スロット0のアイテムが増えているかチェック
            packet.GetPacketResponse(GetGiveCommandPacket(10, 2, 3));
            Assert.AreEqual(2,playerInventory.MainOpenableInventory.GetItem(0).Id);
            Assert.AreEqual(8,playerInventory.MainOpenableInventory.GetItem(0).Count);

        }

        private List<byte> GetGiveCommandPacket(int playerId,int itemId,int count)
        {
            var giveCommand = $"give {playerId} {itemId} {count}"; //give <playerId> <itemId> <count>
            
            var commandPacket = new List<byte>();
            commandPacket.AddRange(ToByteList.Convert((short)11)); //packet id
            commandPacket.AddRange(ToByteList.Convert((short)giveCommand.Length));//command text length
            commandPacket.AddRange(ToByteList.Convert(giveCommand)); //command text

            return commandPacket;
        }
       
        

    }
}