// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nerdbank.Streams;
using Xunit;

namespace MessagePack.Tests
{
    public class MessagePackSerializerStreamTests
    {
        private static readonly TimeSpan TestTimeoutSpan = Debugger.IsAttached ? Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(5);
        private readonly ReadOnlySequence<byte> twoMessages;
        private readonly IReadOnlyList<SequencePosition> messagePositions;
        private readonly CancellationToken timeoutToken = new CancellationTokenSource(TestTimeoutSpan).Token;

        public MessagePackSerializerStreamTests()
        {
            var sequence = new Sequence<byte>();
            var writer = new MessagePackWriter(sequence);
            var positions = new List<SequencePosition>();

            // First message
            writer.Write(5);
            writer.Flush();
            positions.Add(sequence.AsReadOnlySequence.End);

            // Second message is more interesting.
            writer.WriteArrayHeader(2);
            writer.Write("Hi");
            writer.Write("There");
            writer.Flush();
            positions.Add(sequence.AsReadOnlySequence.End);

            this.twoMessages = sequence.AsReadOnlySequence;
            this.messagePositions = positions;
        }

        [Fact]
        public void Deserialize_NullStream()
        {
            Assert.Throws<ArgumentNullException>("stream", () => MessagePackSerializer.Deserialize<bool>(stream: null));
            Assert.Throws<ArgumentNullException>("stream", () =>
            {
                var state = default(MessagePackStreamReadingState);
                MessagePackSerializer.Deserialize<bool>(stream: null, ref state);
            });
        }

        [Fact]
        public async Task DeserializeAsync_NullStream()
        {
            await Assert.ThrowsAsync<ArgumentNullException>("stream", async () => await MessagePackSerializer.DeserializeAsync<bool>(stream: null));
            await Assert.ThrowsAsync<ArgumentNullException>("stream", async () =>
            {
                var state = new StrongBox<MessagePackStreamReadingState>();
                await MessagePackSerializer.DeserializeAsync<bool>(stream: null, state);
            });
        }

        [Fact]
        public async Task DeserializeAsync_NullState()
        {
            await Assert.ThrowsAsync<ArgumentNullException>("state", async () => await MessagePackSerializer.DeserializeAsync<bool>(stream: new MemoryStream(), state: null));
        }

        [Fact]
        public async Task StreamEndsWithNoMessage()
        {
            using (var reader = new MessagePackStreamReader(new MemoryStream()))
            {
                Assert.Null(await reader.ReadAsync(this.timeoutToken));
                Assert.True(reader.RemainingBytes.IsEmpty);
            }
        }

        [Fact]
        public async Task StreamEndsCleanlyAfterMessage()
        {
            var oneMessage = this.twoMessages.Slice(0, this.messagePositions[0]).ToArray();
            using (var reader = new MessagePackStreamReader(new MemoryStream(oneMessage)))
            {
                var message1 = await reader.ReadAsync(this.timeoutToken);
                Assert.NotNull(message1);
                Assert.Equal(oneMessage, message1.Value.ToArray());

                Assert.True(reader.RemainingBytes.IsEmpty);
                Assert.Null(await reader.ReadAsync(this.timeoutToken));
                Assert.True(reader.RemainingBytes.IsEmpty);
            }
        }

        [Fact]
        public async Task TwoMessagesInSingleRead()
        {
            using (var reader = new MessagePackStreamReader(new MemoryStream(this.twoMessages.ToArray())))
            {
                var message1 = await reader.ReadAsync(this.timeoutToken);
                Assert.NotNull(message1);
                Assert.Equal(this.twoMessages.Slice(0, this.messagePositions[0]).ToArray(), message1.Value.ToArray());

                var message2 = await reader.ReadAsync(this.timeoutToken);
                Assert.NotNull(message2);
                Assert.Equal(this.twoMessages.Slice(this.messagePositions[0], this.messagePositions[1]).ToArray(), message2.Value.ToArray());

                Assert.Null(await reader.ReadAsync(this.timeoutToken));
            }
        }

        [Fact]
        public async Task StreamEndsWithNoMessageAndExtraBytes()
        {
            // We'll include the start of the second message since it is multi-byte.
            var partialMessage = this.twoMessages.Slice(messagePositions[0], 1).ToArray();
            using (var reader = new MessagePackStreamReader(new MemoryStream(partialMessage)))
            {
                Assert.Null(await reader.ReadAsync(this.timeoutToken));
                Assert.Equal(partialMessage, reader.RemainingBytes.ToArray());
            }
        }

        [Fact]
        public async Task StreamEndsAfterMessageWithExtraBytes()
        {
            // Include the first message and one more byte
            var partialMessage = this.twoMessages.Slice(0, 2).ToArray();
            using (var reader = new MessagePackStreamReader(new MemoryStream(partialMessage)))
            {
                var firstMessage = await reader.ReadAsync(this.timeoutToken);
                Assert.NotNull(firstMessage);
                Assert.Equal(partialMessage.Take(1), firstMessage.Value.ToArray());

                Assert.Equal(partialMessage.Skip(1), reader.RemainingBytes.ToArray());
                Assert.Null(await reader.ReadAsync(this.timeoutToken));
                Assert.Equal(partialMessage.Skip(1), reader.RemainingBytes.ToArray());
            }
        }
    }
}
