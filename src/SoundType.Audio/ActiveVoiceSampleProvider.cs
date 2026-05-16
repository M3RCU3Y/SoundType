using System.Threading;
using NAudio.Wave;

namespace SoundType.Audio;

public sealed class ActiveVoiceSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly IDisposable _lease;
    private int _released;

    public ActiveVoiceSampleProvider(ISampleProvider source, IDisposable lease)
    {
        _source = source;
        _lease = lease;
        WaveFormat = source.WaveFormat;
    }

    public WaveFormat WaveFormat { get; }

    public int Read(float[] buffer, int offset, int count)
    {
        try
        {
            int read = _source.Read(buffer, offset, count);
            if (read == 0)
            {
                Release();
            }

            return read;
        }
        catch
        {
            Release();
            throw;
        }
    }

    private void Release()
    {
        if (Interlocked.Exchange(ref _released, 1) == 0)
        {
            _lease.Dispose();
        }
    }
}
