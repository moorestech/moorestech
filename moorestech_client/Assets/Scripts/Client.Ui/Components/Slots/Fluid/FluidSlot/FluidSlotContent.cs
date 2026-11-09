using System;
using Client.Ui.Foundation.Icons;
using UnityEngine;

namespace Client.Ui.Components.Slots
{
    /// <summary>
    /// FluidSlot に入れる中身。空・ゴースト・中身を sealed 派生で表し、各派生は自分に要る値だけを持つ
    /// What a FluidSlot holds: empty, ghost and content as sealed subtypes, each carrying only its own values
    /// </summary>
    public abstract class FluidSlotContent
    {
        public static readonly FluidSlotContent Empty = new EmptyFluidSlotContent();

        private protected FluidSlotContent()
        {
        }

        // 空タンクに束縛のゴーストを描く。中身のあるタンクには使わない（Web は中身があればゴーストを無視する）
        // Draws a bound ghost on an empty tank; never for a tank with content (the Web ignores the ghost then)
        public static GhostFluidSlotContent Ghost(UiIcon icon, double amount)
        {
            if (icon == null) throw new ArgumentNullException(nameof(icon), "A ghost fluid needs display material; use Empty for a bare tank");
            return new GhostFluidSlotContent(icon, amount);
        }

        public static FilledFluidSlotContent Filled(UiIcon icon, double amount, double capacity, Color face)
        {
            if (icon == null) throw new ArgumentNullException(nameof(icon), "A filled fluid needs display material; use Empty for an empty tank");
            return new FilledFluidSlotContent(icon, amount, capacity, face);
        }
    }

    public sealed class EmptyFluidSlotContent : FluidSlotContent
    {
        internal EmptyFluidSlotContent()
        {
        }
    }

    public sealed class GhostFluidSlotContent : FluidSlotContent
    {
        public UiIcon Icon { get; }
        public double Amount { get; }

        internal GhostFluidSlotContent(UiIcon icon, double amount)
        {
            Icon = icon;
            Amount = amount;
        }
    }

    public sealed class FilledFluidSlotContent : FluidSlotContent
    {
        public UiIcon Icon { get; }
        public double Amount { get; }
        public double Capacity { get; }
        public Color Face { get; }

        internal FilledFluidSlotContent(UiIcon icon, double amount, double capacity, Color face)
        {
            Icon = icon;
            Amount = amount;
            Capacity = capacity;
            Face = face;
        }
    }
}
