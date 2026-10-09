using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using Client.Game.InGame.Block.IconCapture;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Block
{
    public class BlockIconImagePhotographerSubjectCaptureTest
    {
        private const string TestObjectPrefix = BlockIconCaptureTestEnvironment.TestObjectPrefix;

        [TearDown]
        public void TearDown()
        {
            BlockIconCaptureTestEnvironment.DestroyTestObjects();
        }

        [UnityTest]
        public IEnumerator TryTakeSubjectIconImage_Rendererが無い被写体はログを残してnullを返し後始末も通す()
        {
            var (photographer, captureLight) = CreatePhotographer();
            var subject = new GameObject($"{TestObjectPrefix}EmptySubject");

            // 例外を投げず、被写体破棄とライト復元まで済ませて null を返す
            // Return null without throwing, after destroying the subject and restoring the light
            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(BlockIconImagePhotographer.CaptureLogPrefix) + ".*no renderers"));
            var captureTask = photographer.TryTakeSubjectIconImage(subject, "empty-subject");
            yield return WaitForCompletion(captureTask);

            Assert.That(captureTask.GetAwaiter().GetResult(), Is.Null);
            Assert.That(subject == null, Is.True);
            Assert.That(captureLight.enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator TryTakeSubjectIconImage_描画できる被写体は撮影して元被写体を破棄する()
        {
            var (photographer, captureLight) = CreatePhotographer();
            var subject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            subject.name = $"{TestObjectPrefix}CubeSubject";

            var captureTask = photographer.TryTakeSubjectIconImage(subject, "cube-subject");
            yield return WaitForCompletion(captureTask);

            var texture = captureTask.GetAwaiter().GetResult();
            Assert.That(texture, Is.Not.Null);
            Assert.That(subject == null, Is.True);
            Assert.That(captureLight.enabled, Is.False);
            Object.DestroyImmediate(texture);
        }

        private static (BlockIconImagePhotographer photographer, Light captureLight) CreatePhotographer()
        {
            // 撮影中だけ点く無効ライトを子に持つ撮影器を、実Prefabと同じ参照注入で組む
            // Build a photographer with a disabled child light, injecting the camera like the real prefab
            var photographerObject = new GameObject($"{TestObjectPrefix}SubjectPhotographer");
            var photographer = photographerObject.AddComponent<BlockIconImagePhotographer>();
            var lightObject = new GameObject($"{TestObjectPrefix}SubjectLight");
            lightObject.transform.SetParent(photographerObject.transform, false);
            var captureLight = lightObject.AddComponent<Light>();
            captureLight.enabled = false;
            var cameraPrefab = new GameObject($"{TestObjectPrefix}SubjectCamera").AddComponent<Camera>();
            var cameraField = typeof(BlockIconImagePhotographer).GetField("cameraPrefab", BindingFlags.Instance | BindingFlags.NonPublic);
            cameraField.SetValue(photographer, cameraPrefab);
            return (photographer, captureLight);
        }

        private static IEnumerator WaitForCompletion(UniTask<Texture2D> captureTask)
        {
            for (var frame = 0; frame < BlockIconCaptureTestEnvironment.CaptureCompletionFrameLimit && captureTask.Status == UniTaskStatus.Pending; frame++)
                yield return null;
            Assert.That(captureTask.Status, Is.Not.EqualTo(UniTaskStatus.Pending), "Subject capture did not complete in time.");
        }
    }
}
