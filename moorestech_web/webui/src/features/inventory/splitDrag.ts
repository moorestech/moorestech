import type { SlotRef } from "@/bridge";

export class SplitDragSession {
  private slots: SlotRef[] = [];
  private active = false;
  constructor(private readonly send: (slots: SlotRef[]) => void) {}
  begin(slot: SlotRef, grabHeld: boolean): void { if (grabHeld) { this.active = true; this.slots = [slot]; } }
  enter(slot: SlotRef): void {
    if (!this.active || this.slots.some((current) => current.area === slot.area && current.slot === slot.slot)) return;
    this.slots.push(slot);
  }
  end(): void { if (this.active) { this.active = false; this.send(this.slots); this.slots = []; } }
}
