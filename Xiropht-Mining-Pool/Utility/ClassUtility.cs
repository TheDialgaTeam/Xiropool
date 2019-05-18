using System;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Xiropht_Connector_All.Setting;
using Xiropht_Connector_All.Utils;

namespace Xiropht_Mining_Pool.Utility
{

    public class ClassUtility
    {

        public static string[] RandomOperatorCalculation = new[] { "+", "*", "%", "-", "/" };

        public static string[] RandomNumberCalculation = new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9" };

        public static string ConvertPath(string path)
        {
            if (Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX)
            {
                path = path.Replace("\\", "/");
            }
            return path;
        }

        /// <summary>
        /// Remove special characters
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string RemoveSpecialCharacters(string str)
        {
            return Regex.Replace(str, "[^a-zA-Z0-9_.]+", "", RegexOptions.Compiled);
        }

        /// <summary>
        /// Return the current datetime in second.
        /// </summary>
        /// <returns></returns>
        public static long GetCurrentDateInSecond()
        {
            return DateTimeOffset.Now.ToUnixTimeSeconds();
        }

        /// <summary>
        /// Return the current datetime in millisecond.
        /// </summary>
        /// <returns></returns>
        public static long GetCurrentDateInMilliSecond()
        {
            return DateTimeOffset.Now.ToUnixTimeMilliseconds();
        }

        public static bool SocketIsConnected(TcpClient socket)
        {
            if (socket?.Client != null)
                try
                {
                    return !(socket.Client.Poll(100, SelectMode.SelectRead) && socket.Available == 0);
                }
                catch
                {
                    return false;
                }

            return false;
        }

        /// <summary>
        /// Hide characters from console pending input.
        /// </summary>
        /// <returns></returns>
        public static string GetHiddenConsoleInput()
        {
            StringBuilder input = new StringBuilder();
            while (true)
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Enter) break;
                if (key.Key == ConsoleKey.Backspace && input.Length > 0) input.Remove(input.Length - 1, 1);
                else if (key.Key != ConsoleKey.Backspace) input.Append(key.KeyChar);
            }

            return input.ToString();
        }

        /// <summary>
        /// Convert a string into hex string.
        /// </summary>
        /// <param name="hex"></param>
        /// <returns></returns>
        public static string StringToHexString(string hex)
        {
            byte[] ba = Encoding.UTF8.GetBytes(hex);

            return BitConverter.ToString(ba).Replace("-", "");
        }

        /// <summary>
        /// Return result from a math calculation.
        /// </summary>
        /// <param name="firstNumber"></param>
        /// <param name="operatorCalculation"></param>
        /// <param name="secondNumber"></param>
        /// <returns></returns>
        public static decimal ComputeCalculation(string firstNumber, string operatorCalculation, string secondNumber)
        {
            decimal calculCompute = 0;
            decimal firstNumberDecimal = decimal.Parse(firstNumber);
            decimal secondNumberDecimal = decimal.Parse(secondNumber);
            switch (operatorCalculation)
            {
                case "+":
                    calculCompute = firstNumberDecimal + secondNumberDecimal;
                    break;
                case "-":
                    calculCompute = firstNumberDecimal - secondNumberDecimal;
                    break;
                case "*":
                    calculCompute = firstNumberDecimal * secondNumberDecimal;
                    break;
                case "%":
                    calculCompute = firstNumberDecimal % secondNumberDecimal;
                    break;
                case "/":
                    calculCompute = firstNumberDecimal / secondNumberDecimal;
                    break;
            }

            return calculCompute;
        }


        /// <summary>
        /// Get a random number in float size.
        /// </summary>
        /// <param name="minimumValue"></param>
        /// <param name="maximumValue"></param>
        /// <returns></returns>
        public static decimal GetRandomBetweenJob(decimal minimumValue, decimal maximumValue)
        {
            using (RNGCryptoServiceProvider Generator = new RNGCryptoServiceProvider())
            {
                var randomNumber = new byte[sizeof(decimal)];

                Generator.GetBytes(randomNumber);

                var asciiValueOfRandomCharacter = (decimal)Convert.ToDouble(randomNumber[0]);

                var multiplier = (decimal)Math.Max(0, asciiValueOfRandomCharacter / 255m - 0.00000000001m);

                var range = maximumValue - minimumValue + 1;

                var randomValueInRange = (decimal)Math.Floor(multiplier * range);
                return (minimumValue + randomValueInRange);
            }
        }

        /// <summary>
        ///     Get a random number in integer size.
        /// </summary>
        /// <param name="minimumValue"></param>
        /// <param name="maximumValue"></param>
        /// <returns></returns>
        public static int GetRandomBetween(int minimumValue, int maximumValue)
        {
            using (RNGCryptoServiceProvider Generator = new RNGCryptoServiceProvider())
            {
                var randomNumber = new byte[sizeof(int)];

                Generator.GetBytes(randomNumber);

                var asciiValueOfRandomCharacter = Convert.ToDouble(randomNumber[0]);

                var multiplier = Math.Max(0, asciiValueOfRandomCharacter / 255d - 0.00000000001d);

                var range = maximumValue - minimumValue + 1;

                var randomValueInRange = Math.Floor(multiplier * range);

                return (int)(minimumValue + randomValueInRange);
            }
        }

        /// <summary>
        /// Return a number for complete a math calculation text.
        /// </summary>
        /// <returns></returns>
        public static string GenerateNumberMathCalculation(decimal minRange, decimal maxRange)
        {
            string number = "0";
            StringBuilder numberBuilder = new StringBuilder();
            while (decimal.Parse(number) > maxRange || decimal.Parse(number) < minRange)
            {
                var randomJobSize = ("" + GetRandomBetweenJob(minRange, maxRange)).Length;

                int randomSize = GetRandomBetween(1, randomJobSize);
                int counter = 0;
                while (counter < randomSize)
                {
                    if (randomSize > 1)
                    {
                        var numberRandom = RandomNumberCalculation[GetRandomBetween(0, RandomNumberCalculation.Length - 1)];
                        if (counter == 0)
                        {
                            while (numberRandom == "0")
                            {
                                numberRandom = RandomNumberCalculation[GetRandomBetween(0, RandomNumberCalculation.Length - 1)];
                            }
                            numberBuilder.Append(numberRandom);
                        }
                        else
                        {
                            numberBuilder.Append(numberRandom);
                        }
                    }
                    else
                    {
                        numberBuilder.Append(
                                       RandomNumberCalculation[
                                           GetRandomBetween(0, RandomNumberCalculation.Length - 1)]);
                    }
                    counter++;
                }
                number = numberBuilder.ToString();
                numberBuilder.Clear();
                return number;
            }
            return number;
        }

        /// <summary>
        /// Encrypt share with xor
        /// </summary>
        /// <param name="text"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string EncryptXorShare(string text, string key)
        {
            var result = new StringBuilder();

            for (int c = 0; c < text.Length; c++)
                result.Append((char)((uint)text[c] ^ (uint)key[c % key.Length]));
            return result.ToString();
        }

        /// <summary>
        /// Encrypt share with AES
        /// </summary>
        /// <param name="text"></param>
        /// <param name="keyCrypt"></param>
        /// <param name="keyByte"></param>
        /// <returns></returns>
        public static string EncryptAesShareAsync(string text, string keyCrypt, byte[] keyByte, int size)
        {
            using (var pdb = new PasswordDeriveBytes(keyCrypt, keyByte))
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    using (AesCryptoServiceProvider aes = new AesCryptoServiceProvider())
                    {
                        aes.BlockSize = size;
                        aes.KeySize = size;
                        aes.Key = pdb.GetBytes(aes.KeySize / 8);
                        aes.IV = pdb.GetBytes(aes.BlockSize / 8);
                        using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                        {
                            var textByte = Encoding.UTF8.GetBytes(text);
                            cs.Write(textByte, 0, textByte.Length);
                        }
                        return BitConverter.ToString(ms.ToArray());
                    }
                }
            }
        }

        /// <summary>
        /// Generate a sha512 hash
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string GenerateSHA512(string input)
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            using (var hash = SHA512.Create())
            {
                var hashedInputBytes = hash.ComputeHash(bytes);

                var hashedInputStringBuilder = new StringBuilder(128);
                foreach (var b in hashedInputBytes)
                    hashedInputStringBuilder.Append(b.ToString("X2"));
                return hashedInputStringBuilder.ToString();
            }
        }

        /// <summary>
        /// Get string between two strings.
        /// </summary>
        /// <param name="STR"></param>
        /// <param name="FirstString"></param>
        /// <param name="LastString"></param>
        /// <returns></returns>
        public static string GetStringBetween(string STR, string FirstString, string LastString)
        {
            string FinalString;
            int Pos1 = STR.IndexOf(FirstString) + FirstString.Length;
            int Pos2 = STR.IndexOf(LastString);
            FinalString = STR.Substring(Pos1, Pos2 - Pos1);
            return FinalString;
        }

        /// <summary>
        /// Format decimal place
        /// </summary>
        /// <param name="amount"></param>
        /// <returns></returns>
        public static string FormatMaxDecimalPlace(string amount)
        {
            amount = amount.Replace(".", ",");
            if (amount.Contains(","))
            {
                string newAmount = string.Empty;
                var splitAmount = amount.Split(new[] { "," }, StringSplitOptions.None);
                var newPointNumber = ClassConnectorSetting.MaxDecimalPlace - splitAmount[1].Length;
                if (newPointNumber > 0)
                {
                    newAmount = splitAmount[0] + "," + splitAmount[1];
                    for (int i = 0; i < newPointNumber; i++)
                    {
                        newAmount += "0";
                    }
                    amount = newAmount;
                }
                else if (newPointNumber < 0)
                {
                    newAmount = splitAmount[0] + "," + splitAmount[1].Substring(0, splitAmount[1].Length + newPointNumber);
                    amount = newAmount;
                }

            }
            else
            {
                amount += ",";
                StringBuilder builderAmount = new StringBuilder();
                builderAmount.Append(amount);
                int counter = 0;
                while (counter < ClassConnectorSetting.MaxDecimalPlace)
                {
                    builderAmount.Append("0");
                    counter++;
                }
                amount = builderAmount.ToString();
                builderAmount.Clear();
            }
            return amount.Replace(",", ".");
        }

    }
}
