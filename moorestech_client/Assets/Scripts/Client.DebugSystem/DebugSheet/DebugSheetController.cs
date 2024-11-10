using IngameDebugConsole;
using Tayx.Graphy;
using UnityDebugSheet.Runtime.Core.Scripts;
using UnityDebugSheet.Runtime.Extensions.Graphy;
using UnityDebugSheet.Runtime.Extensions.IngameDebugConsole;
using UnityEngine;

namespace Client.DebugSystem
{
    public sealed class DebugSheetController : MonoBehaviour
    {
        [SerializeField] private GameObject runtimeHierarchyInspector;
        [SerializeField] private DebugSheet debugSheet;
        
        private void Start()
        {
            debugSheet.gameObject.SetActive(true);
            
            var rootPage = debugSheet.GetOrCreateInitialPage();
            
            rootPage.AddPageLinkButton<ItemGetDebugSheet>("Get Item");
            rootPage.AddPageLinkButton<IngameDebugConsoleDebugPage>("In-Game Debug Console", onLoad: x => x.page.Setup(DebugLogManager.Instance));
            rootPage.AddPageLinkButton<GraphyDebugPage>("Graphy", onLoad: x => x.page.Setup(GraphyManager.Instance));
            rootPage.AddSwitch(false, "Runtime Hierarchy Inspector", valueChanged: active => runtimeHierarchyInspector.SetActive(active));
        }
        
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void CreateDebugger()
        {
            var prefab = Resources.Load<GameObject>("moorestech Debug Objects");
            Instantiate(prefab);
        }
    }
}