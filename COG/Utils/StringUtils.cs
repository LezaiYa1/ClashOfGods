﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace COG.Utils;

public static class StringUtils
{
    public static string GetSHA1Hash(this string input)
    {
        using var sha1 = SHA1.Create();
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = sha1.ComputeHash(inputBytes);

        var sb = new StringBuilder();
        foreach (var b in hashBytes) sb.Append(b.ToString("x2"));

        return sb.ToString();
    }

    public static string RemoveLast(this string input) => new(input.Take(input.Length - 1).ToArray());

    public static string CustomFormat(this string text, params object[] args)
    {
        var isInArg = false;
        string temp = "", result = text;
        List<string> argsStr = new();

        if (text == null || args == null) throw new ArgumentNullException();
        if (argsStr.Count > args.Length) throw new IndexOutOfRangeException();

        foreach (var c in text)
        {
            if (c == '%')
            {
                isInArg = !isInArg;
                temp += '%';
                if (!isInArg)
                {
                    argsStr.Add(temp);
                    temp = "";
                }

                continue;
            }

            if (isInArg) temp += c;
        }

        if (isInArg) throw new FormatException("The ending % character is missing.");

        var count = 0;
        foreach (var arg in argsStr)
        {
            result = result.Replace(arg, args[count].ToString());
            count++;
        }

        return result;
    }
}