using System;
using Core.Master;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    public class TrainCarPreviewController : ITrainCarPreviewController, IDisposable
    {
        private readonly GameObject _previewObject;
        private readonly Renderer _renderer;
        private readonly Material _placeableMaterial;
        private readonly Material _notPlaceableMaterial;

        public TrainCarPreviewController()
        {
            _previewObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _previewObject.name = "TrainCarPreview";
            _previewObject.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(_previewObject);

            _renderer = _previewObject.GetComponent<Renderer>();
            var collider = _previewObject.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.Destroy(collider);
            }

            _placeableMaterial = CreatePreviewMaterial(new Color(0f, 1f, 0f, 0.35f));
            _notPlaceableMaterial = CreatePreviewMaterial(new Color(1f, 0f, 0f, 0.35f));

            _previewObject.SetActive(false);
        }

        public void Initialize(ItemId itemId)
        {
        }

        public void ShowPreview(Vector3 position, Quaternion rotation, bool isPlaceable)
        {
            if (_renderer != null)
            {
                _renderer.sharedMaterial = isPlaceable ? _placeableMaterial : _notPlaceableMaterial;
            }

            _previewObject.transform.SetPositionAndRotation(position, rotation);
            if (!_previewObject.activeSelf)
            {
                _previewObject.SetActive(true);
            }
        }

        public void HidePreview()
        {
            if (_previewObject.activeSelf)
            {
                _previewObject.SetActive(false);
            }
        }

        public void Dispose()
        {
            if (_previewObject != null)
            {
                UnityEngine.Object.Destroy(_previewObject);
            }
            if (_placeableMaterial != null)
            {
                UnityEngine.Object.Destroy(_placeableMaterial);
            }
            if (_notPlaceableMaterial != null)
            {
                UnityEngine.Object.Destroy(_notPlaceableMaterial);
            }
        }

        private static Material CreatePreviewMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader)
            {
                color = color,
                renderQueue = 3000
            };

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.DisableKeyword("_SURFACE_TYPE_OPAQUE");
                material.SetFloat("_AlphaClip", 0f);
            }
            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0f);
            }
            if (material.HasProperty("_Mode"))
            {
                material.SetFloat("_Mode", 3f);
            }
            if (material.HasProperty("_SrcBlend"))
            {
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            }
            if (material.HasProperty("_DstBlend"))
            {
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            }
            if (material.HasProperty("_ZWrite"))
            {
                material.SetInt("_ZWrite", 0);
            }
            material.renderQueue = 3000;
            return material;
        }
    }
}

