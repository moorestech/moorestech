using System;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Stage.ScaleReading
{
    /// <summary>
    /// パネルの倍率を毎フレーム読み、変わったときだけ流す。外部の状態（OS の窓の寸法）を見る唯一の点で、ゲーム状態の毎 tick の同値判定ではない（設計書 A-41）
    /// Reads the panel scale every frame and publishes only on change; the single point watching external state (OS window size), not a per-tick game-state check (design A-41)
    /// </summary>
    public sealed class StageScaleWatch
    {
        private readonly VisualElement _root;
        private readonly ReactiveProperty<float> _scale = new(1f);
        private readonly StageScaleReadGuard _readGuard = new();

        public IReadOnlyReactiveProperty<float> Scale => _scale;

        public StageScaleWatch(VisualElement root)
        {
            _root = root;
        }

        public void Start()
        {
            // パネル未接続では倍率も時計も無いので起動の段階で止める
            // Without an attached panel there is neither scale nor clock, so stop at startup
            if (_root.panel == null) throw new InvalidOperationException("[StageScale] root is not attached to a panel");
            ReadOnce();

            // Unity に解像度変更の事象は無く、16:9 どうしの変更では GeometryChangedEvent も来ないので毎フレーム読む
            // Unity has no resolution-change event and a 16:9-to-16:9 change raises no GeometryChangedEvent, so read every frame
            _root.schedule.Execute(ReadOnce).Every(0);
        }

        private void ReadOnce()
        {
            // 読めない状態は毎フレーム続きうるので、入ったとき 1 回だけ開発者へ知らせ、回復で旗を戻す
            // An unreadable state can last every frame, so tell the developer once on entry and clear the flag on recovery
            if (!_readGuard.TryRead(_root.panel, "StageScale", $"keeping {_scale.Value:0.###}", out var scale)) return;
            if (Mathf.Approximately(_scale.Value, scale)) return;

            // 解像度の変更は稀なので、変化を開発者のログに残す（録画の確認にも使う）
            // Resolution changes are rare, so leave each change in the developer log (also used to check recordings)
            Debug.Log($"[StageScale] stage scale changed {_scale.Value:0.###} -> {scale:0.###}");
            _scale.Value = scale;
        }
    }
}
