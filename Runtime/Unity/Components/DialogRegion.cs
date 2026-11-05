using UnityEngine;

namespace DynamicDialog
{
    /// <summary>
    /// Loads a rule database, and optional region facts, while this component is enabled.
    /// Put one in each region's additive scene so its rules come and go with the level
    /// (talk slides 109-111). To stream the database itself instead of keeping it referenced by
    /// the scene, use DialogAddressableRegion (needs the Addressables package).
    /// </summary>
    [AddComponentMenu("Dynamic Dialog/Dialog Region")]
    public class DialogRegion : MonoBehaviour
    {
        [SerializeField] DialogRuleDatabase database;

        [Tooltip("World facts that exist only while this region is active.")]
        [SerializeField] RegionFacts regionFacts = new RegionFacts();

        DialogDirector addedTo;

        public DialogRuleDatabase Database => database;
        public RegionFacts Facts => regionFacts;

        void OnEnable()
        {
            var director = DialogDirector.Instance;
            if (director == null) return;
            if (database != null) director.AddDatabase(database);
            if (regionFacts != null && !regionFacts.IsEmpty) director.Engine.WorldProviders.Add(regionFacts);
            addedTo = director;
        }

        void OnDisable()
        {
            if (addedTo == null) return;
            if (database != null) addedTo.RemoveDatabase(database);
            if (addedTo.Engine != null) addedTo.Engine.WorldProviders.Remove(regionFacts);
            addedTo = null;
        }
    }
}
