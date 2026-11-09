using UnityEngine;

namespace DynamicDialog
{
    /// <summary>
    /// Resets the package's static state each time Play Mode starts, so it works with Enter Play Mode
    /// Options set to skip the domain reload. Without a reload, static fields and static event subscribers
    /// survive from the previous session, and so do the compiled rule sets cached on rule database assets,
    /// with their <c>once</c> and cooldown state.
    /// </summary>
    internal static class DialogStaticState
    {
        /// <summary>Increases every time Play Mode starts. Caches built in an earlier session compare against it.</summary>
        internal static int PlaySession { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetForPlayMode()
        {
            PlaySession++;
            DialogDirector.ResetStatics();
            DialogSpeaker.ResetStatics();
            DialogEvents.Clear();
            DialogPresenters.Reset();
            DialogTag.ResetStatics();
            NearbySpeakerFacts.ResetStatics();
        }
    }
}
