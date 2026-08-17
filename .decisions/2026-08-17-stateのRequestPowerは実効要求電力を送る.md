決定: CommonMachineBlockStateDetail の RequestPower の意味を基礎要求値から実効要求電力（待機中×idlePowerRate、稼働中×モジュール倍率、停止中0）へ変更する（案A）。充足率はクライアント側で current/request を再計算する現行方式を維持し、ワイヤは実消費と実効要求の2値のみ。

棄却案: 案B＝既存フィールド温存で EffectiveRequestPower を別フィールド追加（意味の違う2つの要求値がワイヤに並ぶ）。

理由: 全消費者（電気・歯車・ポンプ等）で充足率の意味論が一貫し、派生 PowerRate（アニメ速度）も自然に追従する。フォールバックで吸収せず正しいモデルへ一括更新する設計原則に沿う。

リンク: docs/adr/0010-machine-power-display-as-satisfaction-rate.md / [[2026-08-17-電力表示は充足率と稼働状態ラベルに分離する]]
