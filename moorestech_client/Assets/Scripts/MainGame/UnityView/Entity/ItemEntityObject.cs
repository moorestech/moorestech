using MainGame.Basic.Server;
using UnityEngine;

namespace MainGame.UnityView.Entity
{
    public class ItemEntityObject : MonoBehaviour, IEntityObject
    {
        private const float Interval = NetworkConst.UpdateIntervalSeconds;
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private Material itemMaterial;
        private float _linerTime;

        private Vector3 _targetPosition;

        private void Awake()
        {
            _targetPosition = transform.position;
        }

        //Linerでポジションを補完させる
        private void Update()
        {
            //補完する
            var rate = _linerTime / Interval;
            rate = Mathf.Clamp01(rate);
            transform.position = Vector3.Lerp(transform.position, _targetPosition, rate);
            _linerTime += Time.deltaTime;
        }

        public void SetDirectPosition(Vector3 position)
        {
            _targetPosition = position;
            transform.position = position;
        }

        public void SetInterpolationPosition(Vector3 position)
        {
            _targetPosition = position;
            _linerTime = 0;
        }

        public void Destroy()
        {
            Destroy(gameObject);
        }

        public void SetTexture(Texture texture)
        {
            var material = new Material(itemMaterial) { mainTexture = texture };
            meshRenderer.material = material;
        }
    }
}