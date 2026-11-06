using UnityEngine;

namespace DynamicDialog
{
    /// <summary>
    /// Base class for presenter components: registers with <see cref="DialogPresenters"/> while enabled.
    /// Derive from it to make your own dialog UI.
    /// <code>
    /// public class MyCaptions : DialogPresenterBehaviour
    /// {
    ///     public override void OnLineStarted(DialogPresentedLine line) { /* show line.Text */ }
    ///     public override void OnLineEnded(DialogPresentedLine line) { /* hide it */ }
    /// }
    /// </code>
    /// </summary>
    public abstract class DialogPresenterBehaviour : MonoBehaviour, IDialogPresenter
    {
        /// <summary>Registers this presenter. Call the base method if you override it.</summary>
        protected virtual void OnEnable() => DialogPresenters.Register(this);

        /// <summary>Unregisters this presenter. Call the base method if you override it.</summary>
        protected virtual void OnDisable() => DialogPresenters.Unregister(this);

        /// <inheritdoc/>
        public abstract void OnLineStarted(DialogPresentedLine line);

        /// <inheritdoc/>
        public abstract void OnLineEnded(DialogPresentedLine line);

        /// <summary>
        /// A line was stopped early. Hide it, usually faster than <see cref="OnLineEnded"/>.
        /// By default this calls <see cref="OnLineEnded"/>.
        /// </summary>
        /// <param name="line">The same value <see cref="OnLineStarted"/> received.</param>
        public virtual void OnLineInterrupted(DialogPresentedLine line) => OnLineEnded(line);
    }
}
