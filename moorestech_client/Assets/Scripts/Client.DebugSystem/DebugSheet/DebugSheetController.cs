using System.Reflection;
using Client.DebugSystem.Environment;
using Client.Game.InGame.Context;
using Client.Game.InGame.Player;
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
            
            /*
             * デバッグコマンドの実装方法:
             * 1. DebugConst.csに定数（Label, Key）を追加
             * 2. DebugSheetController.csでrootPage.AddBoolWithSaveを使ってトグルを追加
             * 3. 実際の機能箇所でDebugParameters.GetValueOrDefaultBool(キー)で値を取得して処理
             * ※DebugParametersはmoorestech_server側のクラスで、値はcache/BoolDebugParameters.json等に永続化される
             */
            
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
            rootPage.AddButton("Get Play Time", clicked: () =>
            {
                ClientContext.VanillaApi.SendOnly.SendCommand(SendCommandProtocol.GetPlayTimeCommand);
            });
            
            rootPage.AddEnumPickerWithSave(DebugEnvironmentType.Debug, "Select Environment", "DebugEnvironmentTypeKey", DebugEnvironmentController.SetEnvironment);
            rootPage.AddButton("Warp Environment Default Position", clicked: () =>
            {
                var allPositions = Object.FindObjectsByType<EnvironmentDefaultPosition>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (allPositions.Length == 0)
                {
                    Debug.LogError("EnvironmentDefaultPosition not found in the scene.");
                    return;
                }

                // アクティブなものを優先、なければ最初のものを使用
                // Prioritize active ones, use first one if none are active
                var targetPosition = System.Array.Find(allPositions, p => p.gameObject.activeInHierarchy) ?? allPositions[0];

                var playerObjectController = PlayerSystemContainer.Instance.PlayerObjectController;
                playerObjectController.SetPlayerPosition(targetPosition.transform.position);
            });
            
            rootPage.AddBoolWithSave(false, IsItemListViewForceShowLabel, IsItemListViewForceShowKey);
            rootPage.AddBoolWithSave(false, SkitPlaySettingsLabel, SkitPlaySettingsKey);
            rootPage.AddBoolWithSave(false, MapObjectSuperMineLabel, MapObjectSuperMineKey);
            rootPage.AddBoolWithSave(false, FixCraftTimeLabel, FixCraftTimeKey);
            rootPage.AddBoolWithSave(false, TrainAutoRunLabel, TrainAutoRunKey);
            rootPage.AddBoolWithSave(false, PlacePreviewKeepLabel, PlacePreviewKeepKey);
        }
        public static void CloseDebugSheet()
        {
            // OnCloseButtonClickedをリフレクションで実行
            var onCloseButtonClicked = typeof(DebugSheet).GetMethod("OnCloseButtonClicked", BindingFlags.NonPublic | BindingFlags.Instance);  
            onCloseButtonClicked.Invoke(_staticDebugSheet, null);
        }
    }
}
