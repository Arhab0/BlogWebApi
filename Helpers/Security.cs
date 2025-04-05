using System.Text;

namespace BlogWebApi.Helpers
{
    public class Security
    {
        public static string Encrypt(string pass)
        {
            using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
            {
                if (pass == null) pass = "";
                byte[] inputBytes = Encoding.ASCII.GetBytes(pass);
                byte[] hashBytes = md5.ComputeHash(inputBytes);
                string EP = Convert.ToBase64String(hashBytes);
                byte[] bytes2 = Encoding.Unicode.GetBytes(EP);
                EP = Convert.ToBase64String(bytes2);
                return EP;
            }
        }
    }
}
