using System.Threading;

namespace SoundType.Audio;

public sealed class VoiceLimiter
{
    private int _activeVoices;
    private int _maxVoices;

    public VoiceLimiter(int maxVoices)
    {
        MaxVoices = maxVoices;
    }

    public int MaxVoices
    {
        get => Volatile.Read(ref _maxVoices);
        set => Volatile.Write(ref _maxVoices, Math.Max(1, value));
    }

    public int ActiveVoices => Volatile.Read(ref _activeVoices);

    public bool TryAcquire(out IDisposable lease)
    {
        while (true)
        {
            int active = Volatile.Read(ref _activeVoices);
            if (active >= MaxVoices)
            {
                lease = EmptyLease.Instance;
                return false;
            }

            if (Interlocked.CompareExchange(ref _activeVoices, active + 1, active) == active)
            {
                lease = new VoiceLease(this);
                return true;
            }
        }
    }

    private void Release()
    {
        int active = Interlocked.Decrement(ref _activeVoices);
        if (active < 0)
        {
            Volatile.Write(ref _activeVoices, 0);
        }
    }

    private sealed class VoiceLease(VoiceLimiter limiter) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                limiter.Release();
            }
        }
    }

    private sealed class EmptyLease : IDisposable
    {
        public static readonly EmptyLease Instance = new();

        public void Dispose()
        {
        }
    }
}
