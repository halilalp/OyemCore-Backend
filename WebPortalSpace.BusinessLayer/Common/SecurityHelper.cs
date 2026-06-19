using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace WebPortalSpace.BusinessLayer.Common
{
    public static class SecurityHelper
    {
        /// <summary>
        /// Legacy MD5 password encryption matching ASP.NET 4.5 DataLayer
        /// </summary>
        public static string EncryptPassword(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            using (var md5 = MD5.Create())
            {
                byte[] bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(value));
                var sb = new StringBuilder();
                foreach (byte b in bytes)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }

        public static string KarakterDuzen(string str)
        {
            if (string.IsNullOrEmpty(str)) return string.Empty;
            return str.Replace("Ý", "İ").Replace("Ð", "Ğ").Replace("Þ", "Ş").Replace("ý", "ı").Replace("þ", "ş").Replace("ð", "ğ").Replace("\'fe", "ş");
        }

        public static string OzelKarakterDuzen(string str)
        {
            if (string.IsNullOrEmpty(str)) return string.Empty;
            return str.Replace("<", " ").Replace(">", " ").Replace("'", " ");
        }

        public static string SafeString(string str)
        {
            if (string.IsNullOrEmpty(str)) return string.Empty;
            return Regex.Replace(str, "[\"'<>?]", "");
        }
    }
}
