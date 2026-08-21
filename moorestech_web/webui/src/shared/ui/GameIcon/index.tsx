import { type ReactNode, useState } from "react";
import { L, useI18n } from "@/shared/i18n";
import styles from "./style.module.css";

type Props = {
  // id はエラー状態を id 変化でリセットする役割も兼ねるため number/guid どちらも受け取る
  // id also resets error state on change, so it accepts both numeric ids and guids
  id: string | number;
  src: string;
  alt: string;
  className?: string;
  fallback?: ReactNode;
};

// ゲーム内アイコンの画像表示とIDフォールバックを共通化する
// Shares image rendering and the id fallback across game icons
export default function GameIcon({ id, src, alt, className, fallback }: Props) {
  const { t } = useI18n();
  const [erroredId, setErroredId] = useState<string | number | null>(null);

  if (erroredId === id) {
    if (fallback !== undefined) {
      return fallback;
    }
    return <span className={`${styles.fallback} ${className ?? ""}`}>
      {t(L.ui.common.iconIdFallback, { id })}
    </span>;
  }

  return <img src={src} alt={alt} className={className} draggable={false} onError={() => setErroredId(id)} />;
}
