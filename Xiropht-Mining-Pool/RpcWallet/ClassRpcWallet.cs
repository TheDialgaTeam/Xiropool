using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Xiropht_Connector_All.RPC;
using Xiropht_Connector_All.Setting;
using Xiropht_Connector_All.Utils;
using Xiropht_Connector_All.Wallet;
using Xiropht_Mining_Pool.Log;
using Xiropht_Mining_Pool.Mining;
using Xiropht_Mining_Pool.Setting;

namespace Xiropht_Mining_Pool.RpcWallet
{

    public class ClassRpcWalletMessageEnumeration
    {

        public const string SendTransactionSuccess = "SEND-TRANSACTION-SUCCESS";
        public const string SendTransactionFailed = "SEND-TRANSACTION-FAILED";
    }

    /// <summary>
    /// Used to contact the rpc wallet tool.
    /// </summary>
    public class ClassRpcWallet
    {
        public static async Task<bool> GetCurrentBalance()
        {
            ClassLog.ConsoleWriteLog("Update current pool balance..", ClassLogEnumeration.IndexPoolWalletLog);
            try
            {
                string request = "get_wallet_balance_by_wallet_address|" + MiningPoolSetting.MiningPoolWalletAddress;
                string result = await ProceedHttpRequest("http://" + MiningPoolSetting.MiningPoolRpcWalletHost + ":" + MiningPoolSetting.MiningPoolRpcWalletPort + "/", request);
                JObject resultJson = JObject.Parse(result);
                if (resultJson.ContainsKey("wallet_address"))
                {
                    ClassMiningPoolGlobalStats.PoolCurrentBalance = decimal.Parse(resultJson["wallet_balance"].ToString().Replace(".", ","), NumberStyles.Currency, Program.GlobalCultureInfo);
                    ClassMiningPoolGlobalStats.PoolPendingBalance = decimal.Parse(resultJson["wallet_pending_balance"].ToString().Replace(".", ","), NumberStyles.Currency, Program.GlobalCultureInfo);
                    ClassLog.ConsoleWriteLog("Pool current balance: " + ClassMiningPoolGlobalStats.PoolCurrentBalance + " " + ClassConnectorSetting.CoinNameMin, ClassLogEnumeration.IndexPoolWalletLog);
                    ClassLog.ConsoleWriteLog("Pool pending balance: " + ClassMiningPoolGlobalStats.PoolPendingBalance + " " + ClassConnectorSetting.CoinNameMin, ClassLogEnumeration.IndexPoolWalletLog);
                    return true;
                }
            }
            catch(Exception error)
            {
                ClassLog.ConsoleWriteLog("Update current pool balance failed, exception error: "+error.Message, ClassLogEnumeration.IndexPoolWalletErrorLog);
                return false;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="walletAddressTarget"></param>
        /// <returns></returns>
        public static async Task<string> SendTransaction(string walletAddressTarget, decimal amount, bool anonymous = false)
        {
            ClassLog.ConsoleWriteLog("Try to send a transaction to wallet address target: "+walletAddressTarget+" of amount: "+amount+" "+ClassConnectorSetting.CoinNameMin, ClassLogEnumeration.IndexPoolWalletLog, ClassLogConsoleEnumeration.IndexPoolConsoleYellowLog, true);
            try
            {
                int anonymousTransaction = anonymous ? 1: 0;
                string request = "send_transaction_by_wallet_address|" + MiningPoolSetting.MiningPoolWalletAddress + "|" + amount + "|" + MiningPoolSetting.MiningPoolFeeTransactionPayment + "|"+anonymousTransaction+"|" + walletAddressTarget;
                string result = await ProceedHttpRequest("http://" + MiningPoolSetting.MiningPoolRpcWalletHost + ":" + MiningPoolSetting.MiningPoolRpcWalletPort + "/", request);
                JObject resultJson = JObject.Parse(result);
                if (resultJson["result"].ToString() != "not_exist")
                {

                    ClassMiningPoolGlobalStats.PoolCurrentBalance = decimal.Parse(resultJson["wallet_balance"].ToString().Replace(".", ","), NumberStyles.Currency, Program.GlobalCultureInfo);
                    ClassMiningPoolGlobalStats.PoolPendingBalance = decimal.Parse(resultJson["wallet_pending_balance"].ToString().Replace(".", ","), NumberStyles.Currency, Program.GlobalCultureInfo);
                    ClassLog.ConsoleWriteLog("Pool current balance: " + ClassMiningPoolGlobalStats.PoolCurrentBalance + " " + ClassConnectorSetting.CoinNameMin, ClassLogEnumeration.IndexPoolWalletLog);
                    ClassLog.ConsoleWriteLog("Pool pending balance: " + ClassMiningPoolGlobalStats.PoolPendingBalance + " " + ClassConnectorSetting.CoinNameMin, ClassLogEnumeration.IndexPoolWalletLog);

                    return resultJson["result"].ToString() + "|" + resultJson["hash"].ToString();
                }
                else
                {
                    return string.Empty;
                }
            }
            catch (Exception error)
            {
                ClassLog.ConsoleWriteLog("Send transaction failed, exception error: " + error.Message, ClassLogEnumeration.IndexPoolWalletErrorLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
            }
            return string.Empty;
        }

        private static async Task<string> ProceedHttpRequest(string url, string requestString)
        {
            string result = string.Empty;
            if (MiningPoolSetting.MiningPoolRpcWalletUseEncryptionKey)
            {
                requestString = ClassAlgo.GetEncryptedResultManual(ClassAlgoEnumeration.Rijndael, requestString, MiningPoolSetting.MiningPoolRpcWalletEncryptionKey, ClassWalletNetworkSetting.KeySize);
            }
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url+requestString);
            request.AutomaticDecompression = DecompressionMethods.GZip;
            request.ServicePoint.Expect100Continue = false;
            request.KeepAlive = false;
            request.Timeout = 10000;
            request.UserAgent = ClassConnectorSetting.CoinName + " Mining Pool Tool - " + Assembly.GetExecutingAssembly().GetName().Version + "R";
            string responseContent = string.Empty;
            using (HttpWebResponse response = (HttpWebResponse)await request.GetResponseAsync())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                result = await reader.ReadToEndAsync();
            }
            if (MiningPoolSetting.MiningPoolRpcWalletUseEncryptionKey)
            {
                result = ClassAlgo.GetDecryptedResultManual(ClassAlgoEnumeration.Rijndael, result, MiningPoolSetting.MiningPoolRpcWalletEncryptionKey, ClassWalletNetworkSetting.KeySize);
            }
            return result;
        }
    }
}
