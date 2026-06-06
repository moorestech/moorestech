import { Mermaid } from '../components/comment-feature';

const chart = `graph LR
    BS["ブレインストーミング<br/>意図・要件・設計の対話"] --> SPEC["設計仕様<br/>spec.md"]
    SPEC --> A1["Codex 監査 1<br/>設計の妥当性"]
    A1 --> SPEC2["仕様に反映<br/>不変条件・抽選順序など"]
    SPEC2 --> PLAN["実装計画<br/>フェーズA / TDD タスク"]
    PLAN --> A2["Codex 監査 2<br/>実コード照合で10指摘"]
    A2 --> PLAN2["計画を再構成<br/>セーブ永続化・容量予約など"]

    classDef doc fill:#EFF6FF,stroke:#2563EB,stroke-width:2px,color:#0F172A
    classDef audit fill:#FFF7ED,stroke:#F59E0B,stroke-width:2px,color:#0F172A
    class BS,SPEC,SPEC2,PLAN,PLAN2 doc
    class A1,A2 audit`;

export function Process() {
  return (
    <section className="section" id="process">
      <div className="eyebrow">07 — 固め方</div>
      <h2>対話 → 仕様 → 計画を、外部監査で 2 回叩いた</h2>
      <p className="lead">
        設計はいきなりコードに落とさず、ブレインストーミングで意図を引き出し、仕様化し、実装計画にし、
        その各段で Codex（外部AI監査人）に実コードを読ませて批判させた。確証バイアスを外すためのガードレール。
      </p>

      <Mermaid
        id="design-process"
        chart={chart}
        label="図04 設計の固め方の流れ。ブレインストーミングから設計仕様を書き、Codex監査1で設計の妥当性を叩いて仕様へ反映し、実装計画（フェーズA・TDDタスク）に落とし、Codex監査2で実コードと照合した10件の指摘を受けて計画を再構成する、という反復プロセス"
      />

      <div className="sxs">
        <div className="col">
          <div className="col-head">監査1（設計仕様）で出た主な指摘</div>
          <ul>
            <li>効果軸を分離し、品質だけを別レイヤーに</li>
            <li>メタデータ「土台あり」は罠。serialization を確認せよ</li>
            <li>レベル無視消費の有無が独立IDの可否を決める</li>
          </ul>
        </div>
        <div className="col">
          <div className="col-head">監査2（実装計画）で出た主な指摘</div>
          <ul>
            <li>処理中セーブ/ロードで効果が壊れる</li>
            <li>生産性の二重格納・容量の食い合い</li>
            <li>共有 static Random は非決定的</li>
            <li>「全機械共通」と言いつつ ElectricMachine だけ</li>
          </ul>
        </div>
      </div>

      <h3>フェーズA の完了条件（仕様 §8.5）</h3>
      <ul className="checks">
        <li><span className="mk ok"><svg width="12" height="12" viewBox="0 0 12 12" aria-hidden="true"><path d="M2.5 6.3l2.3 2.3 4.7-5.3" fill="none" stroke="#fff" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/></svg></span><span>モジュール専用サブインベントリの保存・移動制限（装着済み上書き拒否・Count&gt;1拒否・通常搬入禁止）</span></li>
        <li><span className="mk ok"><svg width="12" height="12" viewBox="0 0 12 12" aria-hidden="true"><path d="M2.5 6.3l2.3 2.3 4.7-5.3" fill="none" stroke="#fff" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/></svg></span><span>処理開始時の効果スナップショット ＋ セーブ永続化</span></li>
        <li><span className="mk ok"><svg width="12" height="12" viewBox="0 0 12 12" aria-hidden="true"><path d="M2.5 6.3l2.3 2.3 4.7-5.3" fill="none" stroke="#fff" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/></svg></span><span>加算後の clamp / 消費電力倍率を実効要求電力に適用</span></li>
        <li><span className="mk ok"><svg width="12" height="12" viewBox="0 0 12 12" aria-hidden="true"><path d="M2.5 6.3l2.3 2.3 4.7-5.3" fill="none" stroke="#fff" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/></svg></span><span>生産性追加産出の仮想容量予約 ＋ 決定的抽選</span></li>
        <li><span className="mk no">—</span><span>ネット同期・クライアントUI は範囲外（別プラン）。サーバーロジック＋セーブまでが本プランの完了</span></li>
      </ul>
    </section>
  );
}
