using Client.Game.InGame.Train;
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
        }
        
        public void ShowPreview(TrainRailConnectPreviewData data)
        {
            Debug.Log($"ShowPreview {data.StartPoint} {data.StartControlPoint} {data.EndControlPoint} {data.EndPoint}");
            Debug.DrawLine(data.StartPoint, data.StartControlPoint, Color.blue);
            Debug.DrawLine(data.StartControlPoint, data.EndControlPoint, Color.purple);
            Debug.DrawLine(data.EndControlPoint, data.EndPoint, Color.red);
            Debug.DrawLine(data.StartPoint - Vector3.down * 0.1f, data.EndPoint - Vector3.down * 0.1f, Color.white);
            
            if (!_previewDataCache.Equals(data))
            {
                _railChain.SetControlPoints(data.StartPoint, data.StartControlPoint, data.EndControlPoint, data.EndPoint);
                _railChain.Rebuild();
                _previewDataCache = data;
            }
        }
    }
}