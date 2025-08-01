﻿using System.Reflection;
using Client.Game.InGame.Context;
using Client.Network.API;
using IngameDebugConsole;
using Server.Protocol.PacketResponse;
using Tayx.Graphy;
using UnityDebugSheet.Runtime.Core.Scripts;
using UnityDebugSheet.Runtime.Extensions.Graphy;
using UnityDebugSheet.Runtime.Extensions.IngameDebugConsole;
using UnityEngine;
using static Client.Game.DebugConst;

namespace Client.DebugSystem
{
    public sealed class DebugSheetController : MonoBehaviour
    {
        [SerializeField] private GameObject runtimeHierarchyInspector;
        [SerializeField] private DebugSheet debugSheet;
        private static DebugSheet _staticDebugSheet;
        
        private void Start()
        {
            _staticDebugSheet = debugSheet;
            debugSheet.gameObject.SetActive(true);
            
            var rootPage = debugSheet.GetOrCreateInitialPage();
            
            rootPage.AddPageLinkButton<ItemGetDebugSheet>("Get Item");
            rootPage.AddPageLinkButton<SkitDebugSheet>("Skit Player");
            rootPage.AddPageLinkButton<CinematicCameraDebugSheet>("Cinematic Camera");
            rootPage.AddPageLinkButton<IngameDebugConsoleDebugPage>("In-Game Debug Console", onLoad: x => x.page.Setup(DebugLogManager.Instance));
            rootPage.AddPageLinkButton<GraphyDebugPage>("Graphy", onLoad: x => x.page.Setup(GraphyManager.Instance));
            rootPage.AddSwitch(false, "Runtime Hierarchy Inspector", valueChanged: active => runtimeHierarchyInspector.SetActive(active));
            rootPage.AddButton("Clear Inventory", clicked: () =>
            {
                var command = $"{SendCommandProtocol.ClearInventoryCommand} {ClientContext.PlayerConnectionSetting.PlayerId}";
                ClientContext.VanillaApi.SendOnly.SendCommand(command);
            });
            
            rootPage.AddEnumPickerWithSave(DebugEnvironmentType.Debug, "Select Environment", "DebugEnvironmentTypeKey", DebugEnvironmentController.SetEnvironment);
            rootPage.AddBoolWithSave(false, IsItemListViewForceShowLabel, IsItemListViewForceShowKey);
            rootPage.AddBoolWithSave(false, SkitPlaySettingsLabel, SkitPlaySettingsKey);
            rootPage.AddBoolWithSave(false, MapObjectSuperMineLabel, MapObjectSuperMineKey);
            
        }
        
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void CreateDebugger()
        {
            var prefab = Resources.Load<GameObject>("moorestech Debug Objects");
            Instantiate(prefab);
        }
        
        public static void CloseDebugSheet()
        {
            // OnCloseButtonClickedをリフレクションで実行
            var onCloseButtonClicked = typeof(DebugSheet).GetMethod("OnCloseButtonClicked", BindingFlags.NonPublic | BindingFlags.Instance);  
            onCloseButtonClicked.Invoke(_staticDebugSheet, null);
        }
    }
}