using System.Collections.Generic;
using UnityEngine;

namespace HunterAwakening
{
    /// <summary>Anything the player can use with the Interact key. Registered globally for the interactor and HUD labels.</summary>
    public abstract class Interactable : MonoBehaviour
    {
        public static readonly List<Interactable> All = new List<Interactable>();

        [SerializeField] protected string displayName = "Interactable";
        [SerializeField] protected float interactRadius = 2.5f;

        public string DisplayName => displayName;
        public float InteractRadius => interactRadius;
        public virtual Vector3 Position => transform.position;

        /// <summary>While busy, the interactor keeps routing input here regardless of distance.</summary>
        public virtual bool IsBusy => false;

        public abstract string GetPrompt();
        public abstract void Interact(PlayerContext player);
        public virtual void Adjust(int delta) { }

        protected GameSession Session => GameSession.Instance;
        protected ProgressionService Progression => GameSession.Instance.Progression;

        protected virtual void OnEnable() => All.Add(this);
        protected virtual void OnDisable() => All.Remove(this);
    }
}
