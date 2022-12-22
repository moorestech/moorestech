using System;
using Cinemachine;
using UnityEngine;

namespace MainGame.UnityView.Control.MouseKeyboard
{
    public class CameraController : MonoBehaviour
    {
        private const float XSpeed = 10;
        private const float YSpeed = 0.05f;
        [SerializeField] private CinemachineFreeLook cinemachineFreeLook;

        public bool IsCameraControlling { get; private set; }
        private float currentDistance = 10f;

        private void Start()
        {
            cinemachineFreeLook.m_YAxis.Value = 0.3f;
        }

        private void Update()
        {
            if (InputManager.Playable.ScreenRightClick.GetKey)
            {
                IsCameraControlling = true;
                cinemachineFreeLook.m_XAxis.m_MaxSpeed = XSpeed;
                cinemachineFreeLook.m_YAxis.m_MaxSpeed = YSpeed;
            }
            else
            {
                IsCameraControlling = false;
                cinemachineFreeLook.m_XAxis.m_MaxSpeed = 0;
                cinemachineFreeLook.m_YAxis.m_MaxSpeed = 0;
            }
            
            currentDistance += InputManager.UI.SwitchHotBar.ReadValue<float>() / -100;
            currentDistance = Mathf.Clamp(currentDistance, 6, 75);
            
            //Topの設定
            cinemachineFreeLook.m_Orbits[0].m_Height = currentDistance;

            //Middleの設定
            cinemachineFreeLook.m_Orbits[1].m_Height = currentDistance/2 + (currentDistance - currentDistance/2) / 2;
            cinemachineFreeLook.m_Orbits[1].m_Radius = currentDistance/2 + (currentDistance - currentDistance/2) / 2;
            
            //Bottomの設定
            cinemachineFreeLook.m_Orbits[2].m_Height = currentDistance/2;
            cinemachineFreeLook.m_Orbits[2].m_Radius = currentDistance/2;
        }
    }
}