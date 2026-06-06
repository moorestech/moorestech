export function LevelRep() {
  return (
    <section className="section" id="level-rep">
      <div className="eyebrow">04 — 最大の設計判断</div>
      <h2>レベル違いアイテムを「別 ItemId」にするか「メタデータ」にするか</h2>
      <p className="lead">
        品質モジュールはレベル違いの産物（チップLv1 / Lv2 …）を生む。これをどう表現するかが設計の分岐点。
        コードベースの事実が決め手になった。
      </p>

      <h3>コードベースの事実 — メタデータの「土台」は罠だった</h3>
      <p>
        <code>IItemStack</code> には <code>ItemStackMetaData</code> のスケルトンが既にある。一見 8 割できているように見えるが、実態は配線されていない。
      </p>
      <div className="code">{`// IItemStack のコメント — 概念だけ存在し、動作はサポート外
`}<span className="c">{`// 「動作はサポートしていないです」`}</span>{`
IItemStack SetMeta(string key, ItemStackMetaData value);

`}<span className="c">{`// 確認した事実:`}</span>{`
`}<span className="c">{`//  ・IsAllowedToAdd（スタック結合判定）は Id のみ見て meta を見ない`}</span>{`
`}<span className="c">{`//  ・セーブ ItemStackSaveJsonObject は itemGuid + count のみ（meta 非永続）`}</span>{`
`}<span className="c">{`//  ・通信 ItemMessagePack は Id + Count のみ（meta 非同期）`}</span>{`
`}<span className="c">{`//  ・レシピマッチングは ItemId 基準`}</span></div>
      <p>
        つまりメタデータ路線は「<strong>部分対応があるぶん、むしろ危険</strong>」。永続化・ネット同期・レシピ照合・スタック結合判定を、
        コアの最もデータ破損 / desync に弱い経路でゼロから整合させる必要がある。
      </p>

      <h3>2 案の比較</h3>
      <div className="sxs">
        <div className="col win">
          <div className="col-head"><span className="pill win">採用</span>独立 ItemId ＋ スキーマ自動生成</div>
          <p>レベルごとに別アイテム。</p>
          <ul>
            <li>スタック・セーブ・通信・レシピ照合が<strong>既存のまま全部動く</strong>（コア改修ほぼゼロ）</li>
            <li>変種アイテムと「LvK × M → Lv(K+1)」合成レシピはスキーマ＋SourceGenerator で自動生成</li>
            <li>N≈5 想定なので ID 数は爆発しない</li>
          </ul>
        </div>
        <div className="col">
          <div className="col-head"><span className="pill lose">不採用</span>ItemStackMetaData にレベルを持たせる</div>
          <p>既存スケルトンを使う…が、</p>
          <ul>
            <li>永続化・同期・レシピ照合・スタック結合を<strong>全部新規実装</strong></li>
            <li>コアの危険な経路に深く手を入れる</li>
            <li>利点は「どのレベルでも可」消費がタダになる点だけ</li>
          </ul>
        </div>
      </div>

      <div className="note-callout">
        <span className="tag">決め手になった問い</span>
        「レベルを持つアイテムは、下流レシピでレベルを無視して消費されるか？」
        → 答えは「<strong>レベルごとに用途が明確（高Lvは高tier専用）</strong>」。
        レベルが常に使用時点で意味を持つので、メタデータ案の唯一の利点が効かない。よって独立 ItemId で綺麗に収まる。
      </div>
    </section>
  );
}
