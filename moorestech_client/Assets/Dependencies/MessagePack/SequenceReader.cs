// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/* Licensed to the .NET Foundation under one or more agreements.
 * The .NET Foundation licenses this file to you under the MIT license.
 * See the LICENSE file in the project root for more information. */

using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace MessagePack
{
    internal ref struct SequenceReader<T>
        where T : unmanaged, IEquatable<T>
    {
        /// <summary>
        ///     A value indicating whether we're using <see cref="sequence" /> (as opposed to <see cref="memory" />.
        /// </summary>
        private readonly bool usingSequence;

        /// <summary>
        ///     Backing for the entire sequence when we're not using <see cref="memory" />.
        /// </summary>
        private ReadOnlySequence<T> sequence;

        /// <summary>
        ///     The position at the start of the <see cref="CurrentSpan" />.
        /// </summary>
        private SequencePosition currentPosition;

        /// <summary>
        ///     The position at the end of the <see cref="CurrentSpan" />.
        /// </summary>
        private SequencePosition nextPosition;

        /// <summary>
        ///     Backing for the entire sequence when we're not using <see cref="sequence" />.
        /// </summary>
        private readonly ReadOnlyMemory<T> memory;

        /// <summary>
        ///     A value indicating whether there is unread data remaining.
        /// </summary>
        private bool moreData;

        /// <summary>
        ///     The total number of elements in the sequence.
        /// </summary>
        private long length;

        /// <summary>
        ///     Initializes a new instance of the <see cref="SequenceReader{T}" /> struct
        ///     over the given <see cref="ReadOnlySequence{T}" />.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SequenceReader(in ReadOnlySequence<T> sequence)
        {
            usingSequence = true;
            CurrentSpanIndex = 0;
            Consumed = 0;
            this.sequence = sequence;
            memory = default;
            currentPosition = sequence.Start;
            length = -1;

            var first = sequence.First.Span;
            nextPosition = sequence.GetPosition(first.Length);
            CurrentSpan = first;
            moreData = first.Length > 0;

            if (!moreData && !sequence.IsSingleSegment)
            {
                moreData = true;
                GetNextSpan();
            }
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="SequenceReader{T}" /> struct
        ///     over the given <see cref="ReadOnlyMemory{T}" />.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SequenceReader(ReadOnlyMemory<T> memory)
        {
            usingSequence = false;
            CurrentSpanIndex = 0;
            Consumed = 0;
            this.memory = memory;
            CurrentSpan = memory.Span;
            length = memory.Length;
            moreData = memory.Length > 0;

            currentPosition = default;
            nextPosition = default;
            sequence = default;
        }

        /// <summary>
        ///     Gets a value indicating whether there is no more data in the <see cref="Sequence" />.
        /// </summary>
        public bool End => !moreData;

        /// <summary>
        ///     Gets the underlying <see cref="ReadOnlySequence{T}" /> for the reader.
        /// </summary>
        public ReadOnlySequence<T> Sequence
        {
            get
            {
                if (sequence.IsEmpty && !memory.IsEmpty)
                {
                    // We're in memory mode (instead of sequence mode).
                    // Lazily fill in the sequence data.
                    sequence = new ReadOnlySequence<T>(memory);
                    currentPosition = sequence.Start;
                    nextPosition = sequence.End;
                }

                return sequence;
            }
        }

        /// <summary>
        ///     Gets the current position in the <see cref="Sequence" />.
        /// </summary>
        public SequencePosition Position
            => Sequence.GetPosition(CurrentSpanIndex, currentPosition);

        /// <summary>
        ///     Gets the current segment in the <see cref="Sequence" /> as a span.
        /// </summary>
        public ReadOnlySpan<T> CurrentSpan { get; private set; }

        /// <summary>
        ///     Gets the index in the <see cref="CurrentSpan" />.
        /// </summary>
        public int CurrentSpanIndex { get; private set; }

        /// <summary>
        ///     Gets the unread portion of the <see cref="CurrentSpan" />.
        /// </summary>
        public ReadOnlySpan<T> UnreadSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => CurrentSpan.Slice(CurrentSpanIndex);
        }

        /// <summary>
        ///     Gets the total number of <typeparamref name="T" />'s processed by the reader.
        /// </summary>
        public long Consumed { get; private set; }

        /// <summary>
        ///     Gets remaining <typeparamref name="T" />'s in the reader's <see cref="Sequence" />.
        /// </summary>
        public long Remaining => Length - Consumed;

        /// <summary>
        ///     Gets count of <typeparamref name="T" /> in the reader's <see cref="Sequence" />.
        /// </summary>
        public long Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (length < 0)
                    // Cache the length
                    length = Sequence.Length;

                return length;
            }
        }

        /// <summary>
        ///     Peeks at the next value without advancing the reader.
        /// </summary>
        /// <param name="value">The next value or default if at the end.</param>
        /// <returns>False if at the end of the reader.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPeek(out T value)
        {
            if (moreData)
            {
                value = CurrentSpan[CurrentSpanIndex];
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        ///     Read the next value and advance the reader.
        /// </summary>
        /// <param name="value">The next value or default if at the end.</param>
        /// <returns>False if at the end of the reader.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryRead(out T value)
        {
            if (End)
            {
                value = default;
                return false;
            }

            value = CurrentSpan[CurrentSpanIndex];
            CurrentSpanIndex++;
            Consumed++;

            if (CurrentSpanIndex >= CurrentSpan.Length)
            {
                if (usingSequence)
                    GetNextSpan();
                else
                    moreData = false;
            }

            return true;
        }

        /// <summary>
        ///     Move the reader back the specified number of items.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Rewind(long count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));

            Consumed -= count;

            if (CurrentSpanIndex >= count)
            {
                CurrentSpanIndex -= (int)count;
                moreData = true;
            }
            else if (usingSequence)
            {
                // Current segment doesn't have enough data, scan backward through segments
                RetreatToPreviousSpan(Consumed);
            }
            else
            {
                throw new ArgumentOutOfRangeException("Rewind went past the start of the memory.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void RetreatToPreviousSpan(long consumed)
        {
            Debug.Assert(usingSequence, "usingSequence");
            ResetReader();
            Advance(consumed);
        }

        private void ResetReader()
        {
            Debug.Assert(usingSequence, "usingSequence");
            CurrentSpanIndex = 0;
            Consumed = 0;
            currentPosition = Sequence.Start;
            nextPosition = currentPosition;

            if (Sequence.TryGet(ref nextPosition, out var memory))
            {
                moreData = true;

                if (memory.Length == 0)
                {
                    CurrentSpan = default;

                    // No data in the first span, move to one with data
                    GetNextSpan();
                }
                else
                {
                    CurrentSpan = memory.Span;
                }
            }
            else
            {
                // No data in any spans and at end of sequence
                moreData = false;
                CurrentSpan = default;
            }
        }

        /// <summary>
        ///     Get the next segment with available data, if any.
        /// </summary>
        private void GetNextSpan()
        {
            Debug.Assert(usingSequence, "usingSequence");
            if (!Sequence.IsSingleSegment)
            {
                var previousNextPosition = nextPosition;
                while (Sequence.TryGet(ref nextPosition, out var memory))
                {
                    currentPosition = previousNextPosition;
                    if (memory.Length > 0)
                    {
                        CurrentSpan = memory.Span;
                        CurrentSpanIndex = 0;
                        return;
                    }

                    CurrentSpan = default;
                    CurrentSpanIndex = 0;
                    previousNextPosition = nextPosition;
                }
            }

            moreData = false;
        }

        /// <summary>
        ///     Move the reader ahead the specified number of items.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(long count)
        {
            const long TooBigOrNegative = unchecked((long)0xFFFFFFFF80000000);
            if ((count & TooBigOrNegative) == 0 && CurrentSpan.Length - CurrentSpanIndex > (int)count)
            {
                CurrentSpanIndex += (int)count;
                Consumed += count;
            }
            else if (usingSequence)
            {
                // Can't satisfy from the current span
                AdvanceToNextSpan(count);
            }
            else if (CurrentSpan.Length - CurrentSpanIndex == (int)count)
            {
                CurrentSpanIndex += (int)count;
                Consumed += count;
                moreData = false;
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
        }

        /// <summary>
        ///     Unchecked helper to avoid unnecessary checks where you know count is valid.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AdvanceCurrentSpan(long count)
        {
            Debug.Assert(count >= 0, "count >= 0");

            Consumed += count;
            CurrentSpanIndex += (int)count;
            if (usingSequence && CurrentSpanIndex >= CurrentSpan.Length) GetNextSpan();
        }

        /// <summary>
        ///     Only call this helper if you know that you are advancing in the current span
        ///     with valid count and there is no need to fetch the next one.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AdvanceWithinSpan(long count)
        {
            Debug.Assert(count >= 0, "count >= 0");

            Consumed += count;
            CurrentSpanIndex += (int)count;

            Debug.Assert(CurrentSpanIndex < CurrentSpan.Length, "this.CurrentSpanIndex < this.CurrentSpan.Length");
        }

        /// <summary>
        ///     Move the reader ahead the specified number of items
        ///     if there are enough elements remaining in the sequence.
        /// </summary>
        /// <returns><c>true</c> if there were enough elements to advance; otherwise <c>false</c>.</returns>
        internal bool TryAdvance(long count)
        {
            if (Remaining < count) return false;

            Advance(count);
            return true;
        }

        private void AdvanceToNextSpan(long count)
        {
            Debug.Assert(usingSequence, "usingSequence");
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));

            Consumed += count;
            while (moreData)
            {
                var remaining = CurrentSpan.Length - CurrentSpanIndex;

                if (remaining > count)
                {
                    CurrentSpanIndex += (int)count;
                    count = 0;
                    break;
                }

                // As there may not be any further segments we need to
                // push the current index to the end of the span.
                CurrentSpanIndex += remaining;
                count -= remaining;
                Debug.Assert(count >= 0, "count >= 0");

                GetNextSpan();

                if (count == 0) break;
            }

            if (count != 0)
            {
                // Not enough data left- adjust for where we actually ended and throw
                Consumed -= count;
                throw new ArgumentOutOfRangeException(nameof(count));
            }
        }

        /// <summary>
        ///     Copies data from the current <see cref="Position" /> to the given <paramref name="destination" /> span.
        /// </summary>
        /// <param name="destination">Destination to copy to.</param>
        /// <returns>True if there is enough data to copy to the <paramref name="destination" />.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryCopyTo(Span<T> destination)
        {
            var firstSpan = UnreadSpan;
            if (firstSpan.Length >= destination.Length)
            {
                firstSpan.Slice(0, destination.Length).CopyTo(destination);
                return true;
            }

            return TryCopyMultisegment(destination);
        }

        internal bool TryCopyMultisegment(Span<T> destination)
        {
            if (Remaining < destination.Length) return false;

            var firstSpan = UnreadSpan;
            Debug.Assert(firstSpan.Length < destination.Length, "firstSpan.Length < destination.Length");
            firstSpan.CopyTo(destination);
            var copied = firstSpan.Length;

            var next = nextPosition;
            while (Sequence.TryGet(ref next, out var nextSegment))
                if (nextSegment.Length > 0)
                {
                    var nextSpan = nextSegment.Span;
                    var toCopy = Math.Min(nextSpan.Length, destination.Length - copied);
                    nextSpan.Slice(0, toCopy).CopyTo(destination.Slice(copied));
                    copied += toCopy;
                    if (copied >= destination.Length) break;
                }

            return true;
        }
    }
}