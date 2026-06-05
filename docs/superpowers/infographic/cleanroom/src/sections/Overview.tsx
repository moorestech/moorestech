import { Mermaid } from '../components/comment-feature';

const chainChart = `
graph LR
    SI["高純度シリコン"] --> WAFER["シリコンウェハ"]
    WAFER --> EUV["EUV露光<br/>(20%失敗)"]
    EUV --> PAT["N回積層パターンウェハ"]
    PAT --> DONE["完成パターンウェハ"]
    DONE --> DIE["シリコンダイ<br/>(1ウェハ300個/30%損失)"]
    DIE --> CHIP["ICチップ基盤"]

    CR(["クリーンルーム<br/>濃度→クラス→歩留まり"]) -. 製造の場 .-> EUV
    CR -. 製造の場 .-> PAT
    CR -. 製造の場 .-> DONE

    classDef chain fill:#EFF6FF,stroke:#2563EB,stroke-width:2px,color:#0F172A
    classDef room fill:#ECFDF5,stroke:#059669,stroke-width:2.5px,color:#065F46
    class SI,WAFER,EUV,PAT,DONE,DIE,CHIP chain
    class CR room
    linkStyle 6 stroke:#059669,stroke-width:1.6px,stroke-dasharray: 6 4
    linkStyle 7 stroke:#059669,stroke-width:1.6px,stroke-dasharray: 6 4
    linkStyle 8 stroke:#059669,stroke-width:1.6px,stroke-dasharray: 6 4
`;

export function Overview() {
  return (
    <section className="section">
      <div className="eyebrow">01 — 全体像</div>
      <h2>半導体チェーンの「製造の場」を縛る一層</h2>
      <p className="lead">
        生産チェーン自体（シリコン→ウェハ→露光→ダイ→チップ）は普通の工場ラインだが、
        露光・積層・現像といった<b>純度が効く工程</b>はクリーンルームの中でしか良い結果を出せない。
        クリーンルームは新しい生産物ではなく、既存ラインに乗る<b>環境制約のレイヤー</b>。
      </p>

      <Mermaid
        chart={chainChart}
        id="chain"
        label="図01 高純度シリコンからシリコンウェハ→EUV露光(20%失敗)→N回積層→完成パターンウェハ→シリコンダイ(1ウェハ300個・30%損失)→ICチップ基盤へと進む半導体生産チェーンと、露光・積層・現像工程がクリーンルーム内で行われ濃度→クラス→歩留まりに支配される関係"
      />

      <h3>設計の3本柱</h3>
      <div className="pillars">
        <div className="pillar">
          <div className="n">PILLAR 1</div>
          <h4>3D密閉部屋検出</h4>
          <p>壁・ドア・ハッチ・パイプコネクタで囲った立体空間を flood-fill で1部屋として検出する。</p>
        </div>
        <div className="pillar">
          <div className="n">PILLAR 2</div>
          <h4>不純物濃度モデル</h4>
          <p>濃度 C＝不純物数 N ÷ 体積 V。清浄機が体積を処理して濃度に比例除去し、平衡濃度に収束する。</p>
        </div>
        <div className="pillar">
          <div className="n">PILLAR 3</div>
          <h4>binning型の効果</h4>
          <p>濃度の閾値で決まるクラスが、作れる最大グレードの天井と汚れによる格下げ率を決める。</p>
        </div>
      </div>

      <div className="callout good">
        <b>割り切り：</b> アイテムを部屋外へ取り出しても変化しない。純度は<b>アイテムが持つ状態ではなく、製造の瞬間に参照される部屋の属性</b>。アイテム単位の汚染度トラッキングはしない。
      </div>
    </section>
  );
}
