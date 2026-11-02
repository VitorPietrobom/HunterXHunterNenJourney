using System;
using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// The bodies a created Hunter can wear (Gon, Killua, Kurapika, Kite — all rigged Humanoids sharing the player's
    /// controller and clips). Shows the one in the profile at its height and moves the animation and aura onto it.
    /// </summary>
    public class PlayerBodies : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            public string name;
            public Animator model;
        }

        [SerializeField] Entry[] bodies = Array.Empty<Entry>();

        Vector3[] baseScales;

        public string[] Names
        {
            get
            {
                var names = new string[bodies.Length];
                for (int i = 0; i < bodies.Length; i++) names[i] = bodies[i].name;
                return names;
            }
        }

        public void Configure(Entry[] entries) => bodies = entries ?? Array.Empty<Entry>();

        void Awake()
        {
            baseScales = new Vector3[bodies.Length];
            for (int i = 0; i < bodies.Length; i++)
                if (bodies[i].model != null) baseScales[i] = bodies[i].model.transform.localScale;
        }

        void Start()
        {
            var session = GameSession.Instance;
            if (session != null) Show(session.Progression.Hunter.body, session.Progression.Hunter.height);
        }

        /// <summary>Shows a body (by name) at a height multiplier; unknown names fall back to the first body.</summary>
        public void Show(string name, float height)
        {
            if (bodies.Length == 0) return;
            int chosen = 0;
            for (int i = 0; i < bodies.Length; i++)
                if (bodies[i].name == name) chosen = i;
            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i].model == null) continue;
                bodies[i].model.gameObject.SetActive(i == chosen);
                // Taller bodies grow from the feet: the model's origin is on the ground.
                bodies[i].model.transform.localScale = baseScales[i] * Mathf.Clamp(height, 0.85f, 1.2f);
            }
            var model = bodies[chosen].model;
            if (TryGetComponent(out PlayerAnimator animation)) animation.UseModel(model);
            if (TryGetComponent(out AuraVisual aura)) aura.Rebind(model);
        }
    }
}
