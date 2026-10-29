using Win10Sim.Core.Setup;
using Xunit;

namespace Win10Sim.Core.Tests;

public class DiskFactoryTests
{
    [Fact]
    public void CreateDefault_ProducesTwoDisksWithExpectedLayout()
    {
        IReadOnlyList<DiskInfo> disks = DiskFactory.CreateDefault();

        Assert.Equal(2, disks.Count);
        Assert.Equal(new[] { 0, 1 }, disks.Select(d => d.Index));
        Assert.Equal(3, disks[0].Partitions.Count);
        Assert.Equal(PartitionKind.Unallocated, disks[1].Partitions[0].Kind);
    }

    [Fact]
    public void FirstDisk_HasEfiAndPrimarySystemPartition()
    {
        DiskInfo disk = DiskFactory.CreateDefault()[0];

        Assert.Contains(disk.Partitions, p => p.Kind == PartitionKind.Efi);
        PartitionInfo primary = disk.Partitions.Single(p => p.Kind == PartitionKind.Primary);
        Assert.True(primary.CanInstall);
        Assert.True(primary.CanFormat);
    }

    [Fact]
    public void RecoveryAndEfiPartitions_CannotBeInstallTargets()
    {
        DiskInfo disk = DiskFactory.CreateDefault()[0];

        foreach (PartitionInfo partition in disk.Partitions.Where(p => p.Kind is PartitionKind.Efi or PartitionKind.Recovery))
        {
            Assert.False(partition.CanInstall);
            Assert.False(partition.CanFormat);
        }
    }

    [Fact]
    public void PartitionIds_AreUnique()
    {
        var ids = DiskFactory.CreateDefault()
            .SelectMany(d => d.Partitions)
            .Select(p => p.Id)
            .ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void Find_ReturnsPartitionOrNull()
    {
        IReadOnlyList<DiskInfo> disks = DiskFactory.CreateDefault();

        Assert.Equal(PartitionKind.Primary, DiskFactory.Find(disks, 0, 3)!.Kind);
        Assert.Null(DiskFactory.Find(disks, 0, 99));
        Assert.Null(DiskFactory.Find(disks, 7, 1));
    }
}
