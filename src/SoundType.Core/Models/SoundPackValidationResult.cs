namespace SoundType.Core.Models;

public sealed class SoundPackValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<string> Errors { get; } = [];
}
