using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace DynamicDialog
{
    /// <summary>
    /// Like <see cref="DialogRegion"/>, but streams its rule database through Addressables, so the
    /// rules (and their audio clips) stay on disk until the region is entered and are released when
    /// it's left (talk slide 110). Only compiled when the Addressables package is installed.
    /// </summary>
    [AddComponentMenu("Dynamic Dialog/Dialog Addressable Region")]
    public class DialogAddressableRegion : MonoBehaviour
    {
        [Tooltip("The region's rule database (a .dlg, spreadsheet or Rule Database asset marked Addressable).")]
        [SerializeField] AssetReferenceT<DialogRuleDatabase> database;

        [Tooltip("World facts that exist only while this region is loaded.")]
        [SerializeField] RegionFacts regionFacts = new RegionFacts();

        AsyncOperationHandle<DialogRuleDatabase> handle;
        DialogRuleDatabase loaded;
        DialogDirector addedTo;

        /// <summary>Raised when the database has loaded and its rules are active.</summary>
        public event Action<DialogAddressableRegion> Loaded;

        /// <summary>The database has loaded and its rules are active.</summary>
        public bool IsLoaded => loaded != null;
        /// <summary>The database is still loading.</summary>
        public bool IsLoading => handle.IsValid() && !handle.IsDone;
        /// <summary>The loaded database, or null.</summary>
        public DialogRuleDatabase LoadedDatabase => loaded;
        /// <summary>The world facts this region adds while enabled.</summary>
        public RegionFacts Facts => regionFacts;

        /// <summary>Sets the database from code. Takes effect the next time the component is enabled.</summary>
        public void SetDatabase(AssetReferenceT<DialogRuleDatabase> reference) => database = reference;

        void OnEnable()
        {
            var director = DialogDirector.Instance;
            if (director == null) return;
            addedTo = director;
            if (regionFacts != null && !regionFacts.IsEmpty) director.Engine.WorldProviders.Add(regionFacts);

            if (database == null || !database.RuntimeKeyIsValid())
            {
                Debug.LogWarning($"[DynamicDialog] {name}: no Addressable rule database is assigned.", this);
                return;
            }
            handle = Addressables.LoadAssetAsync<DialogRuleDatabase>(database.RuntimeKey);
            handle.Completed += OnCompleted;
        }

        void OnCompleted(AsyncOperationHandle<DialogRuleDatabase> op)
        {
            // Disabled while loading: OnDisable already released this handle.
            if (!op.Equals(handle) || addedTo == null) return;

            if (op.Status != AsyncOperationStatus.Succeeded || op.Result == null)
            {
                Debug.LogError($"[DynamicDialog] {name}: couldn't load rule database {database.RuntimeKey}: {op.OperationException?.Message}", this);
                return;
            }
            loaded = op.Result;
            addedTo.AddDatabase(loaded);
            Loaded?.Invoke(this);
        }

        void OnDisable()
        {
            if (addedTo != null)
            {
                if (loaded != null) addedTo.RemoveDatabase(loaded);
                if (addedTo.Engine != null) addedTo.Engine.WorldProviders.Remove(regionFacts);
            }
            loaded = null;
            addedTo = null;
            if (handle.IsValid()) Addressables.Release(handle);
            handle = default;
        }
    }
}
