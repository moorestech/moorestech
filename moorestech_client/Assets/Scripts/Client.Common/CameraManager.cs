using System;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;

namespace Client.Common
{
    public interface IGameCamera
    {
        Camera Camera { get; }
        void SetEnabled(bool cameraEnabled);
    }
    
    public class CameraManager
    {
        private static CameraManager _instance;
        
        /// <summary>現在有効になっている最上位カメラ。存在しなければ null。</summary>
        public static IGameCamera MainCamera => _instance == null || _instance._mainCameraStack.Count == 0 ? null : _instance._mainCameraStack.Peek();
        public static IObservable<IGameCamera> OnMainCameraChanged => _instance._mainCameraChanged;
        
        /// <summary>カメラを積む／外すためのスタック</summary>
        private readonly Stack<IGameCamera> _mainCameraStack = new();
        private readonly Subject<IGameCamera> _mainCameraChanged = new();
        
        
        public static void Initialize()
        {
            _instance = new CameraManager();
        }

        /// <summary>
        /// 新しいカメラを登録し、描画対象を移行する。
        /// すでに同じカメラが積まれている場合は重複させずに最上位へ移動する。
        /// </summary>
        public static void RegisterCamera(IGameCamera camera)
        {
            if (_instance == null) return;
            if (camera == null) return;
            var previousMainCamera = MainCamera;

            // 既存カメラを元の順序を保ったまま積み直す
            // Restack an existing camera while preserving the original order
            var mainCameraStack = _instance._mainCameraStack;
            if (mainCameraStack.Contains(camera))
            {
                var tmp = mainCameraStack.Where(c => c != camera).ToArray();
                mainCameraStack.Clear();
                for (int i = tmp.Length - 1; i >= 0; i--)
                    mainCameraStack.Push(tmp[i]);
            }

            // 描画対象を新しい最上位カメラへ移す
            // Move rendering ownership to the new top camera
            if (MainCamera != null)
            {
                MainCamera.SetEnabled(false);
            }

            // 新しいカメラを最上位へ積んで変更を通知する
            // Push and enable the new top camera, then publish the change
            mainCameraStack.Push(camera);
            camera.SetEnabled(true);
            NotifyIfMainCameraChanged(previousMainCamera);
        }

        /// <summary>
        /// カメラを登録解除し、直下のカメラを復帰させる。
        /// スタック最上位にないカメラは静かに除去。
        /// </summary>
        public static void UnRegisterCamera(IGameCamera camera)
        {
            if (_instance == null) return;
            var mainCameraStack = _instance._mainCameraStack;
            if (camera == null || !mainCameraStack.Contains(camera))
                return;

            // 最上位を外したときだけ直下のカメラへ復帰する
            // Restore the camera below only when removing the current top
            if (mainCameraStack.Peek() == camera)
            {
                var previousMainCamera = MainCamera;
                camera.SetEnabled(false);
                mainCameraStack.Pop();

                if (MainCamera != null)
                {
                    MainCamera.SetEnabled(true);
                }
                NotifyIfMainCameraChanged(previousMainCamera);
                return;
            }
            
            // 途中のカメラは最上位へ影響させず除去する
            // Remove an intermediate camera without changing the current top
            var tmp = mainCameraStack.Where(c => c != camera).ToArray();
            mainCameraStack.Clear();
            for (int i = tmp.Length - 1; i >= 0; i--)
                mainCameraStack.Push(tmp[i]);
        }

        private static void NotifyIfMainCameraChanged(IGameCamera previousMainCamera)
        {
            if (ReferenceEquals(previousMainCamera, MainCamera)) return;
            _instance._mainCameraChanged.OnNext(MainCamera);
        }
    }
}
