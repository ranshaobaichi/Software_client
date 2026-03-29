namespace UI.SampleScene
{
    public class RegisterViewModel
    {
        public string account;
        public string password;

        public RegisterViewModel()
        {
        }

        public void SetData(string account, string password)
        {
            this.account = account;
            this.password = password;
        }
    }
}
