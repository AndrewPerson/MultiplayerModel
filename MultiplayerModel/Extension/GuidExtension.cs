namespace MultiplayerModel.Extension;

public static class GuidExtension
{
    extension(Guid guid)
    {
        public static Guid CreateVersion8(ulong payloadPart1, uint payloadPart2 = 0)
        {
            var packedPayloadArr = new byte[12];

            BitConverter.TryWriteBytes(packedPayloadArr, payloadPart1);
            BitConverter.TryWriteBytes(new(packedPayloadArr, 8, 4), payloadPart2);

            var guidBytes = new byte[16];

            new ReadOnlySpan<byte>(packedPayloadArr, 0, 6).CopyTo(guidBytes);
            guidBytes[6] = 0x80;
            new ReadOnlySpan<byte>(packedPayloadArr, 6, 1).CopyTo(new(guidBytes, 7, 1));
            guidBytes[8] = 0x80;
            new ReadOnlySpan<byte>(packedPayloadArr, 7, 5).CopyTo(new(guidBytes, 9, 5));

            // Guid seems to reverse these bytes internally so we have to preempt this?
            new Span<byte>(guidBytes, 0, 4).Reverse();
            new Span<byte>(guidBytes, 4, 2).Reverse();
            new Span<byte>(guidBytes, 6, 2).Reverse();

            return new(guidBytes);
        }

        public (ulong, uint) ParseAsVersion8()
        {
            var packedPayloadArr = new byte[12];
            var guidBytes = new byte[16];

            guid.TryWriteBytes(guidBytes);

            // Guid seems to reverse these bytes internally so we have to undo this?
            new Span<byte>(guidBytes, 0, 4).Reverse();
            new Span<byte>(guidBytes, 4, 2).Reverse();
            new Span<byte>(guidBytes, 6, 2).Reverse();

            new ReadOnlySpan<byte>(guidBytes, 0, 6).CopyTo(packedPayloadArr);
            new ReadOnlySpan<byte>(guidBytes, 7, 1).CopyTo(new(packedPayloadArr, 6, 1));
            new ReadOnlySpan<byte>(guidBytes, 9, 5).CopyTo(new(packedPayloadArr, 7, 5));

            return (
                BitConverter.ToUInt64(new(packedPayloadArr, 0, 8)),
                BitConverter.ToUInt32(new(packedPayloadArr, 8, 4))
            );
        }
    }
}