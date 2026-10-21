using System.Collections.Generic;
using System.Reflection;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Core.Master;
using Game.Block.Interface.State;
using Game.Fluid;
using MessagePack;
using NUnit.Framework;
using Server.Event.EventReceive;
using UnityEditor;
using UnityEngine;

namespace Client.Tests.GraphicsQuality
{
    public class FluidPipeStateEntryTest : GraphicsQualityTestScope
    {
        [Test]
        public void StateBeforeAndAfterStartUpdatesTransformAndOnlyClonedMaterial()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Asset/Block/Materials/FluidPipe/FluidPipe_Water.shadergraph");
            Assert.IsNotNull(shader);
            var source = Own(new Material(shader));
            source.SetFloat("_WaterLevel", 0.13f);
            var root = Own(new GameObject("water state entry"));
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = source;
            var water = new GameObject("water").transform;
            water.SetParent(root.transform);
            var processor = root.AddComponent<FluidPipeStateChangeProcessor>();
            SetField(processor, "meshRenderer", renderer);
            SetField(processor, "waterLevelTransform", water);

            // Start前の受信値を複製後に一度だけ反映する
            // Apply a pre-Start state once the material has been cloned
            Send(25);
            Assert.AreSame(source, renderer.sharedMaterial);
            typeof(FluidPipeStateChangeProcessor).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(processor, null);
            Own(renderer.sharedMaterial);
            Assert.AreNotSame(source, renderer.sharedMaterial);
            AssertWater(0.25f);
            Send(75);
            AssertWater(0.75f);
            Assert.AreEqual(0.13f, source.GetFloat("_WaterLevel"), 0.00001f);

            #region Internal

            void Send(float amount)
            {
                var detail = new FluidPipeStateDetail(FluidMaster.EmptyFluidId, amount, 100);
                var state = new BlockState(new Dictionary<string, byte[]> { [FluidPipeStateDetail.BlockStateDetailKey] = MessagePackSerializer.Serialize(detail) });
                processor.OnChangeState(new BlockStateMessagePack(state, Vector3Int.zero));
            }

            void AssertWater(float level)
            {
                Assert.AreEqual(level, renderer.sharedMaterial.GetFloat("_WaterLevel"), 0.00001f);
                Assert.AreEqual((level + 0.5f) * 0.5f, water.localPosition.y, 0.00001f);
                Assert.AreEqual(Mathf.Sqrt(1 - Mathf.Pow(level * 2 - 1, 2)) * 0.5f, water.localScale.x, 0.00001f);
            }

            #endregion
        }
    }
}
