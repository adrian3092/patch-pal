namespace PatchPal.Core.Persistence;

/// <summary>
/// A lock held across every process in the Windows session, for a read-add-save of a shared
/// file: two Patch Pal windows would otherwise each save their own copy and drop the other's
/// change. Acquire and release it on the same thread.
/// </summary>
internal sealed class CrossProcessLock : IDisposable
{
    private readonly Mutex _mutex;

    private CrossProcessLock(Mutex mutex) => _mutex = mutex;

    public static CrossProcessLock Acquire(string name, TimeSpan timeout)
    {
        var mutex = new Mutex(initiallyOwned: false, name);
        try
        {
            if (!mutex.WaitOne(timeout))
                throw new IOException("Another Patch Pal window is still saving; try again in a moment.");
        }
        catch (AbandonedMutexException)
        {
            // The last holder exited without releasing it. Saves replace the file whole, so the
            // file is intact, and the lock is now ours.
        }
        catch
        {
            mutex.Dispose();
            throw;
        }
        return new CrossProcessLock(mutex);
    }

    public void Dispose()
    {
        try
        {
            _mutex.ReleaseMutex();
        }
        finally
        {
            _mutex.Dispose();
        }
    }
}
