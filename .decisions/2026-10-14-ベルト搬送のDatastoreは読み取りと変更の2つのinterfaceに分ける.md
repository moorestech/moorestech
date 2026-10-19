# ベルト搬送のDatastoreは読み取りと変更の2つのinterfaceに分ける

日付: 2026-10-14
文脈: BeltTransportDatastore が interface 無しの具象で、読み取りだけの利用側（イベントパケット・セーブ・搬入口）にも RebuildIfDirty や tick 進行が見えていた。tick 順序の外から呼ばれると二重 tick で同期が止まりうる（moores-code-review C16 / D3）。

決定: IBeltTransportLookup（読み取りビュー・セーブ状態・全量・ハッシュ）と IBeltTransportMutation（再構築・ロード登録・tick・機械からの押し込み）に分け、具象が両方を実装する。DI は同一インスタンスを両 interface で登録し、電力網の ElectricWireNetworkDatastore と同形にする。出所: ユーザー裁定 2026-10-14「案A: Lookup / Mutation の2 interface（推奨）」
棄却案: 変更を TickUpdater に閉じ Datastore を読み取り専用公開にする（ロード登録口を別に要する）。流体・歯車と同じ単一 interface IBeltTransportDatastore（前例一致だが読み取り側から変更 API が見えたまま）。
理由: 読み取り側の型から変更 API が消え、電力網の前例とも揃う。
リンク: moorestech_logs/harness/moores-code-review/runs/2026-10-13-2340-belt-segment-client-sync-22de6217/design.md
