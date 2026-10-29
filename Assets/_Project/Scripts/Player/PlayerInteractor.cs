using UnityEngine;

namespace HunterAwakening
{
    /// <summary>Finds the nearest interactable and routes Interact / intensity-adjust input to it.</summary>
    public class PlayerInteractor : MonoBehaviour
    {
        PlayerContext context;

        public Interactable Focus { get; private set; }

        void Awake() => context = GetComponent<PlayerContext>();

        void Update()
        {
            Focus = FindFocus();
            if (Focus == null || GameSession.Instance.InputBlocked || context.Combat.IsDefeated) return;

            var input = context.Input;
            if (input.InteractPressed) Focus.Interact(context);
            if (!Focus.IsBusy)
            {
                if (input.PreviousPressed) Focus.Adjust(-1);
                if (input.NextPressed) Focus.Adjust(1);
            }
        }

        Interactable FindFocus()
        {
            foreach (var i in Interactable.All)
                if (i.IsBusy) return i;

            Interactable best = null;
            float bestDistance = float.MaxValue;
            Vector3 p = transform.position;
            foreach (var i in Interactable.All)
            {
                Vector3 delta = i.Position - p;
                delta.y = 0f;
                float d = delta.magnitude;
                if (d <= i.InteractRadius && d < bestDistance)
                {
                    best = i;
                    bestDistance = d;
                }
            }
            return best;
        }
    }
}
