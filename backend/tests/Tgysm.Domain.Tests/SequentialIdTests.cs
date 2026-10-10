using Tgysm.Domain.Common;

namespace Tgysm.Domain.Tests;

public sealed class SequentialIdTests
{
    private const int IdCount = 10_000;
    private const int RfcVariantBits = 0b10;
    private const int RandomVariantBits = 2;

    [Fact]
    public void Ids_created_in_a_row_sort_in_creation_order_like_the_database_compares_them()
    {
        // Thousands of ids fall into the same millisecond, where a plain version 7 id is random.
        var ids = Enumerable.Range(0, IdCount).Select(_ => SequentialId.Next()).ToList();

        var sorted = ids.OrderBy(id => id.ToByteArray(bigEndian: true), ByteOrder.Instance).ToList();
        Assert.Equal(ids, sorted);
        Assert.Equal(IdCount, ids.Distinct().Count());
    }

    [Fact]
    public void Ids_keep_the_version_7_and_variant_bits()
    {
        var id = SequentialId.Next();

        Assert.Equal(7, id.Version);
        // RFC 9562 variant: the top two bits of the variant nibble are 10; the rest is random.
        Assert.Equal(RfcVariantBits, id.Variant >> RandomVariantBits);
    }

    private sealed class ByteOrder : IComparer<byte[]>
    {
        public static readonly ByteOrder Instance = new();

        public int Compare(byte[]? x, byte[]? y) => x.AsSpan().SequenceCompareTo(y);
    }
}
