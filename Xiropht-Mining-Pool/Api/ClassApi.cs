using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xiropht_Connector_All.Setting;
using Xiropht_Mining_Pool.Log;
using Xiropht_Mining_Pool.Miner;
using Xiropht_Mining_Pool.Mining;
using Xiropht_Mining_Pool.Remote;
using Xiropht_Mining_Pool.Setting;
using Xiropht_Mining_Pool.Threading;
using Xiropht_Mining_Pool.Utility;

namespace Xiropht_Mining_Pool.Api
{

    public class ClassApiEnumeration
    {
        public const string GetPoolStats = "get_pool_stats"; // Get the current pool hashrate.
        public const string GetPoolBlockById = "get_pool_block_by_id"; // Get a block found by the pool by a index selected.
        public const string GetPoolPaymentById = "get_pool_payment_by_id"; // Get a payment done by the pool by an index selected.

        public const string GetWalletStats = "get_wallet_stats"; // Get the wallet stats by a wallet address.
        public const string GetWalletPaymentById = "get_wallet_payment_by_id"; // Get a payment done to a wallet address target by an index selected.
        public const string WalletNoPaymentExist = "wallet_no_payment_exist";
        public const string WalletPaymentIndexNotExist = "wallet_payment_index_not_exist";
        public const string PacketNotExist = "not_exist";
        public const string WalletNotExist = "wallet_not_exist";
        public const string IndexNotExist = "index_not_exist";
        public const string PacketError = "error";

    }

    public class ClassApi
    {
        private static bool ListenApiHttpConnectionStatus;
        private static Thread ThreadListenApiHttpConnection;
        private static TcpListener ListenerApiHttpConnection;
        public const int MaxKeepAlive = 30;
        public static Dictionary<int, string> DictionaryBlockHashCache = new Dictionary<int, string>();
        public static Dictionary<int, string> DictionaryBlockDateFoundCache = new Dictionary<int, string>();
        public static Dictionary<int, string> DictionaryBlockRewardCache = new Dictionary<int, string>();

        /// <summary>
        /// Enable http/https api of the remote node, listen incoming connection throught web client.
        /// </summary>
        public static void StartApiHttpServer()
        {
            ListenApiHttpConnectionStatus = true;
            if (MiningPoolSetting.MiningPoolApiPort <= 0) // Not selected or invalid
            {
                ListenerApiHttpConnection = new TcpListener(IPAddress.Any, 4040); // port 4040 by default
            }
            else
            {
                ListenerApiHttpConnection = new TcpListener(IPAddress.Any, MiningPoolSetting.MiningPoolApiPort);
            }
            ListenerApiHttpConnection.Start();
            ThreadListenApiHttpConnection = new Thread(async delegate ()
            {
                while (ListenApiHttpConnectionStatus && !Program.Exit)
                {
                    try
                    {
                        var client = await ListenerApiHttpConnection.AcceptTcpClientAsync();
                        var ip = ((IPEndPoint)(client.Client.RemoteEndPoint)).Address.ToString();
                        await Task.Factory.StartNew(async () =>
                        {
                            using (var clientApiHttpObject = new ClassClientApiHttpObject(client, ip))
                            {
                                await clientApiHttpObject.StartHandleClientHttpAsync();
                            }
                        }, CancellationToken.None, TaskCreationOptions.RunContinuationsAsynchronously, PriorityScheduler.AboveNormal).ConfigureAwait(false);
                    }
                    catch
                    {

                    }
                }
            });
            ThreadListenApiHttpConnection.Start();
        }

        /// <summary>
        /// Stop Api HTTP Server
        /// </summary>
        public static void StopApiHttpServer()
        {
            ClassLog.ConsoleWriteLog("Stop mining pool API HTTP..", ClassLogEnumeration.IndexPoolApiLog, ClassLogConsoleEnumeration.IndexPoolConsoleYellowLog, true);

            ListenApiHttpConnectionStatus = false;
            if (ThreadListenApiHttpConnection != null && (ThreadListenApiHttpConnection.IsAlive || ThreadListenApiHttpConnection != null))
            {
                ThreadListenApiHttpConnection.Abort();
                GC.SuppressFinalize(ThreadListenApiHttpConnection);
            }
            try
            {
                ListenerApiHttpConnection.Stop();
            }
            catch
            {

            }
            ClassLog.ConsoleWriteLog("Mining pool API HTTP stopped.", ClassLogEnumeration.IndexPoolApiLog, ClassLogConsoleEnumeration.IndexPoolConsoleGreenLog, true);

        }
    }

