using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.Train.RailGraph;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect
{
    public class RailConnectPreviewObject : MonoBehaviour
    {
        [SerializeField] private BezierRailChain _railChainPrefab;
        private TrainRailConnectPreviewData _previewDataCache;
        private BezierRailChain _railChain;
        private RendererMaterialReplacerController _rendererMaterialReplacer;
        private Material _placeMaterial;
        
        private void Start()
        {
            _railChain = Instantiate(_railChainPrefab);
            _placeMaterial = Resources.Load<Material>(MaterialConst.PreviewPlaceBlockMaterial);
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
                _railChain.SetControlPoints(data.StartPoint, data.StartControlPoint, data.EndControlPoint, data.EndPoint);
                _railChain.Rebuild();
                _rendererMaterialReplacer = new RendererMaterialReplacerController(_railChain.gameObject);
                _rendererMaterialReplacer.CopyAndSetMaterial(_placeMaterial);
                if (!data.HasEnoughRailItem) _rendererMaterialReplacer.SetColor(MaterialConst.PreviewColorPropertyName, MaterialConst.NotPlaceableColor);
                _previewDataCache = data;
            }
        }
    }
}
