namespace UI.Account
{

    public class AccountViewModel
    {

        public string Account { get; set; }

        // 清空数据
        public void Clear()
        {
            Account = string.Empty;
        }
    }
}