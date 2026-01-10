using System;
using InGame.Train.Rail;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect
{
    public class RailConnectPreviewObject : MonoBehaviour
    {
        [SerializeField] private BezierRailChain _railChainPrefab;
        private TrainRailConnectPreviewData _previewDataCache;
        private BezierRailChain _railChain;
        
        private void Start()
        {
            _railChain = Instantiate(_railChainPrefab);
        }
        
        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
            _railChain.gameObject.SetActive(active);
        }
        
        public void ShowPreview(TrainRailConnectPreviewData data)
        {
            Debug.Log($"ShowPreview {data.P0} {data.P1} {data.P2} {data.P3}");
            Debug.DrawLine(data.P0, data.P1, Color.blue);
            Debug.DrawLine(data.P1, data.P2, Color.purple);
            Debug.DrawLine(data.P2, data.P3, Color.red);
            Debug.DrawLine(data.P0 - Vector3.down * 0.1f, data.P3 - Vector3.down * 0.1f, Color.white);
            // TODO
            
            if (!_previewDataCache.Equals(data))
            {
                _railChain.SetControlPoints(data.P0, data.P1, data.P2, data.P3);   
                _railChain.Rebuild();
                _previewDataCache = data;
            }
        }
    }
}