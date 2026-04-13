using UnityEngine;
using System;
using Constants;

[Serializable]
public class PlayerData {
    private static bool s_mIsInit = false;
    
    #region Structs
    [Serializable]
    public class PlayerBasicInfo {
        public string uid;
        public string name;
        public AvatarColorID color;
        
        public PlayerBasicInfo(string uid = null, string name = null, AvatarColorID color = 0) {
            this.uid = uid;
            this.name = name;
            this.color = color;
        }
    }
    #endregion
    
    #region Player Data Fields
    private static PlayerData s_instance;
    public static PlayerData SInstance {
        get {
            if (s_instance == null) {
                Debug.LogError("PlayerData instance is not initialized!");
            }
            
            return s_instance;
        }
    }

    [SerializeField]
    public PlayerBasicInfo basicInfo;
    #endregion

    #region Methods
    public static bool IsInit() => s_mIsInit;

    public static void Init(PlayerData playerData) {
        s_mIsInit = true;
        s_instance = playerData;
    }
    
    public static void Clear() {
        s_mIsInit = false;
        s_instance = null;
    }
    #endregion
}
