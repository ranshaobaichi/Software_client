using UnityEngine;
using System;
using System.IO;

[Serializable]
public class LocalizeData {
    private const string FileName = "localize_data.json";
    private static bool m_isInit = false;
    private static bool m_isDirty = false;
    private static string m_lastSavedSnapshot = string.Empty;

    #region Structs
    [Serializable]
    public class PlayerInfo {
        public string lastLoginUid = string.Empty;
    }
    #endregion

    #region Singleton
    private static volatile LocalizeData s_instance;

    public static LocalizeData SInstance {
        get {
            if (s_instance == null) {
                InitIfNot();
            }

            return s_instance;
        }
    }
    #endregion

    #region Player Data Fields
    [SerializeField]
    public PlayerInfo playerInfo;
    #endregion

    #region Methods
    public static void InitIfNot() {
        if (m_isInit) {
            return;
        }
        
        if (!TryLoadData()) {
            // Load failed, create new instance with default values 
            Debug.LogError("[LocalizeData] Failed to load data, initialized with default values.");
            s_instance = new LocalizeData {
                playerInfo = new PlayerInfo()
            };
            m_isDirty = true;
            SaveData();
        }
        
        m_isInit = true;
    }
  
    private static bool TryLoadData() {
        var filePath = GetFilePath();
        if (!File.Exists(filePath)) {
            Debug.LogError($"LocalizeData file not found at {filePath}. ");
            return false;
        }

        var jsonData = File.ReadAllText(filePath);
        if (string.IsNullOrWhiteSpace(jsonData)) {
            Debug.LogError($"LocalizeData file empty at {filePath}. ");
            return false;
        }

        try {
            var playerData = JsonUtility.FromJson<LocalizeData>(jsonData);
            s_instance = playerData;
        }
        catch (Exception e) {
            Debug.LogError($"Failed to load LocalizeData from {filePath}: {e}");
        }

        UpdateSnapshotAfterSave();
        return s_instance != null;
    }

    public static void SaveData() {
        if (s_instance == null) {
            return;
        }

        RefreshDirtyStateBySnapshot();
        if (!m_isDirty) {
            return;
        }

        var filePath = GetFilePath();
        var directoryPath = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directoryPath)) {
            Directory.CreateDirectory(directoryPath);
        }

        var jsonData = JsonUtility.ToJson(s_instance, true);
        File.WriteAllText(filePath, jsonData);
        UpdateSnapshotAfterSave();
    }

    private static void RefreshDirtyStateBySnapshot() {
        if (s_instance == null) {
            m_isDirty = false;
            return;
        }

        var currentSnapshot = BuildSnapshot(s_instance);
        m_isDirty = !string.Equals(currentSnapshot, m_lastSavedSnapshot, StringComparison.Ordinal);
    }

    private static void UpdateSnapshotAfterSave() {
        m_lastSavedSnapshot = BuildSnapshot(s_instance);
        m_isDirty = false;
    }

    private static string BuildSnapshot(LocalizeData data) {
        return data == null ? string.Empty : JsonUtility.ToJson(data, false);
    }

    private static string GetFilePath() {
#if UNITY_EDITOR
        const string editorFilePath = "Assets/Resources/";
        return Path.Combine(editorFilePath, FileName);
#else
        return Path.Combine(Application.persistentDataPath, FileName);
#endif
    }
    #endregion
}