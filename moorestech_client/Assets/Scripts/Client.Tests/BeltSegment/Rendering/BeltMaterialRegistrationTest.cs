using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Client.Game.InGame.BeltSegment.Rendering;
using Client.Game.InGame.Context;
using Client.Mod.Texture;
using Client.Tests.BeltSegment.Network;
using Core.Master;
using Game.BeltSegment;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.BeltSegment.Rendering
{
    public sealed class BeltMaterialRegistrationTest
    {
        [Test]
        public void SnapshotAndAcceptedInsertionBindOnlyUsedKindsAcrossRebuild()
        {
            BeltNetworkFixture.LoadMaster();
            var ids=MasterHolder.ItemMaster.GetItemAllIds().Take(3).ToArray();
            var views=new Dictionary<ItemId,ItemViewData>();
            views.Add(ids[0],new ItemViewData(Texture2D.whiteTexture,MasterHolder.ItemMaster.GetItemMaster(ids[0])));
            // 未使用の欠損kindを定義し、再構築だけでは診断しないことを確認する。
            // An unused missing kind must not be diagnosed merely by rebuilding topology.
            views.Add(ids[2],new ItemViewData((Texture2D)null,MasterHolder.ItemMaster.GetItemMaster(ids[2])));
            var images=(ItemImageContainer)Activator.CreateInstance(typeof(ItemImageContainer),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{views},null);
            var imageProperty=typeof(ClientContext).GetProperty(nameof(ClientContext.ItemImageContainer));
            var original=imageProperty.GetValue(null); imageProperty.SetValue(null,images);
            var world=BeltNetworkFixture.World();
            var renderer=new BeltItemRenderer(world);
            int missingWarnings=0;
            Application.logMessageReceived+=CountMissing;
            try
            {
                var item=BeltDrawPositionTest.Item(ids[0].AsPrimitive(),BeltDirection.Back,0);
                var route=new BeltRoute(new[]{BeltDrawPositionTest.Cell(0,0,0,0),BeltDrawPositionTest.Cell(0,1,0,0)},BeltDrawPositionTest.Entries(BeltDrawPositionTest.Cell(0,-1,0,0)));
                var initial=new BeltWorldSnapshot(new(1,1),1,new(new[]{BeltReplaySegmentState.Normal(2,16,new[]{item})},Array.Empty<BeltReplayLink>(),new[]{new BeltReplayInput(0,BeltDirection.Back)},Array.Empty<BeltReplayOutput>()),new[]{route});
                world.ReceiveSnapshot(initial); renderer.Start();
                Assert.AreEqual(1,Materials().Materials.Count(m=>m!=null));
                AssertBinding(ids[0]); AssertNoMaterial(ids[1]); AssertNoMaterial(ids[2]);
                var inserted=new BeltItem{Guid=Guid.NewGuid(),ItemId=ids[1].AsPrimitive(),AcceptedInput=BeltDirection.Back};
                var tick=new BeltReplayTick(Array.Empty<BeltReplaySpeedChange>(),new[]{0},Array.Empty<int>(),new[]{new BeltReplayInsertion(0,16,inserted)});
                LogAssert.Expect(LogType.Warning,$"[BeltItemMaterials] Missing image for item kind {inserted.ItemId}; using magenta cube.");
                world.ReceiveFrame(BeltNetworkFixture.Frame(initial,tick));
                Assert.AreEqual(2,Materials().Materials.Count(m=>m!=null)); AssertBinding(ids[1]);
                var oldMaterials=Materials().Materials.Where(m=>m!=null).ToArray();
                var oldBuffer=Buffers().Positions;
                world.ReceiveSnapshot(new(world.Position,2,world.CaptureCpuState(),world.Routes));
                Assert.IsFalse(oldBuffer.IsValid()); Assert.IsTrue(oldMaterials.All(m=>m==null));
                AssertBinding(ids[0]); AssertBinding(ids[1]); AssertNoMaterial(ids[2]);
                // 同じ欠損kindの再構築で二度目のwarningを出さない。
                // Rebuilding the same missing kind does not repeat its warning.
                Assert.AreEqual(1,missingWarnings);
            }
            finally
            {
                Application.logMessageReceived-=CountMissing;
                renderer.Dispose(); world.Simulation?.Dispose(); imageProperty.SetValue(null,original);
            }
            #region Internal
            void CountMissing(string message,string stack,LogType type)
            {
                if(message.StartsWith("[BeltItemMaterials] Missing image")) missingWarnings++;
            }
            BeltItemMaterials Materials() => (BeltItemMaterials)typeof(BeltItemRenderer).GetField("materials",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(renderer);
            BeltDrawBuffers Buffers() => (BeltDrawBuffers)typeof(BeltItemRenderer).GetField("buffers",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(renderer);
            void AssertNoMaterial(ItemId id) => Assert.IsNull(Materials().Materials[Array.BinarySearch(Materials().Kinds,id.AsPrimitive())]);
            void AssertBinding(ItemId id)
            {
                int index=Array.BinarySearch(Materials().Kinds,id.AsPrimitive());
                var material=Materials().Materials[index];
                Assert.AreEqual(index,material.GetInt("_KindIndex"));
                Assert.AreEqual(Buffers().Positions.bufferHandle,material.GetBuffer("_Positions"));
                Assert.AreEqual(Buffers().Offsets.bufferHandle,material.GetBuffer("_Offsets"));
            }
            #endregion
        }
    }
}
