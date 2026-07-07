---
name: core-master-layer-boundary
description: "Core.Master(ItemMaster等)へのドメインロジック追加は禁止。Game層機能の解決ロジックはGame層に置き、イベントはC# eventでなくUniRxを使う"
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 831c510f-8217-447c-8dfe-b4bb597cd109
---

プレイヤーインベントリのスロットレベル解決アクセサをItemMaster(Core.Master)に追加する計画を提案したら強く却下された。「プレイヤーインベントリのドメインの変更をItemMasterに入れるのは絶対におかしい。プレイヤーインベントリ管理はGame層なのだからCore層に手を入れること自体間違い」。あわせて「ActionではなくUniRxを使う」。

**Why:** Core.Masterはマスタデータの生ロード・保持のみが責務。各ドメイン固有の解釈（レベル→スロット数の解決、フォールバック等）はそのドメインを所有するGame層アセンブリの責務。イベントもプロジェクト標準はUniRx（Game.UnlockState等が前例）。

**How to apply:**
- マスタ由来の値をドメインが解釈する場合、`MasterHolder.XxxMaster.Xxx`（public readonly生成物）を読むstaticユーティリティを該当Game層アセンブリ（例: Game.PlayerInventory.Interface）に作る。Core.Masterのクラスにはメソッドを足さない
- 通知は `event Action<T>` でなく `IObservable<T>` + `Subject<T>`（UniRx）。asmdefのreferencesに `"UniRx"` を追加（Game.UnlockState.asmdefが前例）
- 関連: [[user-prefers-one-way-flow]]
