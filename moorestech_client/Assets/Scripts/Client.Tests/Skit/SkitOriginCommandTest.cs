using System;
using System.Collections.Generic;
using Client.Skit.Context;
using Client.Skit.Skit;
using CommandForgeGenerator.Command;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using VContainer;

namespace Client.Tests.Skit
{
    // 位置はスポーン原点からの相対値（ADR 0029）
    // Positions are spawn-relative (ADR 0029)
    public class SkitOriginCommandTest
    {
        private static readonly Vector3 Origin = new(500f, 15.5f, 500f);
        private const float WorldPlacementTolerance = 0.001f;
        
        [Test]
        public void CameraWarpAddsOriginToPosition()
        {
            var camera = new RecordingSkitCamera(new SkitOrigin(Origin));
            var context = BuildContext(camera);
            var commands = CommandForgeLoader.LoadCommands(JToken.Parse(
                "{\"commands\":[{\"type\":\"cameraWarp\",\"id\":1,\"fieldOfView\":60,\"Position\":[1,2,3],\"Rotation\":[10,20,30]}]}"));
            
            SkitCommandExecutor.ExecuteAsync(commands, context).GetAwaiter().GetResult();
            
            Assert.AreEqual(Origin + new Vector3(1f, 2f, 3f), camera.LastPosition);
            Assert.AreEqual(new Vector3(10f, 20f, 30f), camera.LastRotation);
        }
        
        [Test]
        public void CameraworkAddsOriginToStartAndEnd()
        {
            var camera = new RecordingSkitCamera(new SkitOrigin(Origin));
            var context = BuildContext(camera);
            var commands = CommandForgeLoader.LoadCommands(JToken.Parse(
                "{\"commands\":[{\"type\":\"camerawork\",\"id\":1,\"duration\":1,\"easing\":\"Linear\",\"StartPosition\":[1,0,0],\"StartRotation\":[0,0,0],\"EndPosition\":[0,0,2],\"EndRotation\":[0,0,0]}]}"));

            SkitCommandExecutor.ExecuteAsync(commands, context).GetAwaiter().GetResult();

            Assert.AreEqual(Origin + new Vector3(1f, 0f, 0f), camera.TweenFrom);
            Assert.AreEqual(Origin + new Vector3(0f, 0f, 2f), camera.TweenTo);
        }

        [Test]
        public void CameraworkOnSkipAddsOriginToEnd()
        {
            var camera = new RecordingSkitCamera(new SkitOrigin(Origin));
            var builder = new ContainerBuilder();
            builder.RegisterInstance<ISkitCamera>(camera);
            builder.RegisterInstance(new SkitOrigin(Origin));
            builder.RegisterInstance<ISkitActionContext>(new AlwaysSkipActionContext());
            var context = new StoryContext(builder.Build());
            var commands = CommandForgeLoader.LoadCommands(JToken.Parse(
                "{\"commands\":[{\"type\":\"camerawork\",\"id\":1,\"duration\":1,\"easing\":\"Linear\",\"StartPosition\":[1,0,0],\"StartRotation\":[0,0,0],\"EndPosition\":[0,0,2],\"EndRotation\":[0,0,0]}]}"));

            SkitCommandExecutor.ExecuteAsync(commands, context).GetAwaiter().GetResult();

            Assert.AreEqual(Origin + new Vector3(0f, 0f, 2f), camera.LastPosition);
        }

        [Test]
        public void CharacterTransformAddsOriginToPosition()
        {
            var character = new GameObject().AddComponent<SkitCharacter>();
            character.SetSkitOrigin(new SkitOrigin(Origin));
            var container = new CharacterObjectContainer(new Dictionary<string, SkitCharacter> { { "chr_001", character } });
            var builder = new ContainerBuilder();
            builder.RegisterInstance(container);
            builder.RegisterInstance(new SkitOrigin(Origin));
            builder.RegisterInstance<ISkitActionContext>(new NeverSkipActionContext());
            var context = new StoryContext(builder.Build());
            var commands = CommandForgeLoader.LoadCommands(JToken.Parse(
                "{\"commands\":[{\"type\":\"characterTransform\",\"id\":1,\"character\":\"chr_001\",\"Position\":[1,2,3],\"Rotation\":[0,0,0]}]}"));

            SkitCommandExecutor.ExecuteAsync(commands, context).GetAwaiter().GetResult();

            Assert.AreEqual(Origin + new Vector3(1f, 2f, 3f), character.transform.position);
            Assert.AreEqual(new Vector3(0f, 0f, 0f), character.transform.eulerAngles);
        }

        [Test]
        public void ControlSkitBackgroundAddsOriginToPosition()
        {
            var environmentManager = new RecordingEnvironmentManager(new SkitOrigin(Origin));
            var builder = new ContainerBuilder();
            builder.RegisterInstance<ISkitEnvironmentManager>(environmentManager);
            builder.RegisterInstance(new SkitOrigin(Origin));
            builder.RegisterInstance<ISkitActionContext>(new NeverSkipActionContext());
            var context = new StoryContext(builder.Build());
            var commands = CommandForgeLoader.LoadCommands(JToken.Parse(
                "{\"commands\":[{\"type\":\"controlSkitBackground\",\"id\":1,\"action\":\"Add\",\"skitEnvironmentAddressablePath\":\"dummy\",\"position\":[1,2,3],\"rotation\":[0,0,0]}]}"));

            SkitCommandExecutor.ExecuteAsync(commands, context).GetAwaiter().GetResult();

            Assert.AreEqual(Origin + new Vector3(1f, 2f, 3f), environmentManager.LastPosition);
        }

