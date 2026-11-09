using System;
using DynamicDialog;
using UnityEngine;

namespace DynamicDialog.Samples.Courtyard
{
    /// <summary>
    /// Game logic that reacts to dialog through the game events rules raise with <c>do</c>, so renaming a rule
    /// never breaks a quest. In CourtyardQuests.dlg:
    /// <list type="bullet">
    /// <item><c>do GiveItem crutch</c>: the player hands over what they're carrying (if it is the named item)
    /// to the speaker's hand.</item>
    /// <item><c>do StartWalking at end</c>: the speaker's NpcBrain starts walking once the line has played.</item>
    /// </list>
    /// The rules themselves record the outcome in world memory.
    /// </summary>
    public class QuestHooks : MonoBehaviour
    {
        [Serializable]
        public class HandOff
        {
            public DialogSpeaker receiver;
            [Tooltip("Where the item ends up. Defaults to the receiver.")]
            public Transform hand;
        }

        [Tooltip("Where each character holds what they are given. Characters not listed hold it at their root.")]
        public HandOff[] handOffs = Array.Empty<HandOff>();

        IDisposable giveItem, startWalking;

        void OnEnable()
        {
            giveItem = DialogEvents.On("GiveItem", OnGiveItem);
            startWalking = DialogEvents.On("StartWalking", OnStartWalking);
        }

        void OnDisable()
        {
            giveItem?.Dispose();
            startWalking?.Dispose();
        }

        void OnGiveItem(DialogSpeaker speaker, DialogEvent e)
        {
            var carry = PlayerCarry.Instance;
            if (speaker == null || carry == null || carry.Held == null) return;
            string item = e.Arg(0);
            if (!string.IsNullOrEmpty(item) && carry.HeldTag != Symbol.Of(item)) return;
            carry.GiveAway(HandOf(speaker));
        }

        void OnStartWalking(DialogSpeaker speaker, DialogEvent e)
        {
            var brain = speaker != null ? speaker.GetComponent<NpcBrain>() : null;
            if (brain != null) brain.canWalk = true;
        }

        Transform HandOf(DialogSpeaker speaker)
        {
            foreach (var h in handOffs)
                if (h != null && h.receiver == speaker && h.hand != null) return h.hand;
            return speaker.transform;
        }
    }
}
