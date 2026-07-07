# live-trial report: Trial 3 — Opus L2（応用作文: 石窯精錬ライン）

## 対象
- **skill**: unity-playmode-recorded-playtest
- **args**（task.md 引用）: 「新規プレイテストシナリオを作成して実行・検証して: ブロック設置はすべてUI経路（…）ベルトコンベアで鉄鉱石の粉を石窯へ搬入し、石窯の精錬出力を別のベルトコンベア経由で木のコンベアチェストへ収納するラインを組む。鉄鉱石の粉5個を投入し、精錬でできた鉄インゴット5個が全数チェストへ届くこと（紛失・重複なし）を検証する。設置座標は x, z とも -20〜-5 の帯。シナリオファイルは tools/playtest/scenarios/ に新規作成」
- **課題の仕掛け（トライアル非提示）**: ①石窯レシピは燃料併投入（粉1+原木5→インゴット1） ②3x2x3マルチブロックのコネクタoffset ③requiredPower:0で電力不要

## model 検証（機械 gate）
- requested_model: `claude-opus-4-8` / actual_model: `claude-opus-4-8` → **一致 ✅**

## timeline
- boot: READY 3s（SESSION_ID: 938dd808-e39b-44da-a6a2-2fa4051b3718）
- poll: DONE 2280s（exit 0、via jsonl）
- wall-clock: 約38分

## 介入
- nudge_count: **0** / gate 応答: **0**（完全自走）

## 成果物
- 完了マーカー: `out/status.json` — PASS, 11/11 asserts, attempts=5, found_product_bug あり
- シナリオ: `artifacts/smelting-line-via-ui.cs`（実行時は tools/playtest/scenarios/ に作成、評価後ハーネスがアーカイブ移動）
- result.json / スクショ5枚 / 録画（109.3s, 6.4MB）: `artifacts/playtest-results-20260707_162954/smelting-line-via-ui/`
- transcript: `transcript.jsonl` / pane: `pane.txt`
- git 副作用: revisions.json M（ハーネスがrevert）+ 新規シナリオ（アーカイブ済み）。.cs 変更ゼロ

## goal 判定（fresh evaluator）
- **goal適合: 94 / 合格**
- 課題要求6項目すべて達成（UI経路4ブロック・座標帯内・5個全数到達・接続assert先行・新規ファイル）
- **integrity**: PlaceBlockDirect への黙示フォールバックなし。バグ回避の `GiveItem("石窯",1)` も設置クリックは PlaceBlockViaUi のままで、サーバーは建設コストしか消費しないことまで evaluator が確認
- **仕掛け対応: 3件すべて事前 master 読解で先取り**（Step 0 サブエージェント探索で machineRecipes/blocks.json/GetSubTicks を投入前に解析）— 実行失敗からの後追いでない最良経路
- 誤ルート: 実質0
- attempts=5 分解: ①using欠落（自己ミス）②unlock IF参照（環境/自己）③**製品バグ**（石窯赤プレビュー）④設置順ミス（傾斜ベルト化）⑤成功。切り分け正確・転嫁なし

## 発見した実プロダクトバグ（evaluator がコード読みで確定）
**電気ブロック（電線コネクタ持ち）はビルドメニューの建設コスト経路だけでは設置不能**
- `ElectricWireAutoConnectPreview.cs` `TrySelectWire` 先頭ガード: `if (virtualCounts.GetValueOrDefault(placingItemId) < 1) return false;` がブロックアイテム自体の所持を要求
- 非電気ブロックは早期 return true で影響なし / サーバー `PlaceBlockProtocol` は RequiredItems（建設コスト）しか検証・消費しない → **クライアント電気系プレビューだけの非対称ガード**
- 旧アイテム駆動設置の残骸が建設コストモデル移行で取りこぼされた形。実プレイで「アンロック+建設素材所持でも電気ブロックだけ置けない」UXバグ

## 総合判定
**✅ 合格**（起動✅ / 完走・自走✅ / goal 合格）— Fable基準（L2相当=介入0）と同等以上。実バグ発見までL2段階で到達

## 推奨アクション
- 発見バグの修正チケット化（TrySelectWire のガードを建設コスト判定に揃える）
- place-blocks-via-ui.md に「多段ブロック隣接ベルトはレイキャスト天面ヒットで傾斜化する→ベルト先敷き」の追記候補（attempt 4 の失敗クラス）
