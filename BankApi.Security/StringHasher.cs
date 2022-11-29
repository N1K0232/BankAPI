using System.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BankApi.Security;

public static class StringHasher
{
    public static string GetOriginalString(string input)
    {
        var bytes = Convert.FromBase64String(input);
        var originalString = Encoding.UTF8.GetString(bytes);
        return originalString;
    }
}