namespace UI.Account
{
    /// <summary>
    /// 只存账号，没有密码
    /// </summary>
    public class AccountViewModel
    {
        // 只有账号，登录注册都用它
        public string Account { get; set; }

        // 清空数据
        public void Clear()
        {
            Account = string.Empty;
        }
    }
}