using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

namespace MainGame.UnityView.UI.Mission
{
    public class MissionUIController : MonoBehaviour
    {
        private const int DisplayMissionCount = 5;

        [SerializeField] private MissionBarElement missionBarPrefab;
        [SerializeField] private RectTransform missionBarParent;
        
        private readonly List<MissionBarElement> _missionBars = new();
        private readonly List<IMissionImplementation> _sortedPriorityMissionDataList = new();

        public void SetMissionList(List<IMissionImplementation> missionDataList)
        {
            foreach (var missionData in missionDataList)
            {
                var missionBar = Instantiate(missionBarPrefab, missionBarParent);
                
                missionBar.SetMissionNameKey(missionData.MissionNameKey);
                missionData.OnDone.Subscribe(_ => SetDaneMissionBar(missionBar).Forget()).AddTo(this);
                
                _missionBars.Add(missionBar);
                _sortedPriorityMissionDataList.Add(missionData);
            }
            _sortedPriorityMissionDataList.Sort((a, b) => b.Priority - a.Priority);
        }

        private async UniTask SetDaneMissionBar(MissionBarElement missionBarElement)
        {
            await missionBarElement.SetDone();
            UpdateDisplayMission();
        }


        /// <summary>
        /// プライオリティの高いミッションから順に表示する
        /// </summary>
        private void UpdateDisplayMission()
        {
            //全てのミッションをオフに
            foreach (var missionBar in _missionBars)
            {
                missionBar.SetActive(false);
            }
            
            //プライオリティの高いミッションから順に表示する
            for (var i = 0; i < DisplayMissionCount; i++)
            {
                if (i >= _sortedPriorityMissionDataList.Count) break;
                //すでに完了してたら表示しない
                if (_sortedPriorityMissionDataList[i].IsDone)
                {
                    i--;
                    continue;
                }
                
                
                var missionData = _sortedPriorityMissionDataList[i];
                _missionBars[i].SetActive(true);
            }
        }
    }
}