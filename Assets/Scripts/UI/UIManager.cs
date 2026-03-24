using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("UI Panels")]
    public GameObject authPanel;
    public GameObject startPanel;

    void Awake()
    {
        // 单例模式
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // 初始显示登录界面
        ShowAuthPanel();
        HideStartPanel();
    }

    public void ShowAuthPanel()
    {
        if (authPanel != null) authPanel.SetActive(true);
        if (startPanel != null) startPanel.SetActive(false);
    }

    public void ShowStartPanel()
    {
        if (authPanel != null) authPanel.SetActive(false);
        if (startPanel != null) startPanel.SetActive(true);
    }

    public void HideAuthPanel()
    {
        if (authPanel != null) authPanel.SetActive(false);
    }

    public void HideStartPanel()
    {
        if (startPanel != null) startPanel.SetActive(false);
    }

    // 登录成功后调用
    public void OnLoginSuccess()
    {
        ShowStartPanel();
    }
}