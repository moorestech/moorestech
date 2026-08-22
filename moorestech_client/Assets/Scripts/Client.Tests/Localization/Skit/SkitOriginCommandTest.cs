using System;
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

namespace Client.Tests.Localization.Skit
{
    // スキットJSONの位置がスポーン原点からの相対値として解釈されることを確認する（ADR 0029）
    // Verifies that skit JSON positions are interpreted relative to the spawn origin (ADR 0029)
    public class SkitOriginCommandTest
    {
        private static readonly Vector3 Origin = new(500f, 15.5f, 500f);
        
        [Test]
        public void CameraWarpAddsOriginToPosition()
        {
            var camera = new RecordingSkitCamera();
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
            var camera = new RecordingSkitCamera();
            var context = BuildContext(camera);
            var commands = CommandForgeLoader.LoadCommands(JToken.Parse(
                "{\"commands\":[{\"type\":\"camerawork\",\"id\":1,\"duration\":1,\"easing\":\"Linear\",\"StartPosition\":[1,0,0],\"StartRotation\":[0,0,0],\"EndPosition\":[0,0,2],\"EndRotation\":[0,0,0]}]}"));
            
            SkitCommandExecutor.ExecuteAsync(commands, context).GetAwaiter().GetResult();
            
            Assert.AreEqual(Origin + new Vector3(1f, 0f, 0f), camera.TweenFrom);
            Assert.AreEqual(Origin + new Vector3(0f, 0f, 2f), camera.TweenTo);
        }
        
        private static StoryContext BuildContext(ISkitCamera camera)
        {
            var builder = new ContainerBuilder();
            builder.RegisterInstance(camera);
            builder.RegisterInstance(new SkitOrigin(Origin));
            builder.RegisterInstance<ISkitActionContext>(new NeverSkipActionContext());
            return new StoryContext(builder.Build());
        }
        
        private sealed class RecordingSkitCamera : ISkitCamera
        {
            public Vector3 LastPosition;
            public Vector3 LastRotation;
            public Vector3 TweenFrom;
            public Vector3 TweenTo;
            
            public void TweenCamera(Vector3 fromPos, Vector3 fromRot, Vector3 toPos, Vector3 toRot, float duration, Ease easing)
            {
                TweenFrom = fromPos;
                TweenTo = toPos;
            }
            
            public void SetTransform(Vector3 pos, Vector3 rot)
            {
                LastPosition = pos;
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
    }
}
