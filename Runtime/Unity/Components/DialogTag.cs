using System.Collections.Generic;
using UnityEngine;

namespace DynamicDialog
{
    /// <summary>
    /// Marks an object that characters can remark on. Its tag becomes the "object" fact of an
    /// OnSee query (talk slides 28-29).
    /// </summary>
    [AddComponentMenu("Dynamic Dialog/Dialog Tag")]
    public class DialogTag : MonoBehaviour
    {
        static readonly List<DialogTag> all = new List<DialogTag>();

        [Tooltip("The name rules test, e.g. 'barrel' or 'soda_can'. Empty uses the GameObject name.")]
        [SerializeField] string tagName;

        /// <summary>Where speakers aim for line of sight. Empty uses this transform.</summary>
        [Tooltip("Where speakers aim at for line of sight. Empty uses this transform.")]
        public Transform focus;

        /// <summary>Every enabled tag.</summary>
        public static IReadOnlyList<DialogTag> All => all;

        /// <summary>The name rules test: Tag Name, or the GameObject name when that is empty.</summary>
        public Symbol Tag => Symbol.Of(string.IsNullOrWhiteSpace(tagName) ? gameObject.name : tagName.Trim());
        /// <summary>Where speakers look at: <see cref="focus"/>, or this transform.</summary>
        public Vector3 FocusPosition => focus != null ? focus.position : transform.position;

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);
    }
}
