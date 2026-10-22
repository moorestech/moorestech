using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.Preview3D
{
    // RenderTextureを背景にした3Dプレビュー。専用カメラで隔離した位置のモデルを写し、ドラッグで回しホイールで寄る
    // 3D preview backed by a RenderTexture; a dedicated camera films an isolated model, drag orbits and wheel zooms
    public class ModelPreviewElement : VisualElement
    {
        private readonly Camera _camera;
        private readonly RenderTexture _texture;
        private readonly Transform _pivot;
        private GameObject _model;
        private float _yaw = 35f;
        private float _pitch = 25f;
        private float _distanceScale = 1f;
        private float _radius = 1f;
        private bool _autoRotate;
        private bool _dragging;

        public ModelPreviewElement(Vector3 stagePosition, int resolution, int layer)
        {
            AddToClassList("p3-preview");
            _texture = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            _pivot = new GameObject("PreviewPivot").transform;
            _pivot.position = stagePosition;
            _camera = new GameObject("PreviewCamera").AddComponent<Camera>();
            _camera.targetTexture = _texture;
            _camera.cullingMask = 1 << layer;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0, 0, 0, 0);
            _camera.fieldOfView = 30f;
            style.backgroundImage = Background.FromRenderTexture(_texture);

            // パネルから外れている間はカメラを止め、描画コストを払わない
            // Stop the camera while detached so hidden previews cost nothing
            RegisterCallback<AttachToPanelEvent>(_ => _camera.enabled = true);
            RegisterCallback<DetachFromPanelEvent>(_ => _camera.enabled = false);
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<WheelEvent>(evt => { _distanceScale = Mathf.Clamp(_distanceScale * Mathf.Exp(evt.delta.y * 0.08f), 0.4f, 3f); UpdateCamera(); });
            schedule.Execute(timer => Tick(timer.deltaTime / 1000f)).Every(0);
        }

        public void SetAutoRotate(bool autoRotate)
        {
            _autoRotate = autoRotate;
        }

        public void SetModel(GameObject model)
        {
            if (_model != null) Object.Destroy(_model);
            _model = model;
            _model.transform.SetParent(_pivot, false);

            // 境界の中心を軸に合わせ、半径から撮影距離を決める
            // Center bounds on the pivot and derive camera distance from the radius
            var bounds = RenderOnlyModelBuilder.CalculateBounds(_model);
            _model.transform.position += _pivot.position - bounds.center;
            _radius = Mathf.Max(0.3f, bounds.extents.magnitude);
            UpdateCamera();
        }

        private void Tick(float deltaSeconds)
        {
            if (!_autoRotate || _dragging) return;
            _yaw += deltaSeconds * 40f;
            UpdateCamera();
        }

        private void UpdateCamera()
        {
            var distance = _radius / Mathf.Sin(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f * _distanceScale;
            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            _camera.transform.position = _pivot.position + rotation * new Vector3(0f, 0f, -distance);
            _camera.transform.rotation = rotation;
            _camera.nearClipPlane = Mathf.Max(0.01f, distance - _radius * 2f);
            _camera.farClipPlane = distance + _radius * 2f;
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            _dragging = true;
            this.CapturePointer(evt.pointerId);
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_dragging || !this.HasPointerCapture(evt.pointerId)) return;
            _yaw += evt.deltaPosition.x * 0.4f;
            _pitch = Mathf.Clamp(_pitch + evt.deltaPosition.y * 0.4f, -80f, 80f);
            UpdateCamera();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            _dragging = false;
            this.ReleasePointer(evt.pointerId);
        }
    }
}
