import { type ReactNode, useState } from "react";
import { L, useI18n } from "@/shared/i18n";
import styles from "./style.module.css";

// fallbackは「未指定」をundefinedセンチネルで表さず判別リテラルにする
// fallback is a discriminated literal, not an undefined sentinel for "unspecified"
export type IconFallback = { kind: "idText" } | { kind: "none" } | { kind: "node"; node: ReactNode };

type Props = {
  // id はエラー状態を id 変化でリセットする役割も兼ねるため number/guid どちらも受け取る
  // id also resets error state on change, so it accepts both numeric ids and guids
  id: string | number;
  src: string;
  alt: string;
  className?: string;
  fallback: IconFallback;
};

type ErroredTarget = { id: string | number; src: string };

// ゲーム内アイコンの画像表示とIDフォールバックを共通化する
// Shares image rendering and the id fallback across game icons
export default function GameIcon({ id, src, alt, className, fallback }: Props) {
  const { t } = useI18n();
  const [erroredTarget, setErroredTarget] = useState<ErroredTarget | null>(null);

  // idだけでなくsrcも一致する間だけラッチし、配信先が変わったら再取得を許す
  // Latch only while both id and src match, so a changed source is fetched again
  if (erroredTarget !== null && erroredTarget.id === id && erroredTarget.src === src) {
    if (fallback.kind === "none") return null;
    if (fallback.kind === "node") return fallback.node;
    return <span className={`${styles.fallback} ${className ?? ""}`}>
      {t(L.ui.common.iconIdFallback, { id })}
    </span>;
  }

  return <img src={src} alt={alt} className={className} draggable={false} onError={() => setErroredTarget({ id, src })} />;
}