        // 執筆ツールが使う逆変換。符号を反転させたら落ちる
        // The inverse used by authoring tools; flipping the sign must fail here
        [Test]
        public void ToRelativeSubtractsOrigin()
        {
            var origin = new SkitOrigin(Origin);

            Assert.AreEqual(new Vector3(1f, 2f, 3f), origin.ToRelative(Origin + new Vector3(1f, 2f, 3f)));
        }

        [Test]
        public void ToWorldAndToRelativeRoundTripToTheSameValue()
        {
            var origin = new SkitOrigin(Origin);
            var relative = new Vector3(-12.5f, 4f, 0.25f);

            Assert.AreEqual(relative, origin.ToRelative(origin.ToWorld(relative)));
            Assert.AreEqual(Origin, origin.ToWorld(origin.ToRelative(Origin)));
        }

        // 背景は親のTransformへ追従せずワールド固定になる（親を非identityにしても位置が変わらない）
        // Backgrounds ignore the parent transform and stay fixed in world space, even under a non-identity parent
        [Test]
        public void BackgroundIsPlacedInWorldSpaceUnderNonIdentityParent()
        {
            var parent = new GameObject("SkitRoot").transform;
            parent.position = new Vector3(100f, 7f, -40f);
            parent.rotation = Quaternion.Euler(0f, 90f, 0f);
            var instance = new GameObject("Environment").transform;
            instance.SetParent(parent);

            var worldPosition = Origin + new Vector3(1f, 2f, 3f);
            SkitEnvironmentManager.PlaceInWorld(instance, worldPosition, new Vector3(0f, 30f, 0f));

            // 回転した親のローカル座標を経由するぶん誤差が乗るので許容差で見る
            // Going through a rotated parent's local space introduces error, so compare with a tolerance
            Assert.AreEqual(worldPosition.x, instance.position.x, WorldPlacementTolerance);
            Assert.AreEqual(worldPosition.y, instance.position.y, WorldPlacementTolerance);
            Assert.AreEqual(worldPosition.z, instance.position.z, WorldPlacementTolerance);
            Assert.AreEqual(30f, instance.eulerAngles.y, WorldPlacementTolerance);
        }

        private static StoryContext BuildContext(ISkitCamera camera)
        {
            var builder = new ContainerBuilder();
            builder.RegisterInstance(camera);
            builder.RegisterInstance(new SkitOrigin(Origin));
            builder.RegisterInstance<ISkitActionContext>(new NeverSkipActionContext());
            return new StoryContext(builder.Build());
        }
        
        // sink側で加算する本番と同じ順序を再現するため、記録用も原点を持って変換する
        // Reproduce production's sink-side addition by having the recorder hold the origin and convert
        private sealed class RecordingSkitCamera : ISkitCamera
        {
            private readonly SkitOrigin _skitOrigin;
            
            public Vector3 LastPosition;
            public Vector3 LastRotation;
            public Vector3 TweenFrom;
            public Vector3 TweenTo;
            
            public RecordingSkitCamera(SkitOrigin skitOrigin)
            {
                _skitOrigin = skitOrigin;
            }
            
            public void TweenCamera(SkitRelativePosition fromPos, Vector3 fromRot, SkitRelativePosition toPos, Vector3 toRot, float duration, Ease easing)
            {
                TweenFrom = fromPos.ToWorld(_skitOrigin);
                TweenTo = toPos.ToWorld(_skitOrigin);
            }
            
            public void SetTransform(SkitRelativePosition pos, Vector3 rot)
            {
                LastPosition = pos.ToWorld(_skitOrigin);
                LastRotation = rot;
            }
            
            public void SetFov(float fov) { }
        }
        
        private sealed class NeverSkipActionContext : ISkitActionContext
        {
            public bool IsAuto => false;
            public bool IsSkip => false;
            public IObservable<Unit> OnSkip => Observable.Never<Unit>();
        }

        private sealed class AlwaysSkipActionContext : ISkitActionContext
        {
            public bool IsAuto => false;
            public bool IsSkip => true;
            public IObservable<Unit> OnSkip => Observable.Never<Unit>();
        }

        private sealed class RecordingEnvironmentManager : ISkitEnvironmentManager
        {
            private readonly SkitOrigin _skitOrigin;
            
            public Vector3 LastPosition;
            public Vector3 LastRotation;
            
            public RecordingEnvironmentManager(SkitOrigin skitOrigin)
            {
                _skitOrigin = skitOrigin;
            }

            public UniTask AddEnvironmentAsync(string addressablePath, SkitRelativePosition position, Vector3 rotation)
            {
                LastPosition = position.ToWorld(_skitOrigin);
                LastRotation = rotation;
                return UniTask.CompletedTask;
            }

            public void RemoveEnvironment(string addressablePath) { }
        }
    }
}
