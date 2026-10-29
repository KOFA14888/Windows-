using System.Text.Json;
using System.Text.Json.Serialization;
using Win10Sim.Core.Setup;

namespace Win10Sim.Core.Persistence;

/// <summary>
/// Чтение/запись state.json. Запись атомарная (temp + move), чтобы «перезагрузка
/// на середине установки» никогда не оставила повреждённый файл.
/// </summary>
public sealed class JsonStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public JsonStateStore(string? stateFilePath = null)
    {
        StateFilePath = stateFilePath ?? AppPaths.StateFile;
    }

    public string StateFilePath { get; }

    public bool Exists => File.Exists(StateFilePath);

    /// <summary>Загружает состояние; при отсутствии/повреждении файла возвращает новую сессию.</summary>
    public SetupSession Load()
    {
        if (!Exists)
            return new SetupSession();

        try
        {
            string json = File.ReadAllText(StateFilePath);
            SetupSession? session = JsonSerializer.Deserialize<SetupSession>(json, SerializerOptions);
            return session ?? new SetupSession();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Битое состояние не должно блокировать запуск: начинаем установку заново
            // и сохраняем копию файла рядом для разбора.
            TryPreserveCorruptedFile();
            return new SetupSession();
        }
    }

    public void Save(SetupSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        AppPaths.EnsureCreated();

        string directory = Path.GetDirectoryName(StateFilePath)!;
        Directory.CreateDirectory(directory);

        string tempPath = StateFilePath + ".tmp";
        string json = JsonSerializer.Serialize(session, SerializerOptions);

        File.WriteAllText(tempPath, json);
        File.Move(tempPath, StateFilePath, overwrite: true);
    }

    public void Reset()
    {
        try
        {
            if (Exists)
                File.Delete(StateFilePath);
        }
        catch (IOException)
        {
            // Файл занят антивирусом — не критично, сессия всё равно сброшена в памяти.
        }
    }

    private void TryPreserveCorruptedFile()
    {
        try
        {
            string backup = StateFilePath + ".corrupted";
            File.Move(StateFilePath, backup, overwrite: true);
        }
        catch (IOException)
        {
        }
    }
}
