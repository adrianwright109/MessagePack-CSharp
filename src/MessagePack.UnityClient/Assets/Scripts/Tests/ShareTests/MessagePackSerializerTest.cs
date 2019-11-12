// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using Nerdbank.Streams;
using SharedData;
using Xunit;

namespace MessagePack.Tests
{
    public class MessagePackSerializerTest
    {
#if !ENABLE_IL2CPP

        [Fact]
        public void NonGeneric()
        {
            var data = new FirstSimpleData { Prop1 = 9, Prop2 = "hoge", Prop3 = 999 };
            Type t = typeof(FirstSimpleData);
            var ms = new MemoryStream();
            var writerBytes = new Sequence<byte>();
            var writer = new MessagePackWriter(writerBytes);

            var data1 = MessagePackSerializer.Deserialize(t, MessagePackSerializer.Serialize(t, data)) as FirstSimpleData;
            var data2 = MessagePackSerializer.Deserialize(t, MessagePackSerializer.Serialize(t, data, StandardResolver.Options)) as FirstSimpleData;

            MessagePackSerializer.Serialize(t, ms, data);
            ms.Position = 0;
            var data3 = MessagePackSerializer.Deserialize(t, ms) as FirstSimpleData;

            ms = new MemoryStream();
            MessagePackSerializer.Serialize(t, ms, data, StandardResolver.Options);
            ms.Position = 0;
            var data4 = MessagePackSerializer.Deserialize(t, ms, StandardResolver.Options) as FirstSimpleData;

            MessagePackSerializer.Serialize(t, ref writer, data, StandardResolver.Options);
            writer.Flush();
            var reader = new MessagePackReader(writerBytes.AsReadOnlySequence);
            var data5 = MessagePackSerializer.Deserialize(t, ref reader, StandardResolver.Options) as FirstSimpleData;

            new[] { data1.Prop1, data2.Prop1, data3.Prop1, data4.Prop1, data5.Prop1 }.Distinct().Is(data.Prop1);
            new[] { data1.Prop2, data2.Prop2, data3.Prop2, data4.Prop2, data5.Prop2 }.Distinct().Is(data.Prop2);
            new[] { data1.Prop3, data2.Prop3, data3.Prop3, data4.Prop3, data5.Prop3 }.Distinct().Is(data.Prop3);
        }

#endif

#if !UNITY_2018_3_OR_NEWER

        /* Unity's NUnit currently no supported Task test. */

        [Fact]
        public async Task NonGeneric_Async()
        {
            var data = new FirstSimpleData { Prop1 = 9, Prop2 = "hoge", Prop3 = 999 };
            Type t = typeof(FirstSimpleData);
            var ms = new MemoryStream();

            await MessagePackSerializer.SerializeAsync(t, ms, data);
            ms.Position = 0;
            var data2 = (FirstSimpleData)await MessagePackSerializer.DeserializeAsync(t, ms);

            Assert.Equal(data, data2);
        }

#endif

        [Fact]
        public void StreamAPI()
        {
            FirstSimpleData[] originalData = Enumerable.Range(1, 100).Select(x => new FirstSimpleData { Prop1 = x * x, Prop2 = "hoge", Prop3 = x }).ToArray();

            var ms = new MemoryStream();
            MessagePackSerializer.Serialize(ms, originalData); // serialize to stream

            var normal = MessagePackSerializer.Serialize(originalData);

            ms.Position = 0;

            normal.SequenceEqual(ms.ToArray()).IsTrue();

            var decompress1 = MessagePackSerializer.Deserialize<FirstSimpleData[]>(ms.ToArray());
            var decompress2 = MessagePackSerializer.Deserialize<FirstSimpleData[]>(normal);
            var decompress3 = MessagePackSerializer.Deserialize<FirstSimpleData[]>(ms);
            ms.Position = 0;
            var onmore = new NonMemoryStream(ms);
            var decompress4 = MessagePackSerializer.Deserialize<FirstSimpleData[]>(onmore);

            decompress1.IsStructuralEqual(originalData);
            decompress2.IsStructuralEqual(originalData);
            decompress3.IsStructuralEqual(originalData);
            decompress4.IsStructuralEqual(originalData);
        }

        [Fact]
        public void BufferRecyclingDeferral()
        {
            var ms = new NonMemoryStream(new MemoryStream());
            MessagePackSerializer.Serialize(ms, "hi");
            ms.Position = 0;

            var resolver = CompositeResolver.Create(
                new IMessagePackFormatter[] { new LazyDeserializerFormatter<string>() },
                new IFormatterResolver[] { StandardResolver.Instance });
            var options = MessagePackSerializerOptions.Standard.WithResolver(resolver);
            Lazy<string> lazy = MessagePackSerializer.Deserialize<Lazy<string>>(ms, options);
            Assert.NotNull(lazy);
            Assert.False(lazy.IsValueCreated);
            Assert.Equal("hi", lazy.Value);
        }

        private class LazyDeserializerFormatter<T> : IMessagePackFormatter<Lazy<T>>
        {
            public Lazy<T> Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
            {
                SequencePosition startPosition = reader.Position;
                reader.Skip();
                var slice = reader.Sequence.Slice(startPosition, reader.Position);
                IDisposable release = reader.ObtainBufferRecyclingDeferral();
                var formatter = options.Resolver.GetFormatterWithVerify<T>();
                return new Lazy<T>(() =>
                {
                    try
                    {
                        var deferredReader = new MessagePackReader(slice);
                        return formatter.Deserialize(ref deferredReader, options);
                    }
                    finally
                    {
                        release.Dispose();
                    }
                });
            }

            public void Serialize(ref MessagePackWriter writer, Lazy<T> value, MessagePackSerializerOptions options)
            {
                throw new NotImplementedException();
            }
        }
    }

    internal class NonMemoryStream : Stream
    {
        private readonly MemoryStream stream;

        public NonMemoryStream(MemoryStream stream)
        {
            this.stream = stream;
        }

        public override bool CanRead => this.stream.CanRead;

        public override bool CanSeek => this.stream.CanSeek;

        public override bool CanWrite => this.stream.CanWrite;

        public override long Length => this.stream.Length;

        public override long Position { get => this.stream.Position; set => this.stream.Position = value; }

        public override void Flush()
        {
            this.stream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return this.stream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return this.stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            this.stream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            this.stream.Write(buffer, offset, count);
        }
    }
}
