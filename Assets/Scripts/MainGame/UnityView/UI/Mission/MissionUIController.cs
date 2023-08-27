using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

namespace MainGame.UnityView.UI.Mission
{
    public class MissionUIController : MonoBehaviour
    {
        private const int DisplayMissionCount = 5;

        [SerializeField] private MissionBarUIElement missionBarUIPrefab;
        [SerializeField] private RectTransform missionBarParent;
        
        private readonly List<MissionBar> _sortedPriorityMissions = new();

        public void SetMissionList(List<MissionBase> missionDataList)
        {
            foreach (var missionData in missionDataList)
            {
                var missionBar = Instantiate(missionBarUIPrefab, missionBarParent);
                
                missionBar.SetMissionNameKey(missionData.MissionNameKey,missionData.MissionNameAddContents);
                missionData.OnDone.Subscribe(_ => SetDaneMissionBar(missionBar).Forget()).AddTo(this);
                
                _sortedPriorityMissions.Add(new MissionBar(missionBar, missionData));
            }
            _sortedPriorityMissions.Sort((a, b) => b.MissionBase.Priority - a.MissionBase.Priority);
            UpdateDisplayMission();
        }

        private async UniTask SetDaneMissionBar(MissionBarUIElement missionBarUIElement)
        {
            missionBarUIElement.SetActive(true);
            await missionBarUIElement.SetDone();
            UpdateDisplayMission();
        }


        /// <summary>
        /// プライオリティの高いミッションから順に表示する
        /// </summary>
        private void UpdateDisplayMission()
        {
            //全てのミッションをオフに
            foreach (var missionBar in _sortedPriorityMissions)
            {
                missionBar.MissionBarUIElement.SetActive(false);
            }
            
            //プライオリティの高いミッションから表示する数分だけ表示する
            //もし完了していたら表示しない
            var displayedBarCount = 0;
            foreach (var missionBar in _sortedPriorityMissions)
            {
                if (missionBar.MissionBase.IsDone)
                {
                    continue;
                }
                
                displayedBarCount++;
                if (DisplayMissionCount < displayedBarCount)
                {
                    break;
                }
                
                missionBar.MissionBarUIElement.SetActive(true);
            }
        }
    }

    class MissionBar
    {
        public readonly MissionBarUIElement MissionBarUIElement;
        public readonly MissionBase MissionBase;
        
        public MissionBar(MissionBarUIElement missionBarUIElement, MissionBase missionBase)
        {
            MissionBarUIElement = missionBarUIElement;
            MissionBase = missionBase;
        }
    }
}