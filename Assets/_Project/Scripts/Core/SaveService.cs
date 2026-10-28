using System;
using System.IO;
using UnityEngine;

namespace HunterAwakening
{
    /// <summary>Versioned local JSON save. Loading never simulates elapsed real time (no offline progress).</summary>
    public class SaveService
    {
        public SaveService(string fileName = "hunter_awakening_save.json")
        {
            FilePath = Path.Combine(Application.persistentDataPath, fileName);
        }

        public string FilePath { get; }
        public bool Exists => File.Exists(FilePath);

        public bool TryLoad(out PlayerState state, out string error)
        {
            state = null;
            error = null;
            if (!Exists) return false;
            try
            {
                state = JsonUtility.FromJson<PlayerState>(File.ReadAllText(FilePath));
                if (state == null) throw new InvalidDataException("empty save file");
                Migrate(state);
                state.Sanitize();
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                state = null;
                try { File.Copy(FilePath, FilePath + ".corrupt", true); } catch { /* best effort backup */ }
                Debug.LogWarning($"[SaveService] Failed to load {FilePath}: {e}");
                return false;
            }
        }

        public void Save(PlayerState state)
        {
            state.schemaVersion = PlayerState.CurrentSchemaVersion;
            state.lastSavedUtc = DateTime.UtcNow.ToString("o");
            string json = JsonUtility.ToJson(state, true);
            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, json);
            File.Copy(temp, FilePath, true);
            File.Delete(temp);
        }

        public void Delete()
        {
            if (Exists) File.Delete(FilePath);
        }

        public static void Migrate(PlayerState state)
        {
            if (state.schemaVersion <= 0) state.schemaVersion = 1; // pre-versioned saves share the v1 layout
            if (state.schemaVersion > PlayerState.CurrentSchemaVersion)
                Debug.LogWarning($"[SaveService] Save schema v{state.schemaVersion} is newer than supported v{PlayerState.CurrentSchemaVersion}; loading what is recognised.");
            // Future: if (state.schemaVersion < 2) { ...upgrade v1 -> v2...; state.schemaVersion = 2; }
        }
    }
}
