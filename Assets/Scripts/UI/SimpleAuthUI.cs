using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Network.Messages;

public class SimpleAuthUI : MonoBehaviour
{
    [Header("Login Panel")]
    public GameObject loginPanel;
    public TMP_InputField loginUsername;
    public TMP_InputField loginPassword;
    public Button loginButton;
    
    [Header("Register Panel")]
    public GameObject registerPanel;
    public TMP_InputField registerUsername;
    public TMP_InputField registerPassword;
    public Button registerButton;
    
    [Header("Common")]
    public Button switchModeButton;
    public TextMeshProUGUI messageText;
    
    [Header("UI Manager")]
    public UIManager uiManager;
    
    private bool isLoginMode = true;
    private bool isConnecting = false;
    
    private string serverHost = "127.0.0.1";
    private int serverPort = 8888;
    
    void Start()
    {
        // 绑定按钮事件
        loginButton.onClick.AddListener(OnLoginClick);
        registerButton.onClick.AddListener(OnRegisterClick);
        switchModeButton.onClick.AddListener(OnSwitchMode);
        
        // 显示登录界面
        ShowLoginMode();
        ShowMessage("", false);
        
        // 自动查找 UIManager
        if (uiManager == null)
        {
            uiManager = FindObjectOfType<UIManager>();
        }
    }
    
    void ConnectToServer()
    {
        if (isConnecting) return;
        isConnecting = true;
        ShowMessage("Connecting to server...", false);
        
        // TODO: 实现网络连接
        // _channel = NetworkManager.SInstance.CreateConnection(serverHost, serverPort);
        // _channel.RegisterHandler<GameMessage>(OnNetworkMessage);
        // _channel.Connect();
        
        // 测试用，实际删除
        Invoke("SimulateConnectionSuccess", 0.5f);
    }
    
    void SimulateConnectionSuccess()
    {
        isConnecting = false;
        ShowMessage("Connected to server", false);
    }
    
    void OnNetworkMessage(GameMessage msg)
    {
        if (msg == null || string.IsNullOrEmpty(msg.type)) return;
        
        switch (msg.type)
        {
            case "login_response":
                break;
            case "register_response":
                break;
        }
    }
    
    void OnLoginClick()
    {
        string username = loginUsername.text;
        string password = loginPassword.text;
        
        if (string.IsNullOrEmpty(username))
        {
            ShowMessage("Please enter username", true);
            return;
        }
        
        if (string.IsNullOrEmpty(password))
        {
            ShowMessage("Please enter password", true);
            return;
        }
        
        ShowMessage("Logging in...", false);
        SendLoginRequest(username, password);
    }
    
    void OnRegisterClick()
    {
        string username = registerUsername.text;
        string password = registerPassword.text;
        
        if (string.IsNullOrEmpty(username))
        {
            ShowMessage("Please enter username", true);
            return;
        }
        
        if (username.Length < 3)
        {
            ShowMessage("Username must be at least 3 characters", true);
            return;
        }
        
        if (string.IsNullOrEmpty(password))
        {
            ShowMessage("Please enter password", true);
            return;
        }
        
        if (password.Length < 6)
        {
            ShowMessage("Password must be at least 6 characters", true);
            return;
        }
        
        ShowMessage("Registering...", false);
        SendRegisterRequest(username, password);
    }
    
    void SendLoginRequest(string username, string password)
    {
        // TODO: 发送登录请求
        // var request = new LoginRequest { username = username, password = password };
        // _channel?.Send(request);
        
        // 测试用，实际删除
        SimulateLoginSuccess(username);
    }
    
    void SendRegisterRequest(string username, string password)
    {
        // TODO: 发送注册请求
        // var request = new RegisterRequest { username = username, password = password };
        // _channel?.Send(request);
        
        // 测试用，实际删除
        SimulateRegisterSuccess(username);
    }
    
    void OnLoginResponse(LoginResponse response)
    {
        if (response.success)
        {
            ShowMessage($"Login success! Welcome {response.username}", false);
            PlayerPrefs.SetString("uid", response.uid);
            PlayerPrefs.SetString("username", response.username);
            PlayerPrefs.SetString("token", response.token);
            PlayerPrefs.Save();
            OnAuthSuccess();
        }
        else
        {
            ShowMessage(response.message, true);
        }
    }
    
    void OnRegisterResponse(RegisterResponse response)
    {
        if (response.success)
        {
            ShowMessage($"Register success! Welcome {response.username}", false);
            OnSwitchMode();
            loginUsername.text = response.username;
        }
        else
        {
            ShowMessage(response.message, true);
        }
    }
    
    void OnSwitchMode()
    {
        isLoginMode = !isLoginMode;
        
        if (isLoginMode)
        {
            ShowLoginMode();
            switchModeButton.GetComponentInChildren<TextMeshProUGUI>().text = "No account? Register";
        }
        else
        {
            ShowRegisterMode();
            switchModeButton.GetComponentInChildren<TextMeshProUGUI>().text = "Already have an account? Log in";
        }
        
        ClearInputs();
        ShowMessage("", false);
    }
    
    void ShowLoginMode()
    {
        loginPanel.SetActive(true);
        registerPanel.SetActive(false);
    }
    
    void ShowRegisterMode()
    {
        loginPanel.SetActive(false);
        registerPanel.SetActive(true);
    }
    
    void ClearInputs()
    {
        loginUsername.text = "";
        loginPassword.text = "";
        registerUsername.text = "";
        registerPassword.text = "";
    }
    
    void ShowMessage(string msg, bool isError)
    {
        messageText.text = msg;
        messageText.color = isError ? Color.red : Color.green;
    }
    
    void OnAuthSuccess()
    {
        Debug.Log("[Auth] Authentication success, showing start panel");
        
        // 隐藏登录界面
        gameObject.SetActive(false);
        
        // 通过 UIManager 显示开始界面
        if (uiManager != null)
        {
            uiManager.ShowStartPanel();
        }
        else
        {
            Debug.LogWarning("[Auth] UIManager not found, trying to find StartUI directly");
            // 直接查找开始界面
            var startUI = FindObjectOfType<StartUI>();
            if (startUI != null)
            {
                startUI.gameObject.SetActive(true);
            }
            else
            {
                Debug.LogError("[Auth] StartUI not found!");
            }
        }
        
        // 启动游戏客户端
        var gameClient = FindObjectOfType<SimpleNetworkClient2D>();
        if (gameClient == null)
        {
            GameObject go = new GameObject("GameClient");
            gameClient = go.AddComponent<SimpleNetworkClient2D>();
        }
    }
    
    // 测试用方法，实际删除
    void SimulateLoginSuccess(string username)
    {
        var response = new LoginResponse
        {
            success = true,
            message = "Login success",
            uid = System.Guid.NewGuid().ToString(),
            username = username,
            token = "test_token"
        };
        OnLoginResponse(response);
    }
    
    void SimulateRegisterSuccess(string username)
    {
        var response = new RegisterResponse
        {
            success = true,
            message = "Register success",
            uid = System.Guid.NewGuid().ToString(),
            username = username
        };
        OnRegisterResponse(response);
    }
    
    void OnDestroy()
    {
        // 断开连接
        // if (_channel != null) _channel.Disconnect();
    }
}