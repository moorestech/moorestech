using System.Collections;
using System.Collections.Generic;
using Client.Game.Common;
using Client.Game.Skit;
using Cysharp.Threading.Tasks;
using UnityDebugSheet.Runtime.Core.Scripts;
using UnityEngine;

namespace Client.DebugSystem
{
    public class SkitDebugSheet : DefaultDebugPageBase
    {
        protected override string Title => "Skit Player";
        
        public override IEnumerator Initialize()
        {
            var customPath = "Vanilla/Skit/skits/100_start_game";
            AddInputField("Skit Addressable Path", value:customPath ,valueChanged: value => customPath = value);
            
            AddButton("Play Custom Skit", subText: "Play skit from custom path", clicked: () =>
            {
                if (string.IsNullOrEmpty(customPath))
                {
                    Debug.LogError("Please enter a custom addressable path.");
                    return;
                }
                
                var skitManager = FindObjectOfType<SkitManager>();
                if (skitManager.IsPlayingSkit)
                {
                    Debug.LogError("A skit is already playing. Please wait for it to finish.");
                    return;
                }
                
                GameStateController.ChangeState(GameStateType.Skit);
                skitManager.StartSkit(customPath).Forget();
                DebugSheetController.CloseDebugSheet();
            });
            
            
            yield break;
        }
    }
}