using System;

namespace LightInDark.UI.Ability;

public interface IAbilityTimer
{
    float Duration { get; }
    float Remaining { get; }
    float Progress { get; }
    bool IsProgressing { get; }
}

/// <summary>Reusable Nebula-style cooldown/effect timer; time advances only through Tick.</summary>
public sealed class AbilityTimer : IAbilityTimer
{
    public float Duration { get; private set; }
    public float Remaining { get; private set; }
    public bool IsProgressing { get; private set; }
    public float Progress => Duration > 0 ? Math.Clamp(Remaining / Duration, 0f, 1f) : 0f;
    public Func<bool> CanProgress { get; set; }
    public event Action Completed;
    public AbilityTimer(float duration) => SetDuration(duration);
    public void SetDuration(float duration)
    {
        if (float.IsNaN(duration) || float.IsInfinity(duration) || duration < 0)
            throw new ArgumentOutOfRangeException(nameof(duration));
        Duration = duration;
    }
    public void Start() { Remaining = Duration; IsProgressing = Remaining > 0; }
    public void SetRemaining(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
        Remaining = Math.Max(0f, value);
    }
    public void Pause() => IsProgressing = false;
    public void Resume() => IsProgressing = Remaining > 0;
    public void Stop() { Remaining = 0f; IsProgressing = false; }
    public void Tick(float delta)
    {
        if (!IsProgressing || delta <= 0 || float.IsNaN(delta) || float.IsInfinity(delta)
            || !(CanProgress?.Invoke() ?? true)) return;
        Remaining = Math.Max(0f, Remaining - delta);
        if (Remaining > 0) return;
        IsProgressing = false;
        Completed?.Invoke();
    }
}
