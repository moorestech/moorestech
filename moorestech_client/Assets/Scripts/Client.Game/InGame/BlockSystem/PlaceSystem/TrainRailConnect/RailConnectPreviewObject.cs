using Client.Common;
using Client.Game.InGame.Train.RailGraph;
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
            // プレビュー用レールを初期化する
            // Initialize preview rail chain
            _railChain = Instantiate(_railChainPrefab);
            _railChain.SetUseGpuDeform(true);
            _railChain.SetPreviewColor(MaterialConst.PlaceableColor);
        }
        
        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
            _railChain.gameObject.SetActive(active);
            if (!active) _previewDataCache = TrainRailConnectPreviewData.Invalid;
        }
        
        public void ShowPreview(TrainRailConnectPreviewData data)
        {
            Debug.DrawLine(data.StartPoint, data.StartControlPoint, Color.blue);
            Debug.DrawLine(data.StartControlPoint, data.EndControlPoint, Color.purple);
            Debug.DrawLine(data.EndControlPoint, data.EndPoint, Color.red);
            Debug.DrawLine(data.StartPoint - Vector3.down * 0.1f, data.EndPoint - Vector3.down * 0.1f, Color.white);
            
            if (!_previewDataCache.Equals(data))
            {
                // プレビュー内容が変わった時だけ更新する
                // Update only when preview data changes
                _railChain.SetControlPoints(data.StartPoint, data.StartControlPoint, data.EndControlPoint, data.EndPoint);
                _railChain.SetPreviewColor(data.HasEnoughRailItem ? MaterialConst.PlaceableColor : MaterialConst.NotPlaceableColor);
                _railChain.Rebuild();
                _previewDataCache = data;
            }
        }
    }
}
