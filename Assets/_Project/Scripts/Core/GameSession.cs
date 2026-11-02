using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HunterAwakening
{
    /// <summary>Scene-level composition root: owns the progression and save services and global UI state.</summary>
    [DefaultExecutionOrder(-100)]
    public class GameSession : MonoBehaviour
    {
        public class Notice
        {
            public string text;
            public float expires;
        }

        public static GameSession Instance { get; private set; }

        [SerializeField] ProgressionConfig config;
        [SerializeField] PlayerContext player;
        [SerializeField] bool loadSaveOnStart = true;
        [SerializeField] float autosaveInterval = 60f;

        readonly List<Notice> notices = new List<Notice>();
        float autosaveTimer;
        bool leavingScene;

        public ProgressionService Progression { get; private set; }
        public SaveService Saves { get; private set; }
        public PlayerContext Player => player;
        public bool DashboardOpen { get; set; }
        /// <summary>The dashboard shows the Hunter's Guide instead of progression (the notice board opens it there).</summary>
        public bool GuidePage
        {
            get => Page == DashboardPage.Guide;
            set => Page = value ? DashboardPage.Guide : DashboardPage.Progression;
        }
        public enum DashboardPage { Progression, Guide, Hatsu }
        public DashboardPage Page { get; set; }

        public void OpenGuide()
        {
            if (Paused) return;
            GuidePage = true;
            DashboardOpen = true;
        }
        /// <summary>Pause menu open: time is stopped and gameplay input ignored.</summary>
        public bool Paused { get; set; }
        /// <summary>True while any full-screen UI owns the mouse and keyboard.</summary>
        /// <summary>The launch title screen is up (see TitleScreen).</summary>
        public bool TitleOpen { get; set; }
        public bool InputBlocked => DashboardOpen || Paused || TitleOpen || DialogueBox.IsOpen || EndingOpen || CheatsOpen;
        /// <summary>The , + . cheat menu is open (see CheatMenu).</summary>
        public bool CheatsOpen { get; set; }
        /// <summary>The ending screen is up (see EndingScreen).</summary>
        public bool EndingOpen { get; set; }
        public bool ShowHelp { get; set; } = true;
        public bool IsResting { get; set; }
        public TrainingReport LastTrainingReport { get; private set; }
        public CombatReport LastCombatReport { get; private set; }
        public IReadOnlyList<Notice> Notices => notices;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (config == null) config = ProgressionConfig.CreateRuntimeDefault();

            Saves = new SaveService();
            PlayerState state = null;
            string error = null;
            if (loadSaveOnStart && Saves.TryLoad(out state, out error))
                PushNotice("Save loaded. (No progress is gained while the game is closed.)");
            else
            {
                if (error != null) PushNotice("Save could not be loaded (backed up as .corrupt): " + error, 8f);
                state = PlayerState.CreateNew();
            }

            Progression = new ProgressionService(config, state);
            Progression.ExerciseApplied += OnExerciseApplied;
            if (GetComponent<HitFeedback>() == null) gameObject.AddComponent<HitFeedback>();
            if (GetComponent<ImpactEffects>() == null) gameObject.AddComponent<ImpactEffects>();
            if (GetComponent<DamageNumbers>() == null) gameObject.AddComponent<DamageNumbers>();
            if (GetComponent<TitleScreen>() == null) gameObject.AddComponent<TitleScreen>();
            if (GetComponent<MusicDirector>() == null) gameObject.AddComponent<MusicDirector>();
            if (GetComponent<DialogueBox>() == null) gameObject.AddComponent<DialogueBox>();
            if (GetComponent<EndingScreen>() == null) gameObject.AddComponent<EndingScreen>();
            if (GetComponent<CheatMenu>() == null) gameObject.AddComponent<CheatMenu>();
            Progression.Notice += text => PushNotice(text, 5f);
        }

        void Start()
        {
            var s = Progression.State;
            if (player != null && s.hasPosition) player.Motor.Teleport(s.position, s.yaw);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Progression.TickActiveTime(dt, IsResting);
            notices.RemoveAll(n => Time.unscaledTime > n.expires);

            var input = player != null ? player.Input : null;
            // Typing in a text field (the Hatsu editor, a name) mustn't trigger the hotkeys.
            bool typing = GUIUtility.keyboardControl != 0;
            if (input != null && !TitleOpen && !DialogueBox.IsOpen && !EndingOpen && !CheatsOpen && !typing)
            {
                if (input.PausePressed) SetPaused(!Paused);
                if (input.DashboardPressed && !Paused) DashboardOpen = !DashboardOpen;
                if (input.HelpPressed) ShowHelp = !ShowHelp;
                if (input.SavePressed && Save()) PushNotice("Game saved.");
                if (input.LoadPressed) ReloadFromDisk();
            }

            autosaveTimer += dt;
            if (autosaveTimer >= autosaveInterval)
            {
                autosaveTimer = 0f;
                Save();
            }
        }

        void OnApplicationQuit() => Save();

        public void SetPaused(bool paused)
        {
            Paused = paused;
            if (paused) DashboardOpen = false;
            Time.timeScale = paused ? 0f : 1f; // HitFeedback takes timeScale back over once unpaused
        }

        public bool Save()
        {
            if (leavingScene || Progression == null) return false;
            if (player != null) Progression.CapturePosition(player.transform.position, player.transform.eulerAngles.y);
            try
            {
                Saves.Save(Progression.State);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                PushNotice("Save failed: " + e.Message, 8f);
                return false;
            }
        }

        /// <summary>Discards unsaved progress and reloads the scene from the save file.</summary>
        public void ReloadFromDisk()
        {
            if (!Saves.Exists)
            {
                PushNotice("No save file yet.");
                return;
            }
            leavingScene = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>Starts over with a fresh character (the pause menu's Reset progress). Settings are kept.</summary>
        public void DeleteSaveAndRestart()
        {
            leavingScene = true;
            SetPaused(false); // timeScale survives the reload
            Saves.Delete();
            PlayerPrefs.DeleteKey(ArenaController.TierPref);
            TitleScreen.RequestNewGame();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public event Action<CombatReport> CombatReported;

        public void ReportCombat(CombatReport report)
        {
            LastCombatReport = report;
            CombatReported?.Invoke(report);
            Progression.RecordCombat(report.tierId, report.won, report.duration, report.headline);
            PushNotice(report.headline + " — open the dashboard (Tab) for analysis.", 6f);
            Save();
        }

        void OnExerciseApplied(TrainingReport report)
        {
            LastTrainingReport = report;
            PushNotice(report.Headline, 5f);
            Save();
        }

        public void PushNotice(string text, float duration = 4f)
        {
            notices.Add(new Notice { text = text, expires = Time.unscaledTime + duration });
            if (notices.Count > 6) notices.RemoveAt(0);
        }
    }
}
