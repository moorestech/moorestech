# 解放判定（Guid正規化＋unlock参照）は PlacementTargetCatalog へ集約する

決定: 坂→直線の Guid 正規化と解放状態の参照を `PlacementTargetCatalog` の1メソッドへ畳み、`BeltConveyorPlaceFamilyUtil.ResolveUnlockBlockGuid` の public 露出を廃止する。`Server.Protocol.asmdef` に `Game.PlacementTarget` を足し、`BlockPickResolver` は static をやめてカタログ注入にする。

棄却案: `BeltConveyorPlaceFamilyUtil` に `IsBlockUnlocked` を足して集約する（asmdef 参照追加1行で済む最小案）。3サイトの重複と例外/静かにfalseの割れは消えるが、スポイトだけが無料設置デバッグ（showAllPlaceable）を見ない不整合は残る。

棄却案: `PlacementTargetEntry` に `UnlockId` を持たせ構築時に1回だけ正規化する。走査コストが起動時1回になり汎用スイッチも種別ディスパッチへ戻るが、ブループリント・列車・接続ツールを含む ctor の一括変更を伴う。採用案と併用可能なので、必要になった時点で追加する。

理由: `PlacementTargetCatalog` には既に「実在確認も含め判定規則はここへ完全集約する（C1裁定）」というコメントがあり、集約先を他所にすると過去の裁定と矛盾する。加えて採用案だけが「無料設置デバッグONでスポイトだけ未解放ブロックを拒否する」という現に起きている不整合を同時に消す。

リンク: [[2026-09-05-坂ベルトは個別エントリとして単体設置できるようにする]]
