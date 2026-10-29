using Win10Sim.App.Infrastructure;
using Win10Sim.Core.Setup;

namespace Win10Sim.App.Screens.Setup;

/// <summary>
/// Строка списка разделов. Заголовки дисков и разделы живут в одном списке,
/// как в настоящем установщике; заголовок отличается флагом <see cref="IsHeader"/>
/// и недоступен для выбора.
/// </summary>
public sealed class DiskRow
{
    private DiskRow()
    {
    }

    public bool IsHeader { get; private init; }

    public PartitionInfo? Partition { get; private init; }

    public string Icon { get; private init; } = string.Empty;

    public string Title { get; private init; } = string.Empty;

    public string Subtitle { get; private init; } = string.Empty;

    public string SizeText { get; private init; } = string.Empty;

    public string FreeText { get; private init; } = string.Empty;

    public static DiskRow ForDisk(DiskInfo disk) => new()
    {
        IsHeader = true,
        Icon = "\uEDA2",
        Title = UiStrings.Format("L.Disk.Drive", disk.Index) + "  ·  " + disk.Model,
        Subtitle = disk.Bus + "  ·  " + UiStrings.FormatSize(disk.SizeBytes)
    };

    public static DiskRow ForPartition(PartitionInfo partition)
    {
        string title = partition.Kind switch
        {
            PartitionKind.Unallocated => UiStrings.Format("L.Disk.Unallocated", partition.DiskIndex),
            PartitionKind.Efi => UiStrings.Format("L.Disk.Partition", partition.PartitionIndex) + "  ·  " + UiStrings.Get("L.Disk.Type.Efi"),
            PartitionKind.Recovery => UiStrings.Format("L.Disk.Partition", partition.PartitionIndex) + "  ·  " + UiStrings.Get("L.Disk.Type.Recovery"),
            _ => UiStrings.Format("L.Disk.Partition", partition.PartitionIndex) + "  ·  " + UiStrings.Get("L.Disk.Type.Primary")
        };

        return new DiskRow
        {
            IsHeader = false,
            Partition = partition,
            Title = title,
            Subtitle = UiStrings.Format("L.Disk.Size", UiStrings.FormatSize(partition.SizeBytes)),
            SizeText = UiStrings.FormatSize(partition.SizeBytes),
            FreeText = partition.FreeBytes > 0
                ? UiStrings.Format("L.Disk.Free", UiStrings.FormatSize(partition.FreeBytes))
                : string.Empty
        };
    }
}
