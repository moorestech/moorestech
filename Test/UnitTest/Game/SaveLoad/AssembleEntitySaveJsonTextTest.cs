using System;
using Game.Entity.Interface;
using Game.Save.Interface;
using Game.Save.Json;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;

using Test.Module.TestMod;

namespace Test.UnitTest.Game.SaveLoad
{
    public class AssembleEntitySaveJsonTextTest
    {
        [Test]
        public void EntitySaveTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var assembleSaveJsonText = serviceProvider.GetService<AssembleSaveJsonText>();
            var entitiesDatastore = serviceProvider.GetService<IEntitiesDatastore>();
            var entityFactory = serviceProvider.GetService<IEntityFactory>();
            
            
            //セーブ用のエンティ追加
            var entity1 = entityFactory.CreateEntity(EntityType.VanillaPlayer,10);
            var entityPosition = new ServerVector3(1,2,3);
            entity1.SetPosition(entityPosition);
            entitiesDatastore.Add(entity1);
            
            var entity2 = entityFactory.CreateEntity(EntityType.VanillaPlayer,30);
            var entityPosition2 = new ServerVector3(4,5,6);
            entity2.SetPosition(entityPosition2);
            entitiesDatastore.Add(entity2);
            
            
            
            
            
            //セーブの実行
            var json = assembleSaveJsonText.AssembleSaveJson();
            Console.WriteLine(json);

            //ロードの実行
            var (_, loadServiceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            (loadServiceProvider.GetService<ILoadRepository>() as LoadJsonFile).Load(json);
            
            
            //ロードしたエンティティを取得
            var loadedEntity1 = entitiesDatastore.Get(10);
            Assert.AreEqual(entity1.InstanceId, loadedEntity1.InstanceId);
            Assert.AreEqual(entityPosition, loadedEntity1.Position);
            Assert.AreEqual(entity1.EntityType, loadedEntity1.EntityType);
            
            var loadedEntity2 = entitiesDatastore.Get(30);
            Assert.AreEqual(entity2.InstanceId, loadedEntity2.InstanceId);
            Assert.AreEqual(entityPosition2, loadedEntity2.Position);
            Assert.AreEqual(entity2.EntityType, loadedEntity2.EntityType);
            
        }
        
    }
}