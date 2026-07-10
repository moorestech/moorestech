# 結果報告: プレイテストスキルのモデル別精度評価（Opus/Sonnet）

実施: 2026-07-07 15:23〜19:00 / ブランチ: `feature/playtest-stabilization`
計画: `2026-07-07-playtest-skill-model-eval-handoff.md`（本レポートはその実施結果）
ハーネス: `run-skill-live-trial`（tmux実セッション・fresh context・`--dangerously-skip-permissions`）
評価対象: 再構築版 `unity-playmode-recorded-playtest`（ルーターSKILL.md + references/ 7ファイル、8b93e7d22 を本worktreeへ同期=1760df13e）
判定: 各トライアルごとに独立の fresh evaluator（general-purpose subagent）が一次情報（result.json・transcript.jsonl・スクショ目視・録画ffprobe・コード実読）で採点。自称は信用しない設計

## 結論（1行）

**再構築版スキルは Opus / Sonnet どちらでも Fable 相当の精度で機能する。6/6 トライアル合格（goal適合 90〜96）、全レベルで誤ルート実質0・PASS詐称0、L2/L3 では実プロダクトバグ2件の発見（うち1件は修正まで）に到達した。**

## 総合マトリクス

| Trial | モデル | Level | 判定 | goal適合 | asserts | attempts | 介入 | 誤ルート | wall-clock |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Opus 4.8 | L1 手順追従 | ✅ | 90 | 8/8 | 2 | 0 | 実質0（sleep1回） | 6分 |
| 2 | Sonnet 5 | L1 手順追従 | ✅ | 90 | 8/8 | 3 | 0 | 実質0 | 11分 |
| 3 | Opus 4.8 | L2 応用作文 | ✅ | 94 | 11/11 | 5 | 0 | 実質0 | 38分 |
| 4 | Sonnet 5 | L2 応用作文 | ✅ | 90 | 11/11 | 3 | 0 | 実質0（sleep軽微） | 30分 |
| 5 | Opus 4.8 | L3 未知領域 | ✅ | 93 | FAIL正直申告(0/1) | 2 | 0 | 0 | 37分 |
| 6 | Sonnet 5 | L3 未知領域 | ✅ | 96 | 5/5（バグ修正後） | 2 | 1(環境※) | 0 | 44分※ |

※Trial 6 の介入1回は Claude 使用量上限（プラン資源切れ）→解除後の「続行せよ」nudge のみ。タスク内容のヒントなし（evaluator が本文確認済み）。wall-clock には上限停止 約25分を含む。
全トライアルで requested_model = actual_model を transcript jq で機械検証済み。

## Fable 基準との比較（申し送りの計測指標）

| 指標 | Fable実績（基準） | Opus | Sonnet |
|---|---|---|---|
| 完走まで人間の介入回数 | L2相当=0 / L3相当=1 | L1/L2/L3 すべて **0** | L1/L2=0、L3=1（**環境要因のみ**） |
| 誤ルート（OS入力・固定sleep多用・共有masterHEAD・ポート見落とし） | 0 | **0**（軽微sleep1のみ） | **0** |
| 全PASS到達までの試行回数 | belt-line-via-ui=3 / gear-chain-pole=3 | L1=2 / L2=5 / L3=2 | L1=3 / L2=3 / L3=2 |
| 実バグ/自ミスの切り分け精度 | 実バグ2件を正しく修正に倒した | 誤帰責0・転嫁0（両評価で確認） | 誤帰責0・転嫁0（同左） |

**結論: 両モデルとも Fable 基準と同等以上。** スキルの「該当referenceだけ読めば実行できる」設計目標は達成と判定する。

## レベル別の所見

### L1（手順追従・belt-line-via-ui.cs 実行）
- 両モデル PASS（goal 90）。興味深いのは初回失敗への**リカバリ経路のモデル差**:
  Opus=文書化部品で手動boot→inject を即興再構成（速いが one-shot 経路から離脱）、
  Sonnet=フルストップ→フレッシュブート（手順回帰型。ただし途中で「再実行時のPlayMode停止」を1回スキップして自招失敗）
- 両者とも初回に **boot自身のドメインリロードが UnityMcpSettings.json を .bak 化する未文書の環境race** に遭遇（→referenceへ追記済み）

### L2（応用作文・石窯精錬ライン）
- 課題に書かなかった3つの仕掛け（燃料併投入・3x2x3コネクタoffset・requiredPower:0）を**両モデルとも実行前の master 読解で全件先取り**（Opus=原木、Sonnet=木炭と別解）
- 接続数assert の投入前先行・Until条件待機・UI経路の徹底（PlaceBlockDirect への黙示フォールバック0）を両者達成
- **両モデルが独立に同一の実バグへ到達**（下記バグ①）— スキル経由のバグ検出が再現性を持つ強い証拠

### L3（未知領域・レール橋脚UI敷設 — 申し送りの試金石）
- 申し送りに残した `TrainRailPlaceService.cs:69` の BlockId 未設定疑いに、**両モデルとも独立に自力到達**（Step 0 探索→実行失敗→ログ的打ち→コールチェーン全トレース）
- Opus: FAIL+特定で正直申告（サーバー直設置の対照実験つき）。Sonnet: 「修正は任意」条項で1行修正を適用→元課題（2本設置+クリック結線+RailNode接続のサーバー側確認）を完遂、negative control つきの assert 設計
- **「本物のバグ発見まで到達できるか」という試金石は両モデルクリア**

## 確定した実プロダクトバグ（fresh evaluator がコード実読で独立検証）

1. **電気ブロックがビルドメニューの建設コスト経路で設置不能**（未修正・要チケット化）
   - `ElectricWireAutoConnectPreview.TrySelectWire` 先頭の `virtualCounts.GetValueOrDefault(placingItemId) < 1` ガードがブロックアイテム自体の所持を要求。非電気ブロックは早期 return true で影響なし。サーバー `PlaceBlockProtocol` は RequiredItems しか検証しないクライアント/サーバー非対称
   - 旧アイテム駆動設置の残骸が建設コストモデル移行で取りこぼされた形。Trial 3/4 が独立到達
2. **レール橋脚がUI経路で設置不能**（**修正済み: 05a8cb1a8**）
   - `TrainRailPlaceService.CreatePlaceInfo()` の `PlaceInfo.BlockId` 未設定→`GetBlockMaster(0)` が毎フレーム例外→設置プロトコル不到達。歯車ポール e6c5b388e と同型の「セル毎BlockId化への追従漏れ」3例目
   - Sonnet L3 が修正を適用、回帰シナリオ `train-rail-connect-via-ui.cs`（5/5 assert）同梱
   - **推奨**: PlaceInfo を手組みしている箇所の BlockId 設定漏れ横断監査（同型3例目のため）

## スキルへ反映済みの改善（このセッションでコミット）

| reference | 追記内容 | 根拠トライアル |
|---|---|---|
| run-scenario.md | 再実行時も毎回 PlayMode stop 必須（使い回しはNREスパム+タイムアウト） | Trial 2 |
| run-scenario.md / troubleshooting.md | UnityMcpSettings.json の .bak 化は boot 自身のドメインリロードでも発生（run-tests非並走でも） | Trial 1/2 |
| place-blocks-via-ui.md | 高さのあるブロックの隣接セルへ後から設置すると天面レイキャストで上に誤設置→背の高いブロックは最後 | Trial 3/4 |
| place-blocks-via-ui.md | 電気系ブロックの設置不能バグ①と GiveItem workaround | Trial 3/4 |

※メインチェックアウト側 `/Users/katsumi/moorestech/.claude/skills/` への逆同期は未実施（本worktreeのコミットが正。次回メイン側で同期すること）

## 評価手法のメモ（再現する人向け）

- 直列実行必須（ポート11564競合）。トライアル間は「成果物をWORK_DIRへアーカイブ→シナリオ原本削除→revisions.json revert→PlayMode停止確認」でベースライン復元（後続トライアルへのカンニング防止）
- 課題の仕掛け（燃料要求など）は出題前に**評価者側がコードで解けることを確認**しておく（石窯 requiredPower:0 は GetSubTicks の requiredPower==0 分岐まで確認した）
- 座標帯指定は足場範囲（x,z∈[-25,+25]）内に収める（当初案の20〜40は足場外ではみ出す設計ミスだった）
- fresh evaluator には「自称を信じず一次情報で」と明示し、バグ主張はコード実読で各リンク検証させる。integrity チェック（Direct へ黙ってフォールバックしていないか・assert を弱めるハックがないか）が特に有効だった
- Claude 使用量上限が長丁場評価の現実的リスク（Trial 6 で発生）。トライアル成果には影響しない形で復旧可能（nudge 内容を透明に記録）

## 成果物の所在

- 各トライアル: `.mso/live-trial/20260707-*-{opus,sonnet}-l{1,2,3}-playtest/`（task.md / report.md / workflow.md / out/status.json / transcript.jsonl / pane.txt / artifacts/）
- 録画 mp4 は git 管理外（`.mso/**/recording.mp4` を .gitignore 追加）。ローカルの同パスに保全
- プロダクト修正: 05a8cb1a8（レールBlockId + 回帰シナリオ）

## 結果ビューアー

- URL: http://localhost:4983 （対象: `.mso/live-trial/` 全トライアル）
- 再起動: `node /Users/katsumi/.claude/skills/run-skill-live-trial/templates/result-viewer/server.mjs --dir /Users/katsumi/moorestech-worktrees/playtest/.mso/live-trial --port 4983`
- 停止: `lsof -i :4983` で PID 確認後 kill
