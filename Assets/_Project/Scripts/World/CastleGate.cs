using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// The King's castle gate: sealed by a wall of his aura until every chimera ant camp in the woods is beaten.
    /// </summary>
    public class CastleGate : Interactable
    {
        [Tooltip("The aura wall: its collider blocks the way and it is hidden once the gate opens.")]
        [SerializeField] GameObject barrier;

        bool open;

        public bool IsOpen => open;

        public void Configure(GameObject wall) => barrier = wall;

        void Start() => Refresh(announce: false);

        void Update()
        {
            if (!open) Refresh(announce: true);
            else if (barrier != null && barrier.activeSelf) barrier.SetActive(false);
            if (barrier != null && barrier.activeSelf)
            {
                // A slow pulse so the seal reads as living aura, not a wall.
                float pulse = 1f + 0.03f * Mathf.Sin(Time.time * 2.3f);
                barrier.transform.localScale = new Vector3(barrierScale.x * pulse, barrierScale.y, barrierScale.z);
            }
        }

        Vector3 barrierScale;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (barrier != null) barrierScale = barrier.transform.localScale;
        }

        void Refresh(bool announce)
        {
            if (Encounter.SquadCount() == 0 || Encounter.SquadsLeft() > 0) return;
            open = true;
            if (barrier != null) barrier.SetActive(false);
            if (announce) Session.PushNotice("A tremor runs through the woods: the King's seal on the castle gate is gone.", 5f);
        }

        public override string GetPrompt()
        {
            if (open) return "The castle gate stands open. The King waits inside.";
            int total = Encounter.SquadCount();
            int left = Encounter.SquadsLeft();
            return $"Sealed by the King's aura — clear the chimera ant camps in the woods ({total - left}/{total} cleared).";
        }

        public override void Interact(PlayerContext player)
        {
            if (!open) Session.PushNotice($"The seal holds. {Encounter.SquadsLeft()} ant camp(s) still guard the woods.", 2.5f);
        }
    }
}
