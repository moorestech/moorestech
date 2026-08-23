# mapObjectの起動生成は近傍先行＋時間予算分散とし、遠方はゲーム開始後に後着させる

## Context

自動生成ワールドの実データはmapObject約79,000個（seed-196実測）に対し、
`MapObjectGameObjectDatastore`は2011個規模を前提に「100個Instantiateごとに1フレームYield」で全量生成し、
`InitialEventApplyWaiter`が全量完了を待ってからゲームを開始するため、起動が十数秒〜数十秒延びていた。

## Decision

- 起動時に全layoutを`InitialHandshakeResponse.PlayerPos`からの距離で一度だけソートし、
  半径150m以内（近傍）だけを生成して`IInitialEventApplyWaitTarget`の待機を解除する。
  残りは同ソート順（近い順）のままゲーム開始後にバックグラウンドで生成し続ける。
  出所: ユーザー裁定 2026-08-23（中心「PlayerPos」・半径「150m」・順序「距離順」）
- フレーム分散は個数固定を廃止し時間予算制にする。近傍（ローディング中）は16ms/フレーム、
  後着（ゲーム開始後）は4ms/フレームを使い切るまでInstantiateする。両値は定数。
  出所: ユーザー裁定 2026-08-23（誤選択「全期間16ms」を直後に「推奨で」と訂正し二段予算を採択）
- 未生成個体宛の破壊/HPイベントは捨てず、instanceId単位の最新状態台帳に保留し、
  Instantiate時にhandshakeスナップショットより優先して適用する。
  出所: ユーザー裁定 2026-08-23「instanceId単位の最新状態を保留し生成時に上書き」
- 全量生成完了の待機を正規API（`WaitForAllInstantiatedAsync`）として公開し、
  既存テスト（MapObjectRotationTest等）とプレイテストDSLはこれを待つ。
  出所: ユーザー裁定 2026-08-23「全量完了の待機を正規APIとして公開」
- 実装は`feature/nearest-search-kd-tree`ブランチ起点。後着生成は`MapObjectNearestSearcher.Register`→dirty→
  次探索時再構築の既存経路にそのまま乗せ、索引側の設計変更はしない。
  出所: ユーザー裁定 2026-08-23「k-d treeブランチを起点に積む」
- 露頭（`OutcropGameObjectDatastore`・1,775件）は対象外で現状維持。
  出所: ユーザー裁定 2026-08-23「露頭は対象外・現状維持」
- `MapObjectGameObject.Initialize`のGetComponentsInChildren走査は計測し、支配的なら是正する。
  出所: ユーザー裁定 2026-08-23「含める（計測して支配的なら直す）」
  **状態: 未実施のまま本タスクから切り離した（bd moorestech-4z88.3）。** 計測にPlayMode実行が必要だが
  Editor固着で起動できなかったため。ユーザー裁定 2026-08-23「Task 6・7へ進む」。
  なお同走査は非活性下生成への対策として `includeInactive:true` へ変更済みで、走査コスト自体は据え置き。
- 時間予算の計測にStopwatch等の実時間APIを使う。「実時間API禁止」規約はサーバーのゲームロジック
  （GameUpdaterティック）対象であり、クライアントの描画分散は適用外と裁定。
  出所: ユーザー裁定 2026-08-23「適用外でOK」

## Considered Options

棄却案と理由は `.decisions/2026-08-23-mapObject起動生成は近傍先行と時間予算分散にする.md` に記録
（Spawn中心／Layout順後着／イベント破棄継続／全期間16ms／個数固定維持／master起点／露頭適用／テスト側限定）。

## Consequences

- ゲーム開始直後は遠方のmapObjectが未生成で、距離順に数十秒かけて埋まる（後着4ms/フレーム）
- k-d tree索引は後着Registerのたびdirtyになり、ピン探索中のguidは後着期間中毎フレーム再構築される
  （最大7,000点規模で概算1〜2ms/回）。許容の可否と間引きはk-d treeセッション側へ申し送り（agent前提）
- k-d側plan/ADRの前提文「点集合は起動後静的・追加なし」は本決定で崩れるため、当該文書の更新が必要（agent前提）
- 開幕スキット中も後着生成は継続する。`ISkitWorldObjectControl.SetActive(false)`はdatastore rootごと
  非活性化するため、後着個体もroot配下として一括で隠れる（agent前提）
