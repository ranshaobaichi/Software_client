using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StartUI : MonoBehaviour
{
    [Header("UI Components")]
    public GameObject startPanel;
    public Button startButton;
    public Button settingsButton;
    public Button quitButton;
    public TextMeshProUGUI versionText;
    
    [Header("Settings")]
    public string gameSceneName = "GameScene";
    public string version = "v1.0.0";
    
    void Start()
    {
        startButton.onClick.AddListener(OnStartClick);
        settingsButton.onClick.AddListener(OnSettingsClick);
        quitButton.onClick.AddListener(OnQuitClick);
        
        if (versionText != null)
        {
            versionText.text = version;
        }
        
        ShowStartPanel();
    }
    
    void OnStartClick()
    {
        Debug.Log("[StartUI] Start game clicked");
        // 加载游戏场景
        // UnityEngine.SceneManagement.SceneManager.LoadScene(gameSceneName);
        
        // 或者隐藏开始界面，显示游戏界面
        HideStartPanel();
        
        // 触发游戏开始事件
        OnGameStart();
    }
    
    void OnSettingsClick()
    {
        Debug.Log("[StartUI] Settings clicked");
        // TODO: 打开设置界面
        // SettingsUI.Show();
    }
    
    void OnQuitClick()
    {
        Debug.Log("[StartUI] Quit game clicked");
        
        #if UNITY_EDITOR
            // 编辑器模式下停止运行
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            // 打包后退出游戏
            Application.Quit();
        #endif
    }
    
    void ShowStartPanel()
    {
        if (startPanel != null)
            startPanel.SetActive(true);
    }
    
    void HideStartPanel()
    {
        if (startPanel != null)
            startPanel.SetActive(false);
    }
    
    void OnGameStart()
    {
        Debug.Log("[StartUI] Game starting...");
        // 可以在这里初始化游戏管理器等
        // GameManager.Instance.StartGame();
    }
}