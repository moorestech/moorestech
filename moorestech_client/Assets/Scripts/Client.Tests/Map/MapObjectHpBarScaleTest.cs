using System;
using Client.Game.InGame.Map.MapObject;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Tests.Map
{
    /// <summary>
    ///     個体スケール下でHPバー逆スケール補正を検証
    ///     Verifies the HP bar counter-scale correction under an instance scale, uniform and non-uniform alike
    /// </summary>
    public class MapObjectHpBarScaleTest
    {
        // ForUnitTestに実在するmapObjectのguid。MasterHolder経由のInitializeを実際に通すため既存採掘テストと同じ値を使う
        // A mapObject guid that actually exists in ForUnitTest, reused from the existing mining tests so Initialize resolves through MasterHolder for real
        private static readonly Guid ExistingMapObjectGuid = new("00000000-0000-2222-0000-000000000001");

        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
        }

        [TestCase(4.73f, 4.73f, 4.73f, TestName = "均一スケール4.73倍")]
        [TestCase(2f, 3f, 4f, TestName = "非一様スケール")]
        public void HPバーが個体スケールに関わらずワールド等倍になる(float scaleX, float scaleY, float scaleZ)
        {
            _root = new GameObject("MapObjectHpBarScaleTestRoot");
            _root.transform.localScale = new Vector3(scaleX, scaleY, scaleZ);

            var hpBarView = CreateHpBarView(_root.transform);
            var mapObject = WireMapObject(_root, hpBarView);

            mapObject.SetRuntimeIdentity(1, ExistingMapObjectGuid.ToString());
            mapObject.Initialize(new GetMapObjectInfoProtocol.MapObjectsInfoMessagePack(1, false, 1));

            var lossyScale = hpBarView.transform.lossyScale;
            Assert.AreEqual(1f, lossyScale.x, 1e-4f, "hp bar world scale x drifted from the instance scale");
            Assert.AreEqual(1f, lossyScale.y, 1e-4f, "hp bar world scale y drifted from the instance scale");
            Assert.AreEqual(1f, lossyScale.z, 1e-4f, "hp bar world scale z drifted from the instance scale");

            #region Internal

            MapObjectHpBarView CreateHpBarView(Transform parent)
            {
                var hpBarObject = new GameObject("HpBar");
                hpBarObject.transform.SetParent(parent, false);

                var slider = hpBarObject.AddComponent<Slider>();
                var text = hpBarObject.AddComponent<TextMeshProUGUI>();
                var view = hpBarObject.AddComponent<MapObjectHpBarView>();

                var serializedView = new SerializedObject(view);
                serializedView.FindProperty("hpSlider").objectReferenceValue = slider;
                serializedView.FindProperty("hpText").objectReferenceValue = text;
                serializedView.ApplyModifiedPropertiesWithoutUndo();

                return view;
            }

            MapObjectGameObject WireMapObject(GameObject rootObject, MapObjectHpBarView view)
            {
                var component = rootObject.AddComponent<MapObjectGameObject>();
                var serializedMapObject = new SerializedObject(component);
                serializedMapObject.FindProperty("hpBarView").objectReferenceValue = view;
                serializedMapObject.ApplyModifiedPropertiesWithoutUndo();
                return component;
            }

            #endregion
        }
    }
}
