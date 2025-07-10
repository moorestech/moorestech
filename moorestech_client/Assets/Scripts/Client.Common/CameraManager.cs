using System.Collections.Generic;
using System.Linq;
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
        public static CameraManager Instance { get; private set; }
        
        /// <summary>現在有効になっている最上位カメラ。存在しなければ null。</summary>
        public IGameCamera MainCamera => _mainCameraStack.LastOrDefault();
        
        /// <summary>カメラを積む／外すためのスタック</summary>
        private readonly Stack<IGameCamera> _mainCameraStack = new();
        
        
        public static void Initialize()
        {
            Instance = new CameraManager();
        }

        /// <summary>
        /// 新しいカメラを登録し、描画対象を移行する。
        /// すでに同じカメラが積まれている場合は重複させずに最上位へ移動する。
        /// </summary>
        public void RegisterCamera(IGameCamera camera)
        {
            if (camera == null) return;

            // すでに同じカメラがスタックにある場合は一度除去
            if (_mainCameraStack.Contains(camera))
            {
                var tmp = _mainCameraStack.Where(c => c != camera).ToArray();
                _mainCameraStack.Clear();
                // 元の順序を保つように再プッシュ
                for (int i = tmp.Length - 1; i >= 0; i--)
                    _mainCameraStack.Push(tmp[i]);
            }

            // 現在の最上位カメラを無効化
            if (MainCamera != null)
            {
                MainCamera.SetEnabled(false);
            }

            // 新しいカメラを積んで有効化
            _mainCameraStack.Push(camera);
            camera.SetEnabled(true);
        }

        /// <summary>
        /// カメラを登録解除し、直下のカメラを復帰させる。
        /// スタック最上位にないカメラは静かに除去。
        /// </summary>
        public void UnRegisterCamera(IGameCamera camera)
        {
            if (camera == null || !_mainCameraStack.Contains(camera))
                return;

            // 最上位の場合
            if (_mainCameraStack.Peek() == camera)
            {
                camera.SetEnabled(false);
                _mainCameraStack.Pop();

                // 直下のカメラを再有効化
                if (MainCamera != null)
                {
                    MainCamera.SetEnabled(true);
                }
                return;
            }
            
            // スタックの途中にある場合は単に除去
            var tmp = _mainCameraStack.Where(c => c != camera).ToArray();
            _mainCameraStack.Clear();
            for (int i = tmp.Length - 1; i >= 0; i--)
                _mainCameraStack.Push(tmp[i]);
        }
    }
}
