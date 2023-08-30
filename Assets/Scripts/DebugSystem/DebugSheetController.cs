using IngameDebugConsole;
using Tayx.Graphy;
using UnityDebugSheet.Runtime.Core.Scripts;
using UnityDebugSheet.Runtime.Extensions.Graphy;
using UnityDebugSheet.Runtime.Extensions.IngameDebugConsole;
using UnityEngine;

namespace DebugSystem
{
    public sealed class DebugSheetController : MonoBehaviour
    {
        [SerializeField] private DebugSheet debugSheet;
        [SerializeField] private DebugLogManager debugLogManager;
        
        private void Start()
        {
            var rootPage = DebugSheet.Instance.GetOrCreateInitialPage();

            rootPage.AddPageLinkButton<IngameDebugConsoleDebugPage>("In-Game Debug Console", onLoad: x => x.page.Setup(DebugLogManager.Instance));
            rootPage.AddPageLinkButton<GraphyDebugPage>("Graphy", onLoad: x => x.page.Setup(GraphyManager.Instance));
            rootPage.AddPageLinkButton<MainDebugPage>(nameof(MainDebugPage));
        }
    }
}