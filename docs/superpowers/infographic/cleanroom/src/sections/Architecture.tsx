import { Mermaid } from '../components/comment-feature';

const seqChart = `
sequenceDiagram
    actor Player
    participant World as WorldBlockDatastore
    participant Sys as CleanRoomDetectionSystem
    participant Det as CleanRoomDetector
    participant Tick as GameUpdater

    rect rgba(37, 99, 235, 0.07)
      Note over Player,Sys: PHASE 1 — 境界ブロックの変更だけが dirty を立てる
      Player->>World: TryAddBlock(壁) / RemoveBlock
      World->>Sys: OnBlockPlaceEvent / OnBlockRemoveEvent
      Sys->>Sys: 境界ブロックなら geometryDirty = true
    end

    rect rgba(14, 165, 233, 0.08)
      Note over Tick,World: PHASE 2 — tick で dirty なら再検出
      Tick->>Sys: UpdateObservable (毎tick)
      Sys->>Det: dirty なら DetectAllRooms(world)
      Det->>World: BlockMasterDictionary を全走査し境界セル集合を構築
      Det-->>Sys: 部屋リスト（各々 V・S・有効フラグ）
      Sys->>Sys: Rooms を更新
    end
`;

export function Architecture() {
  return (
    <section className="section">
      <div className="eyebrow">06 — 実装アーキテクチャ</div>
      <h2>世界システムが tick で部屋を更新する</h2>
      <p className="lead">
        <b>CleanRoomDetectionSystem</b> は DI シングルトンの世界システム。ブロックの設置/破壊のうち
        <b>境界ブロックの変更だけ</b>を購読して geometry-dirty を立て、<code>GameUpdater</code> の tick で
        dirty なら純関数 <b>CleanRoomDetector</b> に全走査再検出させる。非境界ブロックの設置は部屋形状を変えないので再検出を起こさない。
      </p>

      <Mermaid
        chart={seqChart}
        id="seq"
        label="図06 プレイヤーが壁を設置/破壊するとWorldBlockDatastoreがOnBlockPlace/Removeイベントを発火し、CleanRoomDetectionSystemは境界ブロックのときだけgeometryDirtyを立てる。次のtickでGameUpdaterのUpdateObservableが発火し、dirtyならCleanRoomDetectorがBlockMasterDictionaryを全走査して境界セル集合を作りList<CleanRoom>を返してRoomsを更新する流れ"
      />

      <h3>製造機との関係（フェーズ4で実装）</h3>
      <p>
        半導体製造機は<b>有効なクリーンルーム内でのみ稼働</b>する。壁破壊やリークで密閉が崩れる、または機械が部屋外にあると、
        <b>外に置いたのと同じ＝単純に停止</b>。建築途中やチャンクロードでの一瞬の無効化で全機停止する事故を防ぐため、
        部屋状態に <b>Valid / Degraded / Invalid</b> と数秒の再評価猶予、クラス判定にヒステリシスを入れる。
        multi-block 機械は <code>TryGetRoomContainingBlock</code> で<b>全占有セルが同一部屋</b>かを判定する。
      </p>

      <h3>フェーズ分解ロードマップ</h3>
      <div className="table-wrap">
        <table className="tbl">
          <thead><tr><th>フェーズ</th><th>内容</th><th>主産物</th></tr></thead>
          <tbody>
            <tr><td><b>1</b>（プラン化済み）</td><td>境界ブロック＋3D密閉部屋検出＋部屋レジストリ/クエリ</td><td>囲うと検出、壊すと無効化</td></tr>
            <tr><td>2</td><td>純度シミュ（濃度・クラス・ヒステリシス・ACH）＋部屋同一性と純度の永続化</td><td>部屋にクラスが付き応答</td></tr>
            <tr><td>3</td><td>清浄機＋フィルター仕事量消費＋汚染源4種</td><td>維持ループが回る</td></tr>
            <tr><td>4</td><td>製造機統合（binning・Valid/Degraded/Invalid停止）</td><td>生産が部屋クラス依存</td></tr>
            <tr><td>5</td><td>I/O挙動（ハッチ/コネクタ/ドア）＋必要な状態のセーブ/ロード</td><td>遊べる完全形</td></tr>
          </tbody>
        </table>
      </div>

      <div className="callout info">
        <b>性能メモ：</b> ワールドはチャンク非分割で全ブロックが常駐し、<code>GetBlock(Vector3Int)</code> は O(n)。
        だから flood-fill では毎回 <code>BlockMasterDictionary</code> から境界セル集合を一括構築する。dirty 領域の差分更新・連結成分AABBによる局所化はフェーズ2以降の最適化。
      </div>
    </section>
  );
}
