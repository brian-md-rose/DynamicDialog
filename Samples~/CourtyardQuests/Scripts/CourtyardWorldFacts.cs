using UnityEngine;

namespace DynamicDialog.Samples.Courtyard
{
    /// <summary>
    /// World facts from code, on the Dialog Director: <c>player_holding</c> is the tag of whatever the player
    /// carries, or "nothing". Rules check it like any other fact (<c>if player_holding == crutch</c>).
    /// </summary>
    /// <remarks>
    /// Any component on the Dialog Director (or on a speaker) that implements <see cref="IFactProvider"/> adds its
    /// facts to every query, so they are always current without being written into memory.
    /// </remarks>
    public class CourtyardWorldFacts : MonoBehaviour, IFactProvider
    {
        static readonly Symbol PlayerHolding = Symbol.Of("player_holding");

        public void AddFacts(Query query)
        {
            var carry = PlayerCarry.Instance;
            query.Set(PlayerHolding, carry != null ? carry.HeldTag : PlayerCarry.Nothing);
        }
    }
}
