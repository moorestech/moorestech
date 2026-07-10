using System;
using System.Linq;
using Client.WebUiHost.Game;
using Core.Item.Interface;
using Microsoft.Extensions.DependencyInjection;
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
    }
}
