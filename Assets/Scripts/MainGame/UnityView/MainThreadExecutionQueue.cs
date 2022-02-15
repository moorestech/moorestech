using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace MainGame.UnityView
{
    /// <summary>
    /// ネットワークからviewの変更要求が来た時、メインスレッド出ないとメソッドを実行できません
    /// そのため、このオブジェクトのキューに入れ、Updateで呼び出します
    /// </summary>
    public class MainThreadExecutionQueue : MonoBehaviour
    {
        Queue<Action> _actionQueue;

        private void Awake()
        {
            _actionQueue = new Queue<Action>();
            _instance = this;
        }

        public void Insert(Action action)
        {
            lock (_actionQueue)
            {
                _actionQueue.Enqueue(action);
            }
        }

        private void Update()
        {
            lock (_actionQueue)
            {
                while (_actionQueue.Count > 0)
                {
                    _actionQueue.Dequeue()();
                }
            }
        }
        
        private static MainThreadExecutionQueue _instance;

        public static MainThreadExecutionQueue Instance
        {
            get
            {
                if (_instance != null) return _instance;
                throw new Exception("MainThreadExecutionQueueがアタッチされていません");

            }
        }
    }
}