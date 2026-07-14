using System;
using System.Linq;
using System.Reflection;
using Client.Game.InGame.Control;
using Client.Game.InGame.Control.ViewMode;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.ViewMode
{
    public class ThirdPersonCameraDistanceTest
    {
        [Test]
        public void ZoomDuringViewTransitionDoesNotChangeSelectedDistance()
        {
            var distance = new ThirdPersonCameraDistance(5f);
            distance.SetTransitioning(true);

            var changed = distance.TryAddZoom(0.75f);

            Assert.IsFalse(changed);
            Assert.AreEqual(5f, distance.GetDistance());
        }

        [Test]
        public void ZoomOutsideViewTransitionClampsAndChangesSelectedDistance()
        {
            var distance = new ThirdPersonCameraDistance(9.8f);

            var changed = distance.TryAddZoom(0.75f);

            Assert.IsTrue(changed);
            Assert.AreEqual(ThirdPersonCameraDistance.MaximumDistance, distance.GetDistance());
        }

        [Test]
        public void InitialDistanceIsClamped()
        {
            var distance = new ThirdPersonCameraDistance(ThirdPersonCameraDistance.MaximumDistance + 1f);

            Assert.AreEqual(ThirdPersonCameraDistance.MaximumDistance, distance.GetDistance());
        }

        [Test]
        public void CameraControllerIgnoresZoomDuringThirdPersonReturnTween()
        {
            var gameObject = new GameObject("InGameCameraController");
            gameObject.SetActive(false);
            var virtualCameraType = FindType("Cinemachine.CinemachineVirtualCamera");
            var framingType = FindType("Cinemachine.CinemachineFramingTransposer");
            var virtualCamera = gameObject.AddComponent(virtualCameraType);
            var addFramingMethod = virtualCameraType.GetMethods().Single(method => method.Name == "AddCinemachineComponent" && method.IsGenericMethodDefinition);
            var framing = addFramingMethod.MakeGenericMethod(framingType).Invoke(virtualCamera, null);
            framingType.GetField("m_CameraDistance").SetValue(framing, 5f);
            var controller = gameObject.AddComponent<InGameCameraController>();
            SetField(controller, "virtualCamera", virtualCamera);
            InvokeMethod(controller, "Awake");

            framingType.GetField("m_CameraDistance").SetValue(framing, 7f);
            InvokeMethod(controller, "Update");
            var distanceWithoutZoomInput = (float)framingType.GetField("m_CameraDistance").GetValue(framing);
            framingType.GetField("m_CameraDistance").SetValue(framing, 5f);

            controller.SetFirstPersonMode(true);
            controller.SetFirstPersonMode(false);
            controller.AddThirdPersonZoom(0.75f);
            CompleteTween(GetField(controller, "_distanceTweener"));
            controller.AddThirdPersonZoom(0.75f);
            var distanceState = (ThirdPersonCameraDistance)GetField(controller, "_thirdPersonCameraDistance");
            var actualDistance = distanceState.GetDistance();

            KillTween(GetField(controller, "_offsetTweener"));
            KillTween(GetField(controller, "_distanceTweener"));
            UnityEngine.Object.DestroyImmediate(gameObject);
            Assert.AreEqual(7f, distanceWithoutZoomInput);
            Assert.AreEqual(5.75f, actualDistance);

            #region Internal

            static void SetField(object target, string fieldName, object value)
            {
                var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                field.SetValue(target, value);
            }

            static object GetField(object target, string fieldName)
            {
                var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                return field.GetValue(target);
            }

            static void InvokeMethod(object target, string methodName)
            {
                var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
                method.Invoke(target, null);
            }

            static Type FindType(string fullName)
            {
                return AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(fullName)).First(type => type != null);
            }

            static void KillTween(object tween)
            {
                var extensionsType = FindType("DG.Tweening.TweenExtensions");
                var tweenType = FindType("DG.Tweening.Tween");
                var killMethod = extensionsType.GetMethods().Single(method => method.Name == "Kill" && method.GetParameters().Length == 2 && method.GetParameters()[0].ParameterType == tweenType);
                killMethod.Invoke(null, new[] { tween, (object)false });
            }

            static void CompleteTween(object tween)
            {
                var extensionsType = FindType("DG.Tweening.TweenExtensions");
                var tweenType = FindType("DG.Tweening.Tween");
                var completeMethod = extensionsType.GetMethods().Single(method => method.Name == "Complete" && method.GetParameters().Length == 2 && method.GetParameters()[0].ParameterType == tweenType);
                completeMethod.Invoke(null, new[] { tween, (object)true });
            }

            #endregion
        }
    }
}
