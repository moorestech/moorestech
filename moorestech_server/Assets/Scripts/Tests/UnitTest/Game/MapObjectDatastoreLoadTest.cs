using System.Collections.Generic;
using System.Text.RegularExpressions;
using Game.Context;
using Game.Map.Interface.Json;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game
{
    public class MapObjectDatastoreLoadTest
    {
        [Test]
        public void マップに存在しないinstanceIdのセーブは警告してスキップし後続をロードするTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var existingMapObject = ServerContext.MapObjectDatastore.MapObjects[0];

            // 欠損後も破壊状態を復元
            // Restore destruction after missing entry
            var savedMapObjects = new List<MapObjectJsonObject>
            {
#pragma warning disable CS0618
                new() { instanceId = 999999, isDestroyed = true, hp = 0 },
                new() { instanceId = existingMapObject.InstanceId, isDestroyed = true, hp = existingMapObject.CurrentHp },
#pragma warning restore CS0618
            };
            LogAssert.Expect(LogType.Warning, new Regex(@"^セーブ内のinstanceId:999999 のmapObjectがマップに存在しないためスキップします。$"));

            Assert.DoesNotThrow(() => ServerContext.MapObjectDatastore.LoadMapObject(savedMapObjects));
            Assert.That(existingMapObject.IsDestroyed, Is.True);
        }
    }
}
