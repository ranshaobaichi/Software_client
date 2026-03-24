using System;

namespace Network.Messages
{
    /// <summary>
    /// 基础认证消息
    /// </summary>
    [Serializable]
    public class AuthMessage
    {
        public string type;
        public bool success;
        public string message;
    }
    
    /// <summary>
    /// 登录请求
    /// </summary>
    [Serializable]
    public class LoginRequest
    {
        public string type = "login_request";
        public string username;
        public string password;
    }
    
    /// <summary>
    /// 登录响应
    /// </summary>
    [Serializable]
    public class LoginResponse : AuthMessage
    {
        public string uid;
        public string username;
        public string token;
        
        public LoginResponse()
        {
            type = "login_response";
        }
    }
    
    /// <summary>
    /// 注册请求
    /// </summary>
    [Serializable]
    public class RegisterRequest
    {
        public string type = "register_request";
        public string username;
        public string password;
    }
    
    /// <summary>
    /// 注册响应
    /// </summary>
    [Serializable]
    public class RegisterResponse : AuthMessage
    {
        public string uid;
        public string username;
        
        public RegisterResponse()
        {
            type = "register_response";
        }
    }
}