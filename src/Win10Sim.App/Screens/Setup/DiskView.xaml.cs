using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Win10Sim.App.Infrastructure;
using Win10Sim.Core.Setup;

namespace Win10Sim.App.Screens.Setup;

/// <summary>
/// Выбор раздела и форматирование.
///
/// Как в настоящем установщике:
///   * диски и разделы показаны одним списком с заголовками;
///   * кнопка «Форматировать» активна только для основного раздела;
///   * кнопка «Далее» активна только после форматирования выбранного раздела —
///     иначе установка молча затёрла бы данные без предупреждения.
/// </summary>
public partial class DiskView : SetupScreenBase
{
    private readonly ObservableCollection<DiskRow> _rows = new();
    private bool _formatting;

    public DiskView(AppServices services)
        : base(services)
    {
        InitializeComponent();

        DiskList.ItemsSource = _rows;
        RebuildRows();
    }

    protected override OsPhase? BackTarget => OsPhase.SetupInstallType;

    // Пока идёт форматирование, Esc не должен уводить с экрана.
    protected override bool CanNavigateBack => !_formatting;

    private void RebuildRows()
    {
        // Запоминаем именно раздел, а не строку: после Clear() строки становятся
        // чужими для ListBox и присваивание SelectedItem молча игнорируется.
        PartitionInfo? previousPartition = (DiskList.SelectedItem as DiskRow)?.Partition;

        _rows.Clear();
        foreach (DiskInfo disk in Services.Disks)
        {
            _rows.Add(DiskRow.ForDisk(disk));
            foreach (PartitionInfo partition in disk.Partitions)
                _rows.Add(DiskRow.ForPartition(partition));
        }

        // Восстанавливаем выбор: прежний раздел → сохранённый в сессии → первый пригодный.
        DiskRow? target =
            (previousPartition is null ? null : FindRow(previousPartition.DiskIndex, previousPartition.PartitionIndex))
            ?? FindRow(Session.SelectedDiskIndex, Session.SelectedPartitionIndex)
            ?? _rows.FirstOrDefault(r => r.Partition?.CanInstall == true);

        DiskList.SelectedItem = target;
    }

    private DiskRow? FindRow(int diskIndex, int partitionIndex)
        => _rows.FirstOrDefault(r =>
            r.Partition is { } p && p.DiskIndex == diskIndex && p.PartitionIndex == partitionIndex);

    private void OnPartitionSelected(object sender, SelectionChangedEventArgs e)
    {
        if (DiskList.SelectedItem is not DiskRow row || row.Partition is not { } partition)
        {
            FormatButton.IsEnabled = false;
            NextButton.IsEnabled = false;
            StatusText.Text = UiStrings.Get("L.Disk.NoSelection");
            return;
        }

        Session.SelectedDiskIndex = partition.DiskIndex;
        Session.SelectedPartitionIndex = partition.PartitionIndex;

        FormatButton.IsEnabled = partition.CanFormat;

        // Выбор другого раздела сбрасывает отметку форматирования: старый
        // отформатированный раздел к новому выбору отношения не имеет.
        bool formatted = Session.DiskFormatted && Session.FormattedPartition == partition.Id;
        NextButton.IsEnabled = formatted && partition.CanInstall;

        StatusText.Text = formatted
            ? UiStrings.Get("L.Disk.FormatDone")
            : partition.CanInstall
                ? UiStrings.Get("L.Disk.MustFormat")
                : UiStrings.Get("L.Disk.Warning");

        Services.SaveState();
    }

    private async void OnFormatClick(object sender, RoutedEventArgs e)
    {
        if (DiskList.SelectedItem is not DiskRow row || row.Partition is not { } partition || !partition.CanFormat)
            return;

        _formatting = true;
        FormatButton.IsEnabled = false;
        NextButton.IsEnabled = false;
        FormatOverlay.Visibility = Visibility.Visible;
        FormatStageText.Text = UiStrings.Get("L.Disk.FormatQuick");

        try
        {
            var progress = new Progress<FormatStage>(stage => FormatStageText.Text = stage switch
            {
                FormatStage.QuickFormat => UiStrings.Get("L.Disk.FormatQuick"),
                FormatStage.CreatingFileSystem => UiStrings.Get("L.Disk.FormatCreating"),
                _ => UiStrings.Get("L.Disk.FormatDone")
            });

            await Services.Installer.FormatAsync(partition, progress, CancellationToken.None);

            Session.DiskFormatted = true;
            Session.FormattedPartition = partition.Id;
            Services.SaveState();

            // Небольшая пауза, чтобы пользователь увидел финальный статус.
            await Task.Delay(SetupTimings.Scale(TimeSpan.FromMilliseconds(700)));
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            _formatting = false;
            FormatOverlay.Visibility = Visibility.Collapsed;
        }

        FormatButton.IsEnabled = partition.CanFormat;
        NextButton.IsEnabled = true;
        StatusText.Text = UiStrings.Get("L.Disk.FormatDone");
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e) => RebuildRows();

    private void OnNextClick(object sender, RoutedEventArgs e)
    {
        if (DiskList.SelectedItem is not DiskRow row || row.Partition is not { } partition)
            return;

        if (!Session.DiskFormatted || Session.FormattedPartition != partition.Id)
        {
            StatusText.Text = UiStrings.Get("L.Disk.MustFormat");
            return;
        }

        Session.SelectedDiskIndex = partition.DiskIndex;
        Session.SelectedPartitionIndex = partition.PartitionIndex;
        Services.SaveState();
        Advance(OsPhase.SetupCopying);
    }

    private void OnBackClick(object sender, RoutedEventArgs e) => Advance(OsPhase.SetupInstallType);
}
