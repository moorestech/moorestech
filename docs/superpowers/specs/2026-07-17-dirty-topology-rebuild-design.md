# 電力・歯車トポロジのdirty全体再構築 設計

日付: 2026-07-17 / ブランチ: feature/DirtyTopologyRebuild（feature/ElectricTickUnification6 の上のstacked PR）
素材: feature/ElectricTickUnification3 の `a2cefc9da4`（dirty全体再構築）＋`a2a0d3e8d0`（歯車チェーンのdirty通知統一）を、破壊予約システム抜きで適応する。
仕様書: `C:\Users\5080\Desktop\fab\仕様.txt` 2.1（tick順序①〜④）・3.2（トポロジ変更のtick内反映）に対応。

## 目的

PR1（電力tick一元化）で導入したコマンドバッファ方式（FIFOキューをtick先頭でflush）を、
「登録集合＋dirtyフラグ→tick先頭でO(V+E)一括再構築→適用mapの原子交換」へ置き換える。
電力と歯車を同一パターンに統一し、tick順序（電力網再構築→歯車網再構築→電力tick→歯車tick）を
1ファイルのコードとして明示する。

根拠: 最終的な連結性は登録集合の最終状態だけで決まるため、変更クエリを順番に適用する意味がない。
キュー自体を廃止し、変更のないtickはゼロコスト・あるtickはO(V+E)一回とする。
追加のみの増分マージ最適化は行わない（必要になってから）。

## 設計

### 1. 電力データストア（ElectricWireNetworkDatastore）

- コマンドバッファ（ElectricWireTopologyCommand とキュー処理）を削除する。
- 保持するのは「登録集合（BlockInstanceId→IElectricWireConnector の辞書）＋dirtyフラグ」のみ。
- AddConnector / RemoveConnector / MarkTopologyDirty は登録集合を即時更新してdirtyを立てるだけ。
- 接続集合を変更するcomponentメソッド（TryAddWireConnection等）自身がMarkTopologyDirtyを呼ぶ。
  呼び出し元utilにdirty化責務を残さない（レビュー裁定 2026-07-17）。
- RebuildIfDirty(): dirtyなら登録集合の最終状態から ElectricWireTopologyMap.Build で全再構築し、
  完成後に適用mapを原子交換（構築成功まで旧mapに触らない）。旧mapは Destroy する。
- tick中の参照（TryGetEnergySegment / GetSegments）は常に適用map経由。
  tick途中でセグメント所属・列挙が変化しないことを保証する（仕様3.2）。

### 2. 歯車データストア（GearNetworkDatastore）

- 電力と同じ「登録集合＋dirty→一括再構築→原子交換」へ移行する。
- staticメソッド（_instance経由）は全廃し、IGearNetworkDatastore interfaceを新設して
  電力側と同じDI注入（ServerContext.GetService経由）へ統一する（レビュー裁定 2026-07-17）。
  RebuildIfDirtyは電力側と同様interfaceに載せず具象のみ。
- GearConnectedComponentFinder によるO(V+E)連結成分探索、GearNetworkTopologyBuildResult を導入。
- GearTopologyMutation（増分ミューテーションのコマンド）は削除する。
- 歯車チェーン（GearChainPole）の接続変更も MarkTopologyDirty への通知に一本化する（a2a0d3e8d0）。

### 3. tick構成 — ServerTickUpdater 新設

```
ServerTickUpdater.Update():
    電力網 RebuildIfDirty
    歯車網 RebuildIfDirty
    電力tick（ElectricTickUpdater.Update）
    歯車tick（GearTickUpdater.Update）
```

- 仕様2.1の①〜④がこの1ファイルで読める。将来のFluid/Trainはここに行を足す。
- DIコンテナ（MoorestechServerDIContainerGenerator）は
  `GameUpdater.AdditionalUpdates.Add(ServerTickUpdater.Update)` の1行登録のみ。DI構造は分割しない。
- ElectricTickUpdater からflush呼び出しが消え、需給確定（SettleTick）＋後処理（RunPostTickProcess）
  だけの純粋な計算になる。GearTickUpdater も同様に再構築を持たない。
- 置き場所は電力・歯車の両asmdefを参照できる層（Server.Boot想定、asmdef制約で確定）。

### 4. 破壊の扱い（スコープ判断）

- 破壊は即時のまま（予約なし）。破壊→登録集合から即除去＋dirty→次tick先頭で網に反映。
- tick中の残存は既存のIsDestroyガードで凌ぐ（PR1と同じ運用）。
- フォールバック（ユーザー承認済み）: 即時破壊起因でテストが落ちて解決不能な場合のみ、
  破壊処理をtick最後尾へ移す方式を検討する。

## スコープ外（明示）

- 破壊予約＋tick末尾一括確定（仕様2.1⑥・3.3）→ 後続PR
- GameUpdater.TickEndUpdates の追加 → 予約系と一緒に後続PR
- セーブ協調（仕様2.1⑦）→ 後続PR
- branch3 の MoorestechServerTickRegistration.cs（DI分割の一部）→ 使わない（ユーザー却下済み）
- Fluid/Train/BlockSystem のObservable購読からの移行 → 必要になったときの別PR

## テスト

- branch3 のテスト変更を適応する: GearNetworkTestDirtyRebuild（新設）、
  電力系の接続/切断/セーブロード/マルチセグメントテストのdirty再構築対応、
  ElectricNetworkReflectionTestUtil / GearNetworkDatastoreReflectionTestUtil。
- 予約システム前提のテスト変更が混ざっていれば除去する。
- 既存893件＋新規が全てPASSするまで完了としない。

## 検証済みの前提

- branch3 `a2cefc9da4` は50ファイル・+868/-925行。うち MoorestechServerTickRegistration.cs（34行変更）は
  DI分割前提のため取り込まず、ServerTickUpdater 新設で代替する（本設計の差分点）。
- 仕様6.2の「満充電の浮動小数点問題・検討中」はPR1で解決済み（需要=不足分申告＋トレランス1e-4）。
