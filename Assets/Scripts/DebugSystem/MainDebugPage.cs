using System.Collections;
using UnityDebugSheet.Runtime.Core.Scripts;
using UnityEngine;

namespace DebugSystem
{
    public class MainDebugPage : DefaultDebugPageBase
    {
        protected override string Title => "moorestech Debug Page";
        
        
        public override IEnumerator Initialize()
        {
            // Add a button to this page.
            AddButton("Example Button", clicked: () => { Debug.Log("Clicked"); });

            yield break;
        }
    }
}