決定: mapVeinsの露頭参照はoutcropAddressablePath（休眠データ）を廃止し、item/fluid問わず全veinが持つoutcropMapObjectGuid（foreignKey→map/mapObjects）に置換する。露頭の見た目は露頭mapObjectのaddressablePathに一本化。fluid鉱脈（水・原油）の露頭は叩けない純ビジュアルとし、mapObjectsのminingTypeに「None」を追加してスキーマで明示する。
棄却案:
- 露頭参照をitem veinのveinParam限定にしfluid veinは当面露頭なし（fluid鉱脈の発見手段が無いままになる）
- item veinはGuid参照・fluid veinは純ビジュアルのoutcropAddressablePathの二本立て（クライアントの露頭表示経路が2つに割れる）
理由: 全veinで露頭の生成・表示経路が1本になり、叩けるか否かはminingTypeで明示的に表現できる。
リンク: .decisions/2026-08-04-露頭をサーバー管理mapObjectとして手掘り対象にする.md
