決定: 設置不可理由のカーソルtooltip表示は、通常ブロック設置(CommonBlockPlaceSystem)だけでなくベルトコンベア・レール・列車・ギアチェーンポール・電線ツール・BP貼り付けの全PlaceSystemを対象にし、「理由→カーソルtooltip」の共通基盤を作って各PlaceSystemから理由をプッシュする。
棄却案: CommonBlockPlaceSystemのみ対応し基盤だけ共通化して他は後続タスクへ回す案。
理由: 表示の一貫性を優先し、PlaceSystemごとに理由表示の有無が割れる状態を作らない。
リンク: [[2026-08-21-設置不可の全理由をカーソルtooltipに表示する]] / PlaceSystemBase.cs / PlaceSystemStateController.cs

出所: ユーザー裁定 2026-08-21 選択「全PlaceSystemを対象にし、理由表示の基盤を共通化」
