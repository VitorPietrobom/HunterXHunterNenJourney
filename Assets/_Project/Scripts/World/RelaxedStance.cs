using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// Holds a Humanoid in a calm standing pose — arms down, elbows soft — with a slow breath, instead of the shared
    /// fighting-stance idle. Poses the skeleton directly through Unity's muscle space, so no animation clip is needed.
    /// </summary>
    public class RelaxedStance : MonoBehaviour
    {
        [SerializeField] Animator animator;

        HumanPoseHandler handler;
        HumanPose rest, pose;
        int chest, spine, head;
        float phase;

        public void Configure(Animator model) => animator = model;

        void Start()
        {
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                enabled = false;
                return;
            }
            animator.enabled = false; // the pose below replaces the animation
            handler = new HumanPoseHandler(animator.avatar, animator.transform);
            handler.GetHumanPose(ref rest);
            foreach (var side in new[] { "Left", "Right" })
            {
                Set(ref rest, $"{side} Shoulder Down-Up", -0.1f);
                Set(ref rest, $"{side} Arm Down-Up", -0.55f);
                Set(ref rest, $"{side} Arm Front-Back", 0.15f);
                Set(ref rest, $"{side} Arm Twist In-Out", 0f);
                Set(ref rest, $"{side} Forearm Stretch", 0.75f);
            }
            chest = Index("Chest Front-Back");
            spine = Index("Spine Front-Back");
            head = Index("Head Nod Down-Up");
            pose = rest;
            pose.muscles = (float[])rest.muscles.Clone();
            phase = Random.value * 10f;

            // Some exports (VRoid) face away from their root once posed this way: check the hips and turn around.
            handler.SetHumanPose(ref rest);
            Vector3 left = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position;
            Vector3 right = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg).position;
            Vector3 facing = Vector3.Cross(right - left, Vector3.up);
            if (Vector3.Dot(facing, transform.forward) < 0f)
                animator.transform.localRotation *= Quaternion.Euler(0f, 180f, 0f);
        }

        void OnDestroy() => handler?.Dispose();

        static int Index(string muscle) => System.Array.IndexOf(HumanTrait.MuscleName, muscle);

        static void Set(ref HumanPose p, string muscle, float value)
        {
            int i = Index(muscle);
            if (i >= 0) p.muscles[i] = value;
        }

        void LateUpdate()
        {
            phase += Time.deltaTime;
            float breath = Mathf.Sin(phase * 1.6f);
            System.Array.Copy(rest.muscles, pose.muscles, rest.muscles.Length);
            if (chest >= 0) pose.muscles[chest] += 0.04f * breath;
            if (spine >= 0) pose.muscles[spine] += 0.02f * breath;
            if (head >= 0) pose.muscles[head] += 0.03f * Mathf.Sin(phase * 0.5f);
            handler.SetHumanPose(ref pose);
        }
    }
}
