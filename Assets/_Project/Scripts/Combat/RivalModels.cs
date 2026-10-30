using System;
using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// Gives each opponent tier its own fighter (Gon, Kurapika, Hisoka, Meruem). All models share the Humanoid
    /// controller and clips, so switching only swaps which one is shown and driven. Tiers without a model use the
    /// default one.
    /// </summary>
    [RequireComponent(typeof(EnemyAnimator))]
    public class RivalModels : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            public string tierId;
            public Animator model;
        }

        [SerializeField] Animator defaultModel;
        [SerializeField] Entry[] models = Array.Empty<Entry>();

        public Animator Current { get; private set; }

        /// <summary>Set by the scene builder.</summary>
        public void Configure(Animator fallback, Entry[] entries)
        {
            defaultModel = fallback;
            models = entries ?? Array.Empty<Entry>();
        }

        public void Show(string tierId)
        {
            Animator chosen = defaultModel;
            foreach (var entry in models)
                if (entry.model != null && entry.tierId == tierId) chosen = entry.model;

            if (defaultModel != null) defaultModel.gameObject.SetActive(chosen == defaultModel);
            foreach (var entry in models)
                if (entry.model != null) entry.model.gameObject.SetActive(entry.model == chosen);
            Current = chosen;
            GetComponent<EnemyAnimator>().UseModel(chosen);
        }
    }
}
