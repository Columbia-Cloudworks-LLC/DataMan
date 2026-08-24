using System.Runtime.CompilerServices;

namespace DataMan.Core.Plugins.Internal;

internal static class CollectibleContextUnloader
{
    // NoInlining so the caller's stack does not pin the ALC through inlined locals.
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static WeakReference[]? UnloadAndDrop(DiscoveredSlot[]? slots)
    {
        if (slots is null)
        {
            return null;
        }

        if (slots.Length == 0)
        {
            return [];
        }

        var weaks = new WeakReference[slots.Length];
        for (var i = 0; i < slots.Length; i++)
        {
            weaks[i] = slots[i].ContextWeak;
            slots[i].Context.Unload();
        }

        return weaks;
    }

    public static bool CollectUntilDead(WeakReference[] weaks, int rounds = 10)
    {
        for (var i = 0; i < rounds; i++)
        {
            if (!AnyAlive(weaks))
            {
                return true;
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        return !AnyAlive(weaks);
    }

    private static bool AnyAlive(WeakReference[] weaks)
    {
        foreach (var weak in weaks)
        {
            if (weak.IsAlive)
            {
                return true;
            }
        }

        return false;
    }
}
