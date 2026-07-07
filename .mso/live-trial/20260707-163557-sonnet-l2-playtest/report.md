# live-trial report: Trial 4 — Sonnet L2（応用作文: 石窯精錬ライン）

## 対象
- **skill**: unity-playmode-recorded-playtest
- **args**: Trial 3（Opus L2）と同一課題（石窯精錬ライン・UI経路・座標帯 -20〜-5・粉5→インゴット5全数検証）。Opus の成果物はアーカイブ撤去済みでカンニング不能
- **課題の仕掛け（非提示）**: ①燃料併投入（粉1+原木5 or 木炭3） ②3x2x3コネクタoffset ③requiredPower:0

## model 検証（機械 gate）
- requested_model: `claude-sonnet-5` / actual_model: `claude-sonnet-5` → **一致 ✅**

## timeline
- boot: READY 3s（SESSION_ID: 4ff8ac3c-8e39-4225-9b25-9df4914102dd）
- poll: DONE 1800s（exit 0、via jsonl）
- wall-clock: 約30分

## 介入
- nudge_count: **0** / gate 応答: **0**（完全自走）

## 成果物
- 完了マーカー: `out/status.json` — PASS, 11/11 asserts, attempts=3, found_product_bug 2件
- シナリオ: `artifacts/smelting-line-via-ui.cs`
- result.json / スクショ4枚 / 録画（114s, 1280x720）: `artifacts/playtest-results-20260707_170156/smelting-line-via-ui/`
- transcript: `transcript.jsonl` / pane: `pane.txt`
- git 副作用: トライアル自身が revisions.json を revert（模範的）。.cs 変更ゼロ

## goal 判定（fresh evaluator）
- **goal適合: 90 / 合格**
- 課題要求5項目すべて達成。接続assert先行・Until条件待機・固定sleepなし
- **integrity**: 4ブロックすべて PlaceBlockViaUi。Direct フォールバックなし。workaround（石窯アイテム1個付与）はUI経路を保持（「HoldingItem place mode」寄りになる小濁りの注記あり）
- **仕掛け対応: 3件すべて事前 master 確認で先取り**（シナリオWrite前に blocks.json/machineRecipes.json 照会＋コード読了。木炭ルートを選択= Opus の原木と別解）
- 誤ルート: 実質0（オーケストレーション用 shell sleep のみ軽微1）
- attempts=3 分解: ①製品バグ遭遇（正しく帰属）②カメラ埋没の見栄え修正（自己）③成功。切り分け正確・PASS詐称なし

## found_product_bug 判定（evaluator 独立検証）
1. **TrySelectWire ガード → 実バグ確定**（Trial 3 と同一バグへ独立到達）。さらに Sonnet は「サーバー側は電線接続0件なら無条件許可」というクライアント/サーバー乖離の因果まで正確に特定（EvaluateAutoConnect :36-37 で裏取り済み）
2. **屋根コライダー誤設置（y=34）→ 実再現・ただし「未文書の設置順依存な操作特性」寄り**。product bug ラベルはやや過大（Opus は同現象を自己ミス=設置順で処理。分類は Opus が適切、文書化価値の指摘は Sonnet が先）

## 総合判定
**✅ 合格**（起動✅ / 完走・自走✅ / goal 合格）

## Trial 3（Opus L2）との比較メモ
- 両者 PASS・11/11・介入0・仕掛け3件事前先取り・同一実バグへ独立到達（スキル再現性の強い証拠）
- attempts: Sonnet 3 < Opus 5（Sonnet はコンパイルエラー・unlock 回り道なし）
- wall-clock: Sonnet 30分 < Opus 38分
- バグ報告の質: ①の因果深度は Sonnet がやや上（サーバー側許可条件まで特定）。②の分類は Opus が適切（自己ミス扱い）で Sonnet は過大ラベル

## 推奨アクション
- place-blocks-via-ui.md へ「高さのあるブロックの隣接セルへ後から設置するとレイキャストが天面ヒットして上に載る→背の高いブロックは最後に置く」を追記（両モデルが独立に踏んだ＝reference の穴）
