using System.Collections.Generic;
using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// The soundtrack follows the moment: calm music while exploring and training, fight music once anything turns on
    /// the player (arena matches included), the heaviest tracks when the King rises. Each mood keeps its own player, so
    /// leaving a fight crossfades back into the calm track where it left off. Tracks come from Resources/Music/&lt;Mood&gt;/
    /// (made locally by Audio/split_soundtrack.py.txt); a mood with no tracks stays silent.
    /// </summary>
    public class MusicDirector : MonoBehaviour
    {
        public enum Mood { Calm, Fight, Boss }

        const float FadeIn = 1.2f;
        const float FadeOutOfFight = 3f;
        // Seconds without a fight before the music calms down (so a short gap between pulls doesn't flip-flop).
        const float CalmDelay = 5f;
        const float BaseVolume = 0.55f;

        class Channel
        {
            public AudioSource source;
            public List<AudioClip> playlist = new List<AudioClip>();
            public int next;
            public float level;
        }

        readonly Dictionary<Mood, Channel> channels = new Dictionary<Mood, Channel>();
        Mood current = Mood.Calm;
        float lastFight = float.NegativeInfinity;

        public Mood Current => current;

        void Awake()
        {
            foreach (Mood mood in System.Enum.GetValues(typeof(Mood)))
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                source.ignoreListenerPause = true;
                source.volume = 0f;
                var channel = new Channel { source = source };
                channel.playlist.AddRange(Resources.LoadAll<AudioClip>("Music/" + mood));
                Shuffle(channel.playlist);
                channels[mood] = channel;
            }
        }

        static void Shuffle(List<AudioClip> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        void Update()
        {
            var target = Wanted();
            if (target != Mood.Calm) lastFight = Time.unscaledTime;
            // Fights take over at once; calm returns only after a breather.
            if (target != current && (target != Mood.Calm || Time.unscaledTime - lastFight >= CalmDelay))
                current = target;

            var session = GameSession.Instance;
            // Quieter under menus, so the music doesn't fight with reading.
            float duck = session != null && (session.Paused || session.DashboardOpen) ? 0.45f : 1f;
            float dt = Time.unscaledDeltaTime;
            foreach (var pair in channels)
            {
                var ch = pair.Value;
                if (ch.playlist.Count == 0) continue;
                bool active = pair.Key == current;
                float rate = active ? 1f / FadeIn : 1f / FadeOutOfFight;
                ch.level = Mathf.MoveTowards(ch.level, active ? 1f : 0f, rate * dt);
                ch.source.volume = ch.level * BaseVolume * GameSettings.MusicVolume * duck;

                if (active && !ch.source.isPlaying && (ch.source.clip == null || ch.source.time <= 0f || ch.source.time >= ch.source.clip.length - 0.05f))
                    PlayNext(ch);
                else if (active && !ch.source.isPlaying) ch.source.UnPause();
                else if (!active && ch.level <= 0f && ch.source.isPlaying) ch.source.Pause(); // resumes where it left off
            }
        }

        void PlayNext(Channel ch)
        {
            ch.source.clip = ch.playlist[ch.next];
            ch.next = (ch.next + 1) % ch.playlist.Count;
            ch.source.time = 0f;
            ch.source.Play();
        }

        static Mood Wanted()
        {
            var session = GameSession.Instance;
            if (session == null || session.TitleOpen) return Mood.Calm;
            foreach (var e in Encounter.All)
                if (e.IsBoss && e.Engaged) return Mood.Boss;
            foreach (var e in EnemyController.All)
                if (e.IsActive) return Mood.Fight;
            return Mood.Calm;
        }
    }
}
