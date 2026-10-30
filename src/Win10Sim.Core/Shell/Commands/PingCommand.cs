using System.Globalization;
using System.Net.NetworkInformation;

namespace Win10Sim.Core.Shell.Commands;

/// <summary>
/// ping — настоящий ICMP через <see cref="Ping"/> платформы .NET.
/// Это единственная команда, которая выходит в сеть: остальное — виртуальный диск.
///
/// Аргументы как у настоящего ping: <c>ping host</c>, <c>ping -n 2 host</c>,
/// <c>ping -l 32 host</c>, <c>ping -4 host</c>. Поддержано без привилегий
/// (обычный ICMP echo) и с отменой через CancellationToken (Ctrl+C в окне cmd).
/// </summary>
public sealed class PingCommand : ICommand
{
    public string Name => "ping";
    public string Help => "PING         Проверка соединения с узлом (реальный ICMP-запрос).";
    public string Usage =>
        "PING [-n число] [-l размер] [-w таймаут] [-4] [-6] имя_узла\r\n" +
        "  -n — число эхо-запросов (по умолчанию 4)\r\n" +
        "  -l — размер буфера (32–65500)\r\n" +
        "  -w — таймаут ожидания ответа в миллисекундах\r\n" +
        "  Например: ping ya.ru -n 2";

    public async Task<CommandResult> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (arguments.Count == 0)
            return CommandResult.FromLine("Использование: ping [-n число] [-l размер] [-w таймаут] имя_узла");

        int count = 4;
        int bufferSize = 32;
        int timeout = 4000;
        string? host = null;

        for (int i = 0; i < arguments.Count; i++)
        {
            string argument = arguments[i];

            if (argument is "-n" or "/n" && i + 1 < arguments.Count)
            {
                int.TryParse(arguments[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out count);
                continue;
            }

            if (argument is "-l" or "/l" && i + 1 < arguments.Count)
            {
                int.TryParse(arguments[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out bufferSize);
                continue;
            }

            if (argument is "-w" or "/w" && i + 1 < arguments.Count)
            {
                int.TryParse(arguments[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out timeout);
                continue;
            }

            if (argument is "-4" or "-6" or "-t" or "/4" or "/6")
                continue;     // -t в GUI-симуляторе ведёт себя как обычный ping

            if (!argument.StartsWith('-') && !argument.StartsWith('/'))
                host = argument;
        }

        if (string.IsNullOrWhiteSpace(host))
            return CommandResult.FromLine("Использование: ping имя_узла");

        count = Math.Clamp(count, 1, 20);
        bufferSize = Math.Clamp(bufferSize, 32, 65500);
        timeout = Math.Clamp(timeout, 100, 10000);

        var lines = new List<string>
        {
            string.Empty,
            $"Обмен пакетами с {host} с {bufferSize} байт данных:",
            string.Empty
        };

        byte[] buffer = new byte[bufferSize];
        int sent = 0;
        int received = 0;
        long totalMilliseconds = 0;
        int minimum = int.MaxValue;
        int maximum = 0;

        try
        {
            using var ping = new Ping();

            for (int i = 0; i < count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                PingReply reply = await ping.SendPingAsync(host, timeout, buffer).ConfigureAwait(false);
                sent++;

                if (reply.Status == IPStatus.Success)
                {
                    received++;
                    int time = (int)reply.RoundtripTime;
                    totalMilliseconds += time;
                    minimum = Math.Min(minimum, time);
                    maximum = Math.Max(maximum, time);

                    lines.Add($"Ответ от {reply.Address}: число байт={reply.Buffer.Length} время={time}мс TTL={reply.Options?.Ttl ?? 0}");
                }
                else
                {
                    lines.Add($"Превышен интервал ожидания для запроса. ({reply.Status})");
                }

                if (i < count - 1)
                    await Task.Delay(700, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (PingException ex)
        {
            lines.Add("Не удалось выполнить ping: " + ex.InnerException?.Message ?? ex.Message);
            return new CommandResult(lines, exitCode: 1);
        }
        catch (Exception ex) when (ex is System.Net.Sockets.SocketException or ArgumentException)
        {
            lines.Add("Не удалось разрешить имя узла " + host + ".");
            return new CommandResult(lines, exitCode: 1);
        }

        int lost = sent - received;
        double lossPercent = sent == 0 ? 0 : lost * 100.0 / sent;

        lines.Add(string.Empty);
        lines.Add($"Статистика Ping для {host}:");
        lines.Add($"    Пакетов: отправлено = {sent}, получено = {received}, потеряно = {lost} ({(int)lossPercent}% потерь)");

        if (received > 0)
        {
            long average = totalMilliseconds / received;
            lines.Add("Примерное время передачи сигнала в мс:");
            lines.Add($"    Минимальное = {minimum}мс, Максимальное = {maximum}мс, Среднее = {average}мс");
        }

        lines.Add(string.Empty);
        return new CommandResult(lines, exitCode: received > 0 ? 0 : 1);
    }
}
