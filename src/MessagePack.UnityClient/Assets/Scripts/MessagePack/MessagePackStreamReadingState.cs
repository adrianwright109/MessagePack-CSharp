// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Nerdbank.Streams;

namespace MessagePack
{
    public struct MessagePackStreamReadingState
    {
        internal readonly Stream Stream;
        internal SequencePool.Rental SequenceRental;
        internal SequencePosition? EndOfLastMessage;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessagePackStreamReadingState"/> struct.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        internal MessagePackStreamReadingState(Stream stream)
        {
            this.Stream = stream ?? throw new ArgumentNullException(nameof(stream));
            this.SequenceRental = SequencePool.Shared.Rent();
            this.EndOfLastMessage = null;
        }

        /// <summary>
        /// Gets the sequence that we read data from the <see cref="Stream"/> into.
        /// </summary>
        internal Sequence<byte> ReadData => this.SequenceRental.Value;

        internal bool IsDefault => this.Stream == null;

        /// <summary>
        /// Gets any bytes that have been read since the last complete message returned from <see cref="ReadAsync(CancellationToken)"/>.
        /// </summary>
        public ReadOnlySequence<byte> RemainingBytes => this.EndOfLastMessage.HasValue ? this.ReadData.AsReadOnlySequence.Slice(this.EndOfLastMessage.Value) : this.ReadData.AsReadOnlySequence;
  }
}
