using System;
using System.IO;
using UnityEngine;

namespace ProjectMayham.Core
{
    /// <summary>
    /// The state that outlives a scene: money, the stash, what the player carries, trader stock. It lives in memory
    /// while the game runs and is written to a JSON file whenever the scene changes (and on request). Scene objects
    /// that own a part of the state subscribe to <see cref="SaveRequested"/> and copy it into <see cref="Data"/>
    /// before the file is written.
    /// </summary>
    public static class GameSession
    {
        private const string FileName = "save.json";

        private static SaveData data;

        /// <summary>Raised right before the data is written: owners of state copy it into <see cref="Data"/>.</summary>
        public static event Action SaveRequested;
        public static event Action<int> MoneyChanged;

        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);
        public static bool HasSave => File.Exists(SavePath);

        /// <summary>The current state; loaded from the save file (or a fresh game) on first use.</summary>
        public static SaveData Data
        {
            get
            {
                if (data == null && !Load()) data = new SaveData();
                return data;
            }
        }

        public static int Money => Data.money;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            data = null;
            SaveRequested = null;
            MoneyChanged = null;
        }

        /// <summary>Starts over with a fresh state and overwrites the save file.</summary>
        public static void NewGame()
        {
            data = new SaveData();
            WriteFile();
        }

        /// <summary>Reads the save file. Returns false (and keeps the current state) when there is none or it is broken.</summary>
        public static bool Load()
        {
            try
            {
                if (!HasSave) return false;
                var loaded = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
                if (loaded == null) return false;
                data = loaded;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Could not read the save file: {e.Message}");
                return false;
            }
        }

        /// <summary>Collects the state of the scene and writes the save file.</summary>
        public static void Save()
        {
            if (data == null) return;
            SaveRequested?.Invoke();
            WriteFile();
        }

        public static void AddMoney(int amount)
        {
            if (amount == 0) return;
            Data.money = Mathf.Max(0, Data.money + amount);
            MoneyChanged?.Invoke(Data.money);
        }

        /// <summary>Takes the money if there is enough of it.</summary>
        public static bool TrySpend(int amount)
        {
            if (amount < 0 || Data.money < amount) return false;
            AddMoney(-amount);
            return true;
        }

        private static void WriteFile()
        {
            try
            {
                File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
            }
            catch (Exception e)
            {
                Debug.LogError($"Could not write the save file: {e.Message}");
            }
        }
    }
}
