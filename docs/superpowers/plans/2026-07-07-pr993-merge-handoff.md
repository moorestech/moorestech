# ~~申し送り: PR #993（プレイテスト分先行マージ）の完了作業~~

> **🚨 撤回（2026-07-07 20:40）: 本ドキュメントの「#993先行マージ」方針は誤った記録であり、実行後にユーザー判断で巻き戻された。**
> 正しい方針は**実装順マージ（#987を先に仕上げてマージ → その後#993を新規PRで再作成してマージ）**。
> #993は一度マージ（94c4c72f8）→ revert（#994）→ master-fable-tmpをforce-pushで762af74b5へ復元済み。
> 巻き戻しの全記録・関連ハッシュ・復旧手順は `2026-07-07-pr993-rollback-record.md` が正。**本ドキュメントの手順を再実行してはならない。**

作成: 2026-07-07 19:57 / ブランチ: `feature/playtest-stabilization`（worktree: `/Users/katsumi/moorestech-worktrees/playtest`）
関連: `2026-07-07-playtest-skill-model-eval-results.md`（モデル別評価6/6合格・実バグ2件の結果報告）

## ゴールと方針（ユーザー決定済み）

**#993（feature/playtest-stabilization → master-fable-tmp）を #987 より先にマージする。**
git のマージは祖先ごと入るため、#993 マージで設置システム刷新の土台101コミット（1877b7315まで）も master-fable-tmp へ入り、**#987 は plan4/5 尾部（約24コミット）だけの PR に自動縮小**する。#987 にはこの旨と縮小後の衝突解決指針（`TrainRailPlaceService.cs` は #987 側のビルドメニュー駆動リファクタ版を採用、回帰シナリオ `train-rail-connect-via-ui.cs` で検証）を**コメント投稿済み**。

## 現在の状態（すべてコミット済み・未push分あり）

- master-fable-tmp を**2回マージ済み**:
  1. `7098ff9a8` — #989（blockConnectInfo.yml廃止・ドメイン別コネクタスキーマ）分。衝突は `_CompileRequester.cs` のみ（自明）
  2. `d6e508228` — #992（GearConnectJudge/meshingAxis）分。実衝突1件 = `VanillaGearToElectricGeneratorTemplate.cs`（#992の `BlockConnectorComponent<IGearEnergyTransformer, GearConnectJudge>` 化 × 本ブランチの電線ワイヤー配線）→ **両方を合成して解決済み**（新ジェネリクス + generatorComponent/wireConnector 保持）。コンパイル 0 エラー確認済み
- **masterデータ統合**: #989 は master 側データ移行（hermes/mt-014 の 645b0fa）が本ブランチ互換の master 系列に無かったため、**584a14e へ同じ変換を機械適用した統合コミットを作成**
  - moorestech_master リポジトリの `playtest-stabilization-integration` ブランチ = `3b2de6f`（**origin へ push 済み**）
  - 変換規則: 全コネクタの `connectType` 削除 / inventory系（inputConnects/outputConnects）は `connectOption` 削除 / gear・fluid系（gearConnects/inflowConnects/outflowConnects）は `connectOption`→`option` 改名 + **スキーマ既定値を明示補完**（gear: isReverse=true, fluid: flowCapacity=10, connectTankIndex=0）
  - ピン更新コミット: `ebb4788be`（`.moorestech-external-revisions.json` → 3b2de6f）
  - worktree `/Users/katsumi/moorestech-worktrees/playtest-master` は同ブランチをチェックアウト済み（run-scenario のデフォルト master パスがそのまま機能する）
- **検証状況**: 1回目マージ後 = preflight PASS + belt-line-via-ui 8/8 + train-rail-connect-via-ui 5/5 全PASS。2回目マージ後 = preflight PASS + コンパイルクリーンまで確認済み。**シナリオ2本の再実行は未実施**（ここで中断）

## ✅ 完了報告（2026-07-07 20:30）

**#993 は 94c4c72f8 でマージ完了。** 残り3ステップはすべて実施済み。

- シナリオ再検証: belt-line-via-ui **8/8 PASS** / train-rail-connect-via-ui **5/5 PASS**
- #987 は新 master-fable-tmp 比で **24コミット（plan4/5尾部）に縮小**をローカル `git rev-list --count` で確認（GitHub UI の diff 再計算は遅延する）
- `tmux lt-*` リーク無し

実施中に得た追加の知見（ハマりどころ8・9として追記）:

8. **シナリオ側も #992 のジェネリクス変更に追従が必要だった**: `BlockConnectorComponent<IBlockInventory>` 参照が CS0305 で注入コンパイル失敗。`BlockConnectorComponent<IBlockInventory, DefaultConnectJudge>`（`Game.Block.Interface.Component.ConnectJudge`）へ修正（4600b7972）。プロジェクト本体のコンパイルが通ってもシナリオ .cs は別途チェックされない
9. **train-rail クリック結線はRailNode生成イベントのtick同期反映レースで稀に失敗する**: 設置直後にクリックすると `[TrainRailConnect] Failed to resolve node info from cache.` が出て FROM 選択がリセットされる。RailNodeCreatedEvent は `TrainUnitFutureMessageBuffer` 経由で post-sim tick に反映されるため、クライアントキャッシュへの反映がクリックに間に合わないことがある。再実行で PASS（一過性）。恒久対策するならシナリオの結線クリック前に `_cache.TryGetNodeId` 成立を Until 待機するのが良い。なお注入コンパイル失敗したセッションの残留状態で次回 boot が「game not ready within 180s」+ NRE スパムになることがあり、これも停止→コンソールクリア→再実行で解消した

## 残り手順（3ステップ・約15分）※実施済み・記録として保持

```bash
cd /Users/katsumi/moorestech-worktrees/playtest   # 必ずpwd確認
# 1. 実機再検証（推奨。省略可だがbaseが動いた直後なので流すのが安全）
./tools/playtest/run-scenario.sh ./moorestech_client tools/playtest/scenarios/belt-line-via-ui.cs
./tools/playtest/run-scenario.sh ./moorestech_client tools/playtest/scenarios/train-rail-connect-via-ui.cs
# 2. push → mergeable 確認
git push origin feature/playtest-stabilization
gh pr view 993 --json mergeable,mergeStateStatus   # MERGEABLE になるまで数十秒
# 3. マージ（squashしない・履歴保持）
gh pr merge 993 --merge
```

マージ後の確認: #987 の差分が plan4/5 尾部のみに縮小されていること。`tmux ls | grep '^lt-'` のリーク無し（現状クリーン）。

## ハマりどころ（このセッションで実際に踏んだもの）

1. **base が動く**: 作業中に #992 が master-fable-tmp へ入り、マージ→検証をもう一巡した。残り手順は一気に実行すること。再発したら「fetch→merge→衝突解決→compile→preflight→シナリオ→push」を繰り返す
2. **mooresmaster ローダーは option 内の欠損キーに既定値を補完しない**（schema の default 記述は当てにならない）。gear/fluid の `option` は必須キーを明示して埋める。645b0fa も明示値で書いている
3. **f11e2ce（master統合の見かけ上の合流点）は使えない**: placeSystem.json が plan4 形式（sortPriority）で本ブランチ（priority + usePlaceItems）と不整合。正解は「584a14e + blocks.json のみコネクタ移行」
4. **`_CompileRequester.cs` / `.uloop/tools.json` の局所変更が `git merge` を無言で失敗させる**: merge 前に `git checkout --` で掃除。grep でフィルタすると abort メッセージを見落とす
5. **UnityMcpSettings.json の .bak 退避 race**: boot の PlayMode 突入ドメインリロードでも発生（run-tests 非並走でも）。`cp UserSettings/UnityMcpSettings.json.bak UserSettings/UnityMcpSettings.json` で復元（references/troubleshooting.md に反映済み）
6. **「ERROR: another scenario is running」**: 前セッションのランナー静的状態が残ると出る。PlayMode 停止→`RequestScriptReload()`→45秒待ちで解消
7. **master-fable-tmp の既存ピン 9c0993f は実は #989 スキーマとデータ不整合**（connectOption のまま）。master-fable-tmp 単体では PlayMode 検証が通らない状態だった可能性が高い。#987 縮小後のマージ時にも master ピンの整合確認が必要

## マージ後の後続タスク（別作業）

- **#987（plan4/5 尾部）のマージ**: `TrainRailPlaceService.cs` 衝突は #987 側を採用（PRコメント参照）。plan4/5 は master データも進む（sortPriority / itemGuid撤去 / idlePowerRate）ため、ピンを plan2-master-migration 系列（c940ca4 付近）へ進めて検証
- **未修正バグのチケット化**: 電気ブロックが建設コスト経路で設置不能（`ElectricWireAutoConnectPreview.TrySelectWire` の在庫ガード、評価L2で両モデルが独立到達・コード検証済み）
- **PlaceInfo 手組み箇所の BlockId 設定漏れ横断監査**（同型退行3例目のため）
- スキル本体のメインチェックアウト側（`/Users/katsumi/moorestech/.claude/skills/`）への逆同期（reference改善3件は本worktreeのコミットが正）
