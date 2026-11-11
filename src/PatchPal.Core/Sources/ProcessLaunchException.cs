namespace PatchPal.Core.Sources;

/// <summary>A process couldn't be started the way it was asked for (for example, without admin rights).</summary>
public sealed class ProcessLaunchException : Exception
{
    public ProcessLaunchException(string message) : base(message) { }

    public ProcessLaunchException(string message, Exception inner) : base(message, inner) { }
}
