using System;
using Cinemachine;
using UnityEngine;

namespace MainGame.Control.Game.MouseKeyboard
{
    public class CameraDistancesControl : MonoBehaviour
    {
        [SerializeField] private CinemachineVirtualCamera virtualCamera;
        private CinemachineFramingTransposer cinemachineFraming;
        public MoorestechInputSettings _inputSettings;

        private void Awake()
        {
            cinemachineFraming = virtualCamera.GetCinemachineComponent<CinemachineFramingTransposer>();
            _inputSettings = new MoorestechInputSettings();
            _inputSettings.Enable();
        }

        private void Update()
        {
            cinemachineFraming.m_CameraDistance += _inputSettings.UI.SwitchHotBar.ReadValue<float>() / -100;

            cinemachineFraming.m_CameraDistance = Mathf.Clamp(cinemachineFraming.m_CameraDistance, 10, 75);
        }
    }
}