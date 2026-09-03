using System;
using System.Collections.Generic;
using System.Reflection;
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.Inventory.RecipeViewer;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.CancelInput;
using Client.Network.API;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Server.Util.MessagePack;
using Tests.Module.TestMod;
using UnityEngine;
using static Server.Protocol.PacketResponse.PlayerInventoryResponseProtocol;

namespace Client.Tests.Inventory
{
    /// <summary>
    ///     初期インベントリ応答が装備モデルへ実際に適用されるかを、PlayerInventoryStateの結線ごと検証する。
    ///     装備は専用の取得プロトコルを持たずこの応答に相乗りするため、結線が1行落ちるだけで初期装備が無言で失われる。
    ///     Verifies that the initial inventory response actually reaches the equipment model through PlayerInventoryState's wiring.
    ///     Equipment has no dedicated fetch protocol and rides on this response, so dropping one wiring line silently loses it.
    /// </summary>
    public class PlayerInventoryStateEquipmentApplyTest
    {
        // 装備モデルの初期値は先頭(0)なので、それと区別できるスロットを選んでおく
        // The equipment model starts at the first slot (0), so pick a slot that is distinguishable from it
        private const int SelectedSlot = 1;
        private static readonly Guid ToolItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        private readonly List<GameObject> _createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var gameObject in _createdObjects) UnityEngine.Object.DestroyImmediate(gameObject);
            _createdObjects.Clear();
        }

        [Test]
        public void 初期インベントリ応答の装備と選択インデックスが装備モデルへ適用される()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var toolItemId = MasterHolder.ItemMaster.GetItemId(ToolItemGuid);
            var equipment = new LocalPlayerEquipment();

            // 装備スロット0に工具、選択はスロット1という初期値と異なる状態を応答に載せる
            // The response carries a tool in slot 0 and selects slot 1, both differing from the model's initial state
            var handshake = CreateHandshakeResponse(toolItemId);
            CreatePlayerInventoryState(equipment, handshake);

            Assert.AreEqual(toolItemId, equipment.Slots[0].Id);
            Assert.AreEqual(SelectedSlot, equipment.SelectedIndex);

            #region Internal

            InitialHandshakeResponse CreateHandshakeResponse(ItemId itemId)
            {
                var equipmentSlots = new[] { new ItemMessagePack(itemId, 1), new ItemMessagePack(ItemMaster.EmptyItemId, 0) };
                var inventory = new PlayerInventoryResponse(new PlayerInventoryResponseProtocolMessagePack(
                    0, Array.Empty<ItemMessagePack>(), new ItemMessagePack(ItemMaster.EmptyItemId, 0), equipmentSlots, SelectedSlot));

                // Handshake使用項目だけを設定
                // Set only fields consumed by the handshake
#pragma warning disable CS0618
                var initialHandshake = new global::Server.Protocol.PacketResponse.InitialHandshakeProtocol.ResponseInitialHandshakeMessagePack
                {
                    PlayerPos = new Vector3MessagePack(Vector3.zero),
                };
#pragma warning restore CS0618

                return new InitialHandshakeResponse(initialHandshake, (null, null, inventory, null, null, null, null, null));
            }

            void CreatePlayerInventoryState(LocalPlayerEquipment playerEquipment, InitialHandshakeResponse initialHandshake)
            {
                // uGUIビューはSetActiveしか呼ばれないため最小の実体を渡す
                // The uGUI views only receive SetActive, so pass minimal instances
                var recipeViewerView = CreateComponent<RecipeViewerView>("RecipeViewer");
                var viewController = CreateComponent<PlayerInventoryViewController>("PlayerInventoryView");
                SetPrivateField(viewController, "mainInventoryObject", CreateObject("MainInventory"));
                SetPrivateField(viewController, "subInventoryParent", CreateObject("SubInventoryParent").transform);

                new PlayerInventoryState(recipeViewerView, viewController,
                    new LocalPlayerInventoryController(new LocalPlayerInventory(), playerEquipment),
                    playerEquipment, initialHandshake, new RightShortPressInputService(new RightShortPressInput()));
            }

            T CreateComponent<T>(string name) where T : Component
            {
                return CreateObject(name).AddComponent<T>();
            }

            GameObject CreateObject(string name)
            {
                var gameObject = new GameObject(name);
                gameObject.SetActive(false);
                _createdObjects.Add(gameObject);
                return gameObject;
            }

            void SetPrivateField(object target, string fieldName, object value)
            {
                var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(field, $"field not found: {fieldName}");
                field.SetValue(target, value);
            }

            #endregion
        }
    }
}
