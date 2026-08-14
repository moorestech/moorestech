決定: 電線の「既存ブロックへの接続」と「電柱延長設置」をva:electricWireExtendの1プロトコルに統合する。リクエストで終点種別（既存ブロック/新設電柱）を区別し、応答は共通で「成否＋終点InstanceId（=次の起点）」を返す。va:electricWireConnectionEditはDisconnect（切断）専用に縮小し、Connectモードは廃止する。
棄却案: プロトコルは2本のまま、connectionEditに応答を足しクライアントの起点更新パイプラインだけ共有する案。
理由: 両操作は「起点→終点への延長、成功したら終点が次の起点」で本質的に同一。二重実装を残さないため。
リンク: [[2026-08-05-電線接続の起点チェーンは応答確認後に行う]] / ElectricWireExtendProtocol.cs / ElectricWireConnectionEditProtocol.cs
