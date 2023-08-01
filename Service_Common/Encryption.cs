using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service_Common
{
    public class Encryption
    {
        public string Encrypt(string password)
        {
            string strmsg = string.Empty;
            byte[] encode = new byte[password.Length];
            encode = Encoding.UTF8.GetBytes(password);
            strmsg = Convert.ToBase64String(encode);
            return strmsg;
        }

        public static string Decrypt_Static(object p)
        {
            throw new NotImplementedException();
        }

        public string Decrypt(string encryptpwd)
        {
            string decryptpwd = string.Empty;
            UTF8Encoding encodepwd = new UTF8Encoding();
            Decoder Decode = encodepwd.GetDecoder();
            byte[] todecode_byte = Convert.FromBase64String(encryptpwd);
            int charCount = Decode.GetCharCount(todecode_byte, 0, todecode_byte.Length);
            char[] decoded_char = new char[charCount];
            Decode.GetChars(todecode_byte, 0, todecode_byte.Length, decoded_char, 0);
            decryptpwd = new String(decoded_char);
            return decryptpwd;
        }

        #region Decrypt Encrypted String
        /// <summary>
        /// Farheen (05 June'23)
        /// code for decrypting given string
        /// </summary>
        /// <param name="conStr"></param>
        /// <returns></returns>
        public static string Decrypt_Static(string conStr)
        {
            string decryptStr = string.Empty;
            UTF8Encoding encodeStr = new UTF8Encoding();
            Decoder Decode = encodeStr.GetDecoder();
            byte[] todecode_byte = Convert.FromBase64String(conStr);
            int charCount = Decode.GetCharCount(todecode_byte, 0, todecode_byte.Length);
            char[] decoded_char = new char[charCount];
            Decode.GetChars(todecode_byte, 0, todecode_byte.Length, decoded_char, 0);
            decryptStr = new String(decoded_char);
            return decryptStr;
        }
        #endregion
    }
}
