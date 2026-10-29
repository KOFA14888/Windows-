namespace Win10Sim.Core.Setup;

public enum PartitionKind
{
    Unallocated = 0,
    Recovery,
    Efi,
    Primary,
    System
}

public sealed class PartitionInfo
{
    public PartitionInfo(int diskIndex, int partitionIndex, PartitionKind kind, long sizeBytes, long freeBytes)
    {
        DiskIndex = diskIndex;
        PartitionIndex = partitionIndex;
        Kind = kind;
        SizeBytes = sizeBytes;
        FreeBytes = freeBytes;
    }

    public int DiskIndex { get; }
    public int PartitionIndex { get; }
    public PartitionKind Kind { get; }
    public long SizeBytes { get; }

    /// <summary>Для незанятого пространства равен размеру, для разделов — вычисляется.</summary>
    public long FreeBytes { get; }

    public bool CanInstall => Kind is PartitionKind.Primary or PartitionKind.System or PartitionKind.Unallocated;
    public bool CanFormat => Kind is PartitionKind.Primary or PartitionKind.System;

    /// <summary>Стабильный ключ строки списка (например, "0:2").</summary>
    public string Id => $"{DiskIndex}:{PartitionIndex}";
}

public sealed class DiskInfo
{
    public DiskInfo(int index, string model, string bus, long sizeBytes, IReadOnlyList<PartitionInfo> partitions)
    {
        Index = index;
        Model = model;
        Bus = bus;
        SizeBytes = sizeBytes;
        Partitions = partitions;
    }

    public int Index { get; }
    public string Model { get; }
    public string Bus { get; }
    public long SizeBytes { get; }
    public IReadOnlyList<PartitionInfo> Partitions { get; }
}

/// <summary>
/// «Железо» симулятора: один системный SSD с типовой разметкой UEFI/GPT и
/// второй пустой диск. Ничего реального не читается и не форматируется.
/// </summary>
public static class DiskFactory
{
    private const long Gb = 1000L * 1000 * 1000;
    private const long Mb = 1000L * 1000;

    public static IReadOnlyList<DiskInfo> CreateDefault()
    {
        var disk0 = new DiskInfo(
            index: 0,
            model: "Samsung SSD 860 EVO 500GB",
            bus: "NVMe",
            sizeBytes: 476 * Gb,
            partitions: new[]
            {
                new PartitionInfo(0, 1, PartitionKind.Recovery, 500 * Mb, 0),
                new PartitionInfo(0, 2, PartitionKind.Efi, 100 * Mb, 100 * Mb),
                new PartitionInfo(0, 3, PartitionKind.Primary, 475 * Gb, 402 * Gb)
            });

        var disk1 = new DiskInfo(
            index: 1,
            model: "WDC WD10EZEX-08WN4A0",
            bus: "SATA",
            sizeBytes: 931 * Gb,
            partitions: new[]
            {
                new PartitionInfo(1, 1, PartitionKind.Unallocated, 931 * Gb, 931 * Gb)
            });

        return new[] { disk0, disk1 };
    }

    public static PartitionInfo? Find(IReadOnlyList<DiskInfo> disks, int diskIndex, int partitionIndex)
        => disks.FirstOrDefault(d => d.Index == diskIndex)?.Partitions
               .FirstOrDefault(p => p.PartitionIndex == partitionIndex);
}
