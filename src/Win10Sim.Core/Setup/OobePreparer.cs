namespace Win10Sim.Core.Setup;

/// <summary>Этапы экрана «Мы готовим всё для вас».</summary>
public enum PreparingStep
{
    ApplyingSettings = 0,
    ConfiguringDisplay = 1,
    InstallingApps = 2,
    ConnectingAccount = 3,
    CreatingProfile = 4,
    SettingUpCortana = 5
}

public readonly record struct PreparingProgress(double Percent, PreparingStep Step, bool IsDone);

/// <summary>
/// Имитация финальной подготовки системы после OOBE. Те же правила, что и у
/// установщика: никаких Thread.Sleep, отменяемость, прогресс через IProgress.
/// </summary>
public static class OobePreparer
{
    private readonly record struct StepPlan(PreparingStep Step, int From, int To, double Seconds);

    private static readonly StepPlan[] Plan =
    {
        new(PreparingStep.ApplyingSettings,   0, 14, 1.4),
        new(PreparingStep.ConfiguringDisplay, 14, 30, 1.6),
        new(PreparingStep.InstallingApps, 30, 58, 2.4),
        new(PreparingStep.ConnectingAccount, 58, 74, 1.4),
        new(PreparingStep.CreatingProfile, 74, 92, 1.8),
        new(PreparingStep.SettingUpCortana, 92, 100, 1.2)
    };

    public static async Task PrepareAsync(
        IProgress<PreparingProgress>? progress,
        CancellationToken cancellationToken)
    {
        foreach (StepPlan plan in Plan)
        {
            int steps = Math.Max(1, (plan.To - plan.From) * 2);
            TimeSpan perStep = TimeSpan.FromSeconds(plan.Seconds) / steps;

            for (int i = 0; i <= steps; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                double percent = plan.From + (plan.To - plan.From) * (double)i / steps;
                progress?.Report(new PreparingProgress(percent, plan.Step, false));

                await Task.Delay(perStep, cancellationToken).ConfigureAwait(false);
            }
        }

        progress?.Report(new PreparingProgress(100, PreparingStep.SettingUpCortana, true));
    }
}
