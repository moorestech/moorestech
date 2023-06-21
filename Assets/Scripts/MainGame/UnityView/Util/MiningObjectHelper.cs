using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MainGame.UnityView.UI.Inventory.View.HotBar;
using UnityEngine;

namespace MainGame.UnityView.Util
{
    /// <summary>
    /// 何かを採掘する処理を簡単にして一つにまとめる
    /// 同時に複数の箇所で採掘したりすることを防いだり、UIへの設定を簡単にしたり、非同期で簡単に処理を書けるようにする
    /// </summary>
    public class MiningObjectHelper : MonoBehaviour
    {
        [SerializeField] private ProgressBarView progressBarView;
        public bool IsMining { get; private set; }
        
        
        public delegate bool IsContinueMiningDelegate();

        /// <summary>
        /// 採掘を開始する
        /// キャンセルさせるまで採掘し続ける
        /// キャンセルしてもexceptionは発生しません
        /// </summary>
        /// <param name="miningTime">採掘時間 秒</param>
        /// <param name="OnMined">採掘が完了したら呼び出される</param>
        /// <param name="isContinueMining">採掘を続けるかどうかを判定するメソッド</param>
        /// <param name="cancellationToken">キャンセラレーショントークン</param>
        /// <exception cref="InvalidOperationException">採掘中にこのメソッドを呼び出した場合、この例外がスローされます。</exception>
        public async UniTask StartMining(float miningTime,Action OnMined,IsContinueMiningDelegate isContinueMining,CancellationToken cancellationToken)
        {
            if (IsMining)
            {
                throw new InvalidOperationException("採掘中に他の箇所から採掘開始を呼び出されました。IsMiningフラグを確認し、採掘中でないことを確認してください");
            }
            
            IsMining = true;
            try
            {
                //キャンセルされるまで採掘し続ける
                while (true)
                {
                    await MiningProgress(miningTime,cancellationToken,isContinueMining);
                    OnMined?.Invoke();
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                IsMining = false;
            }
        }

        private async UniTask MiningProgress(float miningTime, CancellationToken cancellationToken, IsContinueMiningDelegate isContinueMining)
        {
            var now = DateTime.Now;
            //UIの更新のためにwhileで回す
            while (now.AddSeconds(miningTime) > DateTime.Now)
            {
                var currentMineRate = (DateTime.Now - now).TotalSeconds / miningTime;
                progressBarView.SetProgress((float) currentMineRate);
                
                //採掘を続けるかどうかを判定する
                if (!isContinueMining())
                {
                    break;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }
    }
}