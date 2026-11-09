using System.Collections.Generic;
using DynamicDialog;
using UnityEngine;

namespace DynamicDialog.Samples.Courtyard
{
    /// <summary>A small prop the player can pick up with F. Its dialog name comes from its <see cref="DialogTag"/>.</summary>
    [RequireComponent(typeof(DialogTag))]
    public class Carryable : MonoBehaviour
    {
        static readonly List<Carryable> all = new List<Carryable>();
        public static IReadOnlyList<Carryable> All => all;

        public bool CanPickUp = true;
        [Tooltip("Local offset and rotation while held.")]
        public Vector3 heldPosition;
        public Vector3 heldEuler;

        DialogTag dialogTag;
        Collider[] colliders;

        public Symbol Tag => dialogTag.Tag;

        void Awake()
        {
            dialogTag = GetComponent<DialogTag>();
            colliders = GetComponentsInChildren<Collider>();
        }

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        public void AttachTo(Transform holder)
        {
            CanPickUp = false;
            foreach (var c in colliders) c.enabled = false;
            transform.SetParent(holder, false);
            transform.localPosition = heldPosition;
            transform.localRotation = Quaternion.Euler(heldEuler);
        }

        public void PlaceAt(Vector3 position, Quaternion rotation)
        {
            transform.SetParent(null, true);
            transform.SetPositionAndRotation(position, rotation);
            foreach (var c in colliders) c.enabled = true;
            CanPickUp = true;
        }
    }
}
