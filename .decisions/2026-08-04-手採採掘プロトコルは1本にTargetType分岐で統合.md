決定: 手採採掘は1ドメインとみなし、既存va:mapObjectInfoAcquisitionを拡張・改名した採掘プロトコル1本（va:mining相当）にTargetType enum(mapObject/vein)で分岐する。mapObjectはinstanceId、veinは座標(Vector3Int)を運び、サーバーはGetOverVeinsで権威判定する。ツール照合・クールダウン（1振り制限）のサーバーサービスは両ターゲットで共有する。
棄却案: 既存プロトコルは触らずva:veinMiningを新設する（同一操作が2本に割れ、クールダウン共有に別途配線が要る。1ドメイン1本・Mode分岐の規約に反する）
理由: プロトコル規約への準拠と、プレイヤー1振り制限が全採掘共通で自然に成立するため。
リンク: [[2026-08-04-露頭mapObject化を棄却しvein自体を採掘対象にする]]、.claude/rules/server-protocol.md
