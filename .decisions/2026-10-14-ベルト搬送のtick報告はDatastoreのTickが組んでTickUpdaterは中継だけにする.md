# ベルト搬送のtick報告はDatastoreのTickが組んでTickUpdaterは中継だけにする

日付: 2026-10-14
文脈: 搬送 tick の差分取り出しと再構築フラグが、Datastore の public 公開（DiffRecorder・OnAssemblyRebuilt）と BeltTransportTickUpdater の中継に分かれていた。別の駆動経路（テスト手回し・将来のクライアント同時 tick 計算）が Simulation.Tick だけを呼ぶと差分が次 tick へ混入し、全量切替フラグも立たない（moores-code-review Warning 22 / D7）。

決定: BeltTransportDatastore.Tick() が BeltTransportTickReport（Simulation.Tick → 差分取り出し → 再構築フラグ）を返す形にし、DiffRecorder と OnAssemblyRebuilt の public 公開を廃止する。BeltTransportTickUpdater.Update は Datastore.Tick() の結果を OnNext で中継するだけにする。流体・歯車の Datastore/TickUpdater 二分は維持する。出所: ユーザー裁定 2026-10-14「案A: Datastore.Tick() が報告を返す（推奨）」
棄却案: TickUpdater を廃止し、Datastore が IObservable を自ら発火して MasterTickUpdater が Datastore.Tick() を直接呼ぶ（流体の二分前例から逸脱）。
理由: 搬送と差分取り出しが1メソッドで不可分になり、前例の形も保てる。
リンク: moorestech_logs/harness/moores-code-review/runs/2026-10-13-2340-belt-segment-client-sync-22de6217/design.md
