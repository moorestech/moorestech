using Game.Entity.Interface;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class AssembleEntitySaveJsonTextTest
    {
        [Test]
        public void EntitySaveTest()
        {
            var (_, serviceProvider) =
                new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory, true);
            var assembleSaveJsonText = serviceProvider.GetService<AssembleSaveJsonText>();
            var entitiesDatastore = serviceProvider.GetService<IEntitiesDatastore>();
            var entityFactory = serviceProvider.GetService<IEntityFactory>();
            
            
            //セーブ用のエンティ追加
            var entity1 = entityFactory.CreateEntity(VanillaEntityType.VanillaPlayer, new EntityInstanceId(10));
            var entityPosition = new Vector3(1, 2, 3);
            entity1.SetPosition(entityPosition);
            entitiesDatastore.Add(entity1);
            
            var entity2 = entityFactory.CreateEntity(VanillaEntityType.VanillaPlayer, new EntityInstanceId(30));
            var entityPosition2 = new Vector3(4, 5, 6);
            entity2.SetPosition(entityPosition2);
            entitiesDatastore.Add(entity2);
            
            
            //セーブの実行
            var json = assembleSaveJsonText.AssembleSaveJson();
            Debug.Log(json);
            
            //ロードの実行
            var (_, loadServiceProvider) =
                new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory, true);
            (loadServiceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(json);
            
            
            //ロードしたエンティティを取得
            var loadedEntity1 = entitiesDatastore.Get(new EntityInstanceId(10));
            Assert.AreEqual(entity1.InstanceId, loadedEntity1.InstanceId);
            Assert.AreEqual(entityPosition, loadedEntity1.Position);
            Assert.AreEqual(entity1.EntityType, loadedEntity1.EntityType);
            
            var loadedEntity2 = entitiesDatastore.Get(new EntityInstanceId(30));
            Assert.AreEqual(entity2.InstanceId, loadedEntity2.InstanceId);
            Assert.AreEqual(entityPosition2, loadedEntity2.Position);
            Assert.AreEqual(entity2.EntityType, loadedEntity2.EntityType);
        }
    }
}