using System;
using System.Linq;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game;
using Core.Item.Interface;
using Core.Master;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.WebUi
{
    public class ItemMasterEndpointTest
    {
        private static readonly Guid TestItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        [Test]
        public void BuildResponseReflectsStackLevelChangeBetweenRequests()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var stackLevelLookup = serviceProvider.GetRequiredService<IItemStackLevelLookup>();
            var stackLevelUnlocker = serviceProvider.GetRequiredService<IItemStackLevelUnlocker>();

            // レベル変更を連続取得で検証
            // Verify a level change across reads
            var firstResponse = ItemMasterEndpoint.BuildResponse(stackLevelLookup);
            stackLevelUnlocker.UnlockStackLevel(TestItemGuid, 2);
            var secondResponse = ItemMasterEndpoint.BuildResponse(stackLevelLookup);

            Assert.AreEqual(100, firstResponse.Items.Single(item => item.ItemId == 1).MaxStack);
            Assert.AreEqual(200, secondResponse.Items.Single(item => item.ItemId == 1).MaxStack);
        }

        [Test]
        public void BuildResponseSerializesItemGuidWithoutSourceName()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var stackLevelLookup = serviceProvider.GetRequiredService<IItemStackLevelLookup>();

            // 実シリアライザを通した公開契約から原文名を排除する
            // Exclude the source name from the public contract produced by the real serializer
            var response = ItemMasterEndpoint.BuildResponse(stackLevelLookup);
            var wire = JToken.Parse(WebUiJson.Serialize(response));
            var item = wire["items"]!.Single(entry => (int)entry["itemId"]! == 1);

            CollectionAssert.AreEquivalent(
                new[] { "itemId", "itemGuid", "maxStack" },
                ((JObject)item).Properties().Select(property => property.Name));
            Assert.AreEqual("00000000-0000-0000-1234-000000000001", (string)item["itemGuid"]!);
            Assert.AreEqual(100, (int)item["maxStack"]!);

            // 英大文字を含む入力も小文字D形式へ正準化する
            // Canonicalize source values containing uppercase hex to lowercase D format
            var uppercaseSourceGuid = Guid.Parse("7868F6D6-6874-4DAD-96A5-EA6BD35F57CF");
            var uppercaseSourceItemId = MasterHolder.ItemMaster.GetItemId(uppercaseSourceGuid).AsPrimitive();
            var canonicalizedItem = wire["items"]!.Single(entry => (int)entry["itemId"]! == uppercaseSourceItemId);
            Assert.AreEqual("7868f6d6-6874-4dad-96a5-ea6bd35f57cf", (string)canonicalizedItem["itemGuid"]!);
        }
    }
}
