import GameIcon from "./GameIcon";

type Props = {
  itemId: number;
  alt?: string;
  className?: string;
};

export default function ItemIcon({ itemId, alt, className }: Props) {
  return <GameIcon id={itemId} src={`/api/icons/${itemId}.png`} alt={alt ?? `item ${itemId}`} className={className} />;
}
