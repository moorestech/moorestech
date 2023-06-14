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

        /// <summary>
        /// 採掘を開始する
        /// キャンセルしてもexceptionは発生しません
        /// </summary>
        /// <param name="miningTime">採掘時間 秒</param>
        /// <param name="cancellationToken">キャンセラレーショントークン</param>
        /// <exception cref="InvalidOperationException">採掘中にこのメソッドを呼び出した場合、この例外がスローされます。</exception>
        /// <returns>採掘に成功したらtrue 途中でキャンセルされたらfalse</returns>
        public async UniTask<bool> StartMining(float miningTime,CancellationToken cancellationToken)
        {
            if (IsMining)
            {
                throw new InvalidOperationException("採掘中に他の箇所から採掘開始を呼び出されました。IsMiningフラグを確認し、採掘中でないことを確認してください");
            }
            
            IsMining = true;
            try
            {
                var now = DateTime.Now;
                //UIの更新のためにwhileで回す
                while (now.AddSeconds(miningTime) > DateTime.Now)
                {
                    var currentMineRate = (DateTime.Now - now).TotalSeconds / miningTime;
                    progressBarView.SetProgress((float) currentMineRate);
                    
                    await UniTask.Yield(PlayerLoopTiming.Update,cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            finally
            {
                IsMining = false;
            }
            
            return true;
        }
    }
}