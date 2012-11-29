using System;
using System.Collections.Generic;
using System.Text;

namespace DrComLoginViaHttp
{
    class ccg
    {
        private static string ConvertToMD5(string input)
        {
            byte[] i;
            i = System.Text.Encoding.UTF8.GetBytes(input);
            i = System.Text.Encoding.Convert(Encoding.UTF8, Encoding.ASCII, i);
            i = (new System.Security.Cryptography.MD5CryptoServiceProvider()).ComputeHash(i);
            return System.BitConverter.ToString(i).Replace("-","").ToLower();
        }

        const string MD5StrStart = "1";
        const string MD5StrEnd = "12345678";
        const string EndStr = "123456781";
        const string R1 = "0";
        const string R2 = "1";
        const string Para = "00";
        const string _0MKKey="123456";

        public static string GetStr(string username, string password)
        {
            return "DDDDD=" + username.Trim() + "&" +
                   "upass=" + ConvertToMD5(MD5StrStart + password + MD5StrEnd) + EndStr + "&" +
                   "R1=" + R1 + "&" +
                   "R2=" + R2 + "&" +
                   "para=" + Para + "&" +
                   "0MKKey=" + _0MKKey;
        }

        public static string ToAscii(string input)
        {
            byte[] i;
            i = System.Text.Encoding.UTF8.GetBytes(input);
            i = System.Text.Encoding.Convert(Encoding.UTF8, Encoding.ASCII, i);
            return System.Text.Encoding.ASCII.GetString(i);
        }
    }
}
