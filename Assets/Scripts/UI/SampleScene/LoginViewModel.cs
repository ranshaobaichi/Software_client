namespace UI.SampleScene
{
    public class LoginViewModel
    {
        // 必须能读能写！！
        public string Account { get; set; }
        public string Password { get; set; }

        public void SetData(string account, string password)
        {
            Account = account;
            Password = password;
        }
    }
}