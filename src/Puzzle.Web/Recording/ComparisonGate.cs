namespace Puzzle.Web.Recording;

/// <summary>Lets one bot comparison run at a time: each keeps every core busy for seconds.</summary>
public sealed class ComparisonGate : IDisposable
{
    private readonly SemaphoreSlim _slot = new(1, 1);

    /// <summary>Takes the one slot; false while another comparison holds it. Whoever takes it must <see cref="Exit"/>.</summary>
    public bool TryEnter() => _slot.Wait(TimeSpan.Zero);

    public void Exit() => _slot.Release();

    public void Dispose() => _slot.Dispose();
}