    public class ClassClientApiHttpObject : IDisposable
    {
        #region Disposing Part Implementation 

        private bool _disposed;

        ~ClassClientApiHttpObject()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                }
            }

            _disposed = true;
        }

        #endregion

        private bool _clientStatus;
        private TcpClient _client;
        private string _ip;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="client"></param>
        /// <param name="ip"></param>
        public ClassClientApiHttpObject(TcpClient client, string ip)
        {
            _clientStatus = true;
            _client = client;
            _ip = ip;
        }

        private async void MaxKeepAliveFunctionAsync()
        {
            var dateConnection = DateTimeOffset.Now.ToUnixTimeSeconds() + ClassApi.MaxKeepAlive;
            while (dateConnection > DateTimeOffset.Now.ToUnixTimeSeconds())
            {
                await Task.Delay(1000);
            }
            CloseClientConnection();
        }

        /// <summary>
        /// Start to listen incoming client.
        /// </summary>
        /// <returns></returns>
        public async Task StartHandleClientHttpAsync()
        {
            var isWhitelisted = true;

            if (ClassFilteringMiner.CheckMinerIsBannedByIP(_ip))
            {
                isWhitelisted = false;
            }
            int totalWhile = 0;
            if (isWhitelisted)
            {
                await Task.Run(() => MaxKeepAliveFunctionAsync()).ConfigureAwait(false);
                try
                {
                    while (_clientStatus)
                    {
                        try
                        {
                            using (NetworkStream clientHttpReader = new NetworkStream(_client.Client))
                            {
                                using (BufferedStream bufferedStreamNetwork = new BufferedStream(clientHttpReader, ClassConnectorSetting.MaxNetworkPacketSize))
                                {
                                    byte[] buffer = new byte[ClassConnectorSetting.MaxNetworkPacketSize];

                                    int received = await bufferedStreamNetwork.ReadAsync(buffer, 0, buffer.Length);
                                    if (received > 0)
                                    {
                                        string packet = Encoding.UTF8.GetString(buffer, 0, received);
                                        try
                                        {
                                            if (!GetAndCheckForwardedIp(packet))
                                            {
                                                break;
                                            }
                                        }
                                        catch
                                        {

                                        }

                                        packet = ClassUtility.GetStringBetween(packet, "GET /", "HTTP");
                                        packet = packet.Replace("%7C", "|"); // Translate special character | 
                                        packet = packet.Replace(" ", ""); // Remove empty,space characters
                                        ClassLog.ConsoleWriteLog("HTTP API - packet received from IP: " + _ip + " - " + packet, ClassLogEnumeration.IndexPoolApiLog, ClassLogConsoleEnumeration.IndexPoolConsoleYellowLog);
                                        

                                        await HandlePacketHttpAsync(packet);
                                        break;
                                    }
                                    else
                                    {
                                        totalWhile++;
                                    }
                                    if (totalWhile >= 8)
                                    {
                                        break;
                                    }
                                }
                            }
                        }
                        catch (Exception error)
                        {
                            ClassLog.ConsoleWriteLog("HTTP API - exception error: " + error.Message, ClassLogEnumeration.IndexPoolApiErrorLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog);
                            break;
                        }
                    }
                }
                catch
                {
                }
            }
            CloseClientConnection();
        }

        /// <summary>
        /// This method permit to get back the real ip behind a proxy and check the list of banned IP.
        /// </summary>
        private bool GetAndCheckForwardedIp(string packet)
        {
            var splitPacket = packet.Split(new[] { "\n" }, StringSplitOptions.None);
            foreach (var packetEach in splitPacket)
            {
                if (packetEach != null)
                {
                    if (!string.IsNullOrEmpty(packetEach))
                    {
                        if (packetEach.ToLower().Contains("x-forwarded-for: "))
                        {
                            _ip = packetEach.ToLower().Replace("x-forwarded-for: ", "");
                            ClassLog.ConsoleWriteLog("HTTP/HTTPS API - X-Forwarded-For ip of the client is: " + _ip, ClassLogEnumeration.IndexPoolApiLog, ClassLogEnumeration.IndexPoolApiErrorLog);
                            if (ClassFilteringMiner.CheckMinerIsBannedByIP(_ip))
                            {
                                return false;
                            }
                        }

                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Close connection incoming from the client.
        /// </summary>
        private void CloseClientConnection()
        {
            _clientStatus = false;
            _client?.Close();
            _client?.Dispose();
        }

        /// <summary>
        /// Handle get request received from client.
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
        private async Task HandlePacketHttpAsync(string packet)
        {
            if (packet.Contains("|"))
            {
                var splitPacket = packet.Split(new[] { "|" }, StringSplitOptions.None);

                switch (splitPacket[0])
                {
                    case ClassApiEnumeration.GetPoolPaymentById:
                        if(int.TryParse(splitPacket[1], out var paymentId))
                        {
                            if (paymentId > 0)
                            {
                                paymentId--;
                            }
                            if (ClassMinerStats.DictionaryPoolTransaction.ContainsKey(paymentId))
                            {
                                var payment = ClassMinerStats.DictionaryPoolTransaction[paymentId].Split(new[] { "|" }, StringSplitOptions.None);
                                string hash = payment[0];
                                string amount = payment[1];
                                string fee = payment[2];
                                string timeSent = payment[3];
                                Dictionary<string, string> paymentContent = new Dictionary<string, string>()
                                {
                                    {"payment_id", splitPacket[1] },
                                    {"payment_hash", hash },
                                    {"payment_amount", ClassUtility.FormatMaxDecimalPlace(amount) },
                                    {"payment_fee", ClassUtility.FormatMaxDecimalPlace(fee) },
                                    {"payment_date_sent", timeSent }
                                };
                                await BuildAndSendHttpPacketAsync(string.Empty, true, paymentContent);
                                break;
                            }
                            else
                            {
                                await BuildAndSendHttpPacketAsync(ClassApiEnumeration.IndexNotExist);
                            }
                        }
                        else
                        {
                            await BuildAndSendHttpPacketAsync(ClassApiEnumeration.PacketError);
                        }
                        break;
                    case ClassApiEnumeration.GetPoolBlockById:
                        if (!int.TryParse(splitPacket[1], out var blockId))
                        {
                            if (blockId > 0)
                            {
                                blockId--;
                            }
                            if (ClassMiningPoolGlobalStats.ListBlockFound.ContainsKey(blockId))
                            {
                                var blockFound = ClassMiningPoolGlobalStats.ListBlockFound[blockId];
                                var splitBlock = blockFound.Split(new[] { "|" }, StringSplitOptions.None);
                                var blockResult = await ClassRemoteApi.GetBlockInformation(splitBlock[0]);
                                if (blockResult != null)
                                {
                                    await BuildAndSendHttpPacketAsync(blockResult, false, null, true); // Send already jsonfyed request.
                                }
                                else
                                {
                                    await BuildAndSendHttpPacketAsync(ClassApiEnumeration.IndexNotExist);
                                }
                                break;
                            }
                            else
                            {
                                await BuildAndSendHttpPacketAsync(ClassApiEnumeration.IndexNotExist);
                            }
                        }
                        else
                        {
                            await BuildAndSendHttpPacketAsync(ClassApiEnumeration.PacketError);
                        }
                        break;
                    case ClassApiEnumeration.GetWalletPaymentById:
                        if (splitPacket.Length > 1)
                        {
                            if (ClassMinerStats.DictionaryMinerStats.ContainsKey(splitPacket[1]))
                            {
                                if(ClassMinerStats.DictionaryMinerTransaction.ContainsKey(splitPacket[1]))
                                {
                                    if(int.TryParse(splitPacket[2], out var walletPaymentId))
                                    {
                                        if (walletPaymentId > 0)
                                        {
                                            walletPaymentId--;
                                        }
                                        if (ClassMinerStats.DictionaryMinerTransaction[splitPacket[1]].Count > walletPaymentId)
                                        {
                                            var paymentSplit = ClassMinerStats.DictionaryMinerTransaction[splitPacket[1]][walletPaymentId].Split(new[] { "|" }, StringSplitOptions.None);
                                            string transactionHash = paymentSplit[0];
                                            string transactionAmount = paymentSplit[1];
                                            string transactionFee = paymentSplit[2];
                                            string transactionDateSent = paymentSplit[3];
                                            Dictionary<string, string> paymentContent = new Dictionary<string, string>()
                                            {
                                                {"payment_hash", transactionHash },
                                                {"payment_amount", ClassUtility.FormatMaxDecimalPlace(transactionAmount) },
                                                {"payment_fee", ClassUtility.FormatMaxDecimalPlace(transactionFee) },
                                                {"payment_date_sent", transactionDateSent }
                                            };
                                            await BuildAndSendHttpPacketAsync(string.Empty, true, paymentContent);
                                        }
                                        else
                                        {
                                            await BuildAndSendHttpPacketAsync(ClassApiEnumeration.WalletPaymentIndexNotExist);
                                        }
                                    }
                                    else
                                    {
                                        await BuildAndSendHttpPacketAsync(ClassApiEnumeration.PacketError);
                                    }
                                }
                                else
                                {
                                    await BuildAndSendHttpPacketAsync(ClassApiEnumeration.WalletNoPaymentExist);
                                }
                            }
                            else
                            {
                                await BuildAndSendHttpPacketAsync(ClassApiEnumeration.WalletNotExist);
                            }
                        }
                        else
                        {
                            await BuildAndSendHttpPacketAsync(ClassApiEnumeration.PacketError);
                        }
                        break;
                    case ClassApiEnumeration.GetWalletStats:
                        if (splitPacket.Length > 1)
                        {
                            if (ClassMinerStats.DictionaryMinerStats.ContainsKey(splitPacket[1]))
                            {
                                int totalPayment = 0;
                                long totalGoodShare = 0;
                                float totalInvalidShare = 0;
                                string totalBalance = "0";
                                string totalPaid = "0";
                                float totalHashrate = 0;

                                if (ClassMinerStats.DictionaryMinerTransaction.ContainsKey(splitPacket[1]))
                                {
                                    totalPayment = ClassMinerStats.DictionaryMinerTransaction[splitPacket[1]].Count;
                                }
                                totalGoodShare = ClassMinerStats.DictionaryMinerStats[splitPacket[1]].TotalGoodShare;
                                totalInvalidShare = ClassMinerStats.DictionaryMinerStats[splitPacket[1]].TotalInvalidShare;
                                totalBalance = ClassMinerStats.DictionaryMinerStats[splitPacket[1]].TotalBalance.ToString("F"+ClassConnectorSetting.MaxDecimalPlace);
                                totalPaid = ClassMinerStats.DictionaryMinerStats[splitPacket[1]].TotalPaid.ToString("F"+ClassConnectorSetting.MaxDecimalPlace);
                                totalHashrate = ClassMinerStats.DictionaryMinerStats[splitPacket[1]].CurrentTotalHashrate;

                                Dictionary<string, string> minerStatsContent = new Dictionary<string, string>()
                                {
                                    {"wallet_address", splitPacket[1] },
                                    {"wallet_total_hashrate", ""+totalHashrate },
                                    {"wallet_total_good_share", ""+totalGoodShare },
                                    {"wallet_total_invalid_share", ""+totalInvalidShare },
                                    {"wallet_total_balance", totalBalance },
                                    {"wallet_total_paid", totalPaid },
                                    {"wallet_total_payment", ""+totalPayment }
                                };
                                await BuildAndSendHttpPacketAsync(string.Empty, true, minerStatsContent);
                                break;
                            }
                            else
                            {
                                await BuildAndSendHttpPacketAsync(ClassApiEnumeration.WalletNotExist);
                            }
                        }
                        else
                        {
                            await BuildAndSendHttpPacketAsync(ClassApiEnumeration.PacketError);
                        }
                        break;
                    default:
                        await BuildAndSendHttpPacketAsync(ClassApiEnumeration.PacketNotExist);
                        break;
                }
            }
            else
            {
                switch (packet)
                {
                    case ClassApiEnumeration.GetPoolStats:
                        decimal networkHashrate = 0;
                        if (decimal.Parse(ClassMiningPoolGlobalStats.CurrentBlockDifficulty) > 1)
                        {
                            networkHashrate = (decimal.Parse(ClassMiningPoolGlobalStats.CurrentBlockDifficulty) * ClassConnectorSetting.NETWORK_MINING_ACCURACY_EXPECTED) / 100;
                        }
                        string lastBlockFoundDate = "0";
                        if (ClassMiningPoolGlobalStats.ListBlockFound.Count > 0)
                        {
                            lastBlockFoundDate = ClassMiningPoolGlobalStats.ListBlockFound[ClassMiningPoolGlobalStats.ListBlockFound.Count - 1].Split(new[] { "|" }, StringSplitOptions.None)[1];
                        }
                        var lastBlockFound = int.Parse(ClassMiningPoolGlobalStats.CurrentBlockId) - 1;
                        string blockHash = string.Empty;
                        string blockTimestampFound = "0";
                        string blockReward = "0";
                        if (ClassApi.DictionaryBlockHashCache.ContainsKey(lastBlockFound))
                        {
                            blockHash = ClassApi.DictionaryBlockHashCache[lastBlockFound];
                            blockTimestampFound = ClassApi.DictionaryBlockDateFoundCache[lastBlockFound];
                            blockReward = ClassApi.DictionaryBlockRewardCache[lastBlockFound];
                        }
                        else
                        {
                            var blockResult = await ClassRemoteApi.GetBlockInformation("" + lastBlockFound);
                            if (blockResult != null)
                            {
                                JObject blockJson = JObject.Parse(blockResult);
                                blockHash = blockJson["block_hash"].ToString();
                                blockTimestampFound = blockJson["block_timestamp_found"].ToString();
                                blockReward = blockJson["block_reward"].ToString();
                                try
                                {
                                    ClassApi.DictionaryBlockHashCache.Add(lastBlockFound, blockHash);
                                }
                                catch
                                {

                                }
                                try
                                {
                                    ClassApi.DictionaryBlockDateFoundCache.Add(lastBlockFound, blockTimestampFound);
                                }
                                catch
                                {

                                }
                                try
                                {
                                    ClassApi.DictionaryBlockRewardCache.Add(lastBlockFound, blockReward);
                                }
                                catch
                                {

                                }
                            }
                        }
                        string miningPortInfo = string.Empty;
                        if (MiningPoolSetting.MiningPoolMiningPort.Count > 0)
                        {
                            int counter = 0;
                            foreach(var miningPort in MiningPoolSetting.MiningPoolMiningPort)
                            {
                                counter++;
                                if (counter < MiningPoolSetting.MiningPoolMiningPort.Count)
                                {
                                    miningPortInfo += miningPort.Key + "|" + miningPort.Value + ";"; // Mining port + mining difficulty;
                                }
                                else
                                {
                                    miningPortInfo += miningPort.Key + "|" + miningPort.Value; // Mining port + mining difficulty;
                                }
                            }
                        }
                        Dictionary<string, string> poolStatsContent = new Dictionary<string, string>()
                        {
                            {"pool_hashrate", "" + ClassMiningPoolGlobalStats.TotalMinerHashrate },
                            {"pool_total_miner_connected", "" + ClassMiningPoolGlobalStats.TotalMinerConnected },
                            {"pool_total_worker_connected", "" + ClassMiningPoolGlobalStats.TotalWorkerConnected },
                            {"pool_total_payment", "" + ClassMinerStats.DictionaryPoolTransaction.Count },
                            {"pool_total_block_found", "" + ClassMiningPoolGlobalStats.ListBlockFound.Count },
                            {"pool_fee", ""+MiningPoolSetting.MiningPoolFee },
                            {"pool_last_block_found_date",  lastBlockFoundDate},
                            {"pool_mining_port_and_difficulty", miningPortInfo },
                            {"pool_minimum_payment", "" + MiningPoolSetting.MiningPoolMinimumBalancePayment },
                            {"network_height", ClassMiningPoolGlobalStats.CurrentBlockId },
                            {"network_difficulty", ClassMiningPoolGlobalStats.CurrentBlockDifficulty },
                            {"network_hashrate",  "" + networkHashrate },
                            {"network_last_block_hash", blockHash },
                            {"network_last_block_found_timestamp", blockTimestampFound},
                            {"network_last_block_reward", blockReward }

                        };
                        await BuildAndSendHttpPacketAsync(string.Empty, true, poolStatsContent);
                        break;
                    default:
                        await BuildAndSendHttpPacketAsync(ClassApiEnumeration.PacketNotExist);
                        break;
                }
            }
        }

        /// <summary>
        /// build and send http packet to client.
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        private async Task BuildAndSendHttpPacketAsync(string content, bool multiResult = false, Dictionary<string, string> dictionaryContent = null, bool alreadyJsonfyed = false)
        {
            string contentToSend = string.Empty;
            if (!alreadyJsonfyed)
            {
                if (!multiResult)
                {
                    contentToSend = BuildJsonString(content);
                }
                else
                {
                    contentToSend = BuildFullJsonString(dictionaryContent);
                }
            }
            else
            {
                contentToSend = content;
            }

            StringBuilder builder = new StringBuilder();

            builder.AppendLine(@"HTTP/1.1 200 OK");
            builder.AppendLine(@"Content-Type: text/plain");
            builder.AppendLine(@"Content-Length: " + contentToSend.Length);
            builder.AppendLine(@"Access-Control-Allow-Origin: *");
            builder.AppendLine(@"");
            builder.AppendLine(@"" + contentToSend);
            await SendPacketAsync(builder.ToString());
            builder.Clear();
            contentToSend = string.Empty;
        }

        /// <summary>
        /// Return content converted for json.
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        private string BuildJsonString(string content)
        {
            JObject jsonContent = new JObject
            {
                { "result", content },
                { "version", Assembly.GetExecutingAssembly().GetName().Version.ToString() },
                { "date_packet", DateTimeOffset.Now.ToUnixTimeSeconds() }
            };
            return JsonConvert.SerializeObject(jsonContent);
        }

        /// <summary>
        /// Return content converted for json.
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        private string BuildFullJsonString(Dictionary<string, string> dictionaryContent)
        {
            JObject jsonContent = new JObject();
            foreach (var content in dictionaryContent)
            {
                jsonContent.Add(content.Key, content.Value);
            }
            jsonContent.Add("version", Assembly.GetExecutingAssembly().GetName().Version.ToString());
            jsonContent.Add("date_packet", DateTimeOffset.Now.ToUnixTimeSeconds());
            return JsonConvert.SerializeObject(jsonContent);
        }

        /// <summary>
        /// Send packet to client.
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
        private async Task SendPacketAsync(string packet)
        {
            try
            {

                using (var networkStream = new NetworkStream(_client.Client))
                {
                    using (BufferedStream bufferedStreamNetwork = new BufferedStream(networkStream, ClassConnectorSetting.MaxNetworkPacketSize))
                    {
                        var bytePacket = Encoding.UTF8.GetBytes(packet);
                        await bufferedStreamNetwork.WriteAsync(bytePacket, 0, bytePacket.Length).ConfigureAwait(false);
                        await bufferedStreamNetwork.FlushAsync().ConfigureAwait(false);
                    }
                }
            }
            catch
            {
            }
        }
    }
}
