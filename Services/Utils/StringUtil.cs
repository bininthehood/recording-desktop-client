using System.Security.Cryptography;
using System.Text;

namespace RecordClient.Services.Utils
{
    class StringUtil
    {
        /// <summary>
        /// 문자열을 SHA512 로 해시 인코딩한다
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string ComputeSHA512(string input)
        {
            using (SHA512 sha512 = SHA512.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = sha512.ComputeHash(inputBytes);

                StringBuilder sb = new StringBuilder();
                foreach (byte b in hashBytes)
                {
                    sb.Append(b.ToString("x2")); // 16진수로 표현
                }

                return sb.ToString();
            }
        }
    }
}
