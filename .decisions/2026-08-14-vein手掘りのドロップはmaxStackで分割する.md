決定: VeinHandMiningService.CreateEarnedItems を VanillaStaticMapObject.GenerateEarnItems と同じくmaxStackで分割して複数スタックを返す形にする
棄却案: MapVeinMasterUtilに maxCount <= レベル1 maxStack の検証を足してデータ側で縛る
理由: ItemStackコンストラクタは上限超過でthrowし、手掘りが使われる序盤はレベル1＝maxStack最小。同ドメインの兄弟実装が既に分割しているので前例に揃える。バリデーションで縛るとマスタ側の自由度をレベル1に固定してしまう
リンク: VeinHandMiningService.cs:92-98 / VanillaStaticMapObject.cs:93-119 / ItemStack.cs:21-23 / ItemStackLevelDataStore.GetMaxStack
