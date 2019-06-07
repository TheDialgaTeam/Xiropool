using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xiropht_Connector_All.RPC;
using Xiropht_Connector_All.Seed;
using Xiropht_Connector_All.Setting;
using Xiropht_Connector_All.SoloMining;
using Xiropht_Connector_All.Utils;
using Xiropht_Mining_Pool.Api;
using Xiropht_Mining_Pool.Log;
using Xiropht_Mining_Pool.Miner;
using Xiropht_Mining_Pool.Mining;
using Xiropht_Mining_Pool.Payment;
using Xiropht_Mining_Pool.Remote;
using Xiropht_Mining_Pool.Setting;
using Xiropht_Mining_Pool.Utility;

namespace Xiropht_Mining_Pool.Network
{
    public class ClassNetworkBlockchain
    {
        private static ClassSeedNodeConnector classSeedNodeConnector;
        public static bool IsConnected;
        private static Thread ThreadCheckConnection;
        private static Thread ThreadListenNetwork;
        private static List<string> ListOfMiningMethodName = new List<string>();
        private static List<string> ListOfMiningMethodContent = new List<string>();
        private static Thread ThreadListenBlockchain;
        private static Thread ThreadAskMiningMethod;
        private static long _lastPacketReceivedFromBlockchain;
        public static bool FirstStart;
        public static bool LoginAccepted;



        #region Connection functions

        /// <summary>
        /// Connect to the network of blockchain.
        /// </summary>
        /// <returns></returns>
        public static async Task<bool> ConnectToBlockchainAsync()
        {

            classSeedNodeConnector?.DisconnectToSeed();
            classSeedNodeConnector = null;
            classSeedNodeConnector = new ClassSeedNodeConnector();


            ListOfMiningMethodName?.Clear();
            ListOfMiningMethodContent?.Clear();
            if (!await classSeedNodeConnector.StartConnectToSeedAsync(string.Empty))
            {
                IsConnected = false;
                return false;
            }
            if (!FirstStart)
            {
                FirstStart = true;
                CheckBlockchainConnection();
            }
            IsConnected = true;
            return true;
        }


        private static void CheckBlockchainConnection()
        {
            _lastPacketReceivedFromBlockchain = DateTimeOffset.Now.ToUnixTimeSeconds();
            var threadCheckConnection = new Thread(async delegate ()
            {
                while (true)
                {
                    Thread.Sleep(1000);
                    if (!IsConnected || !classSeedNodeConnector.ReturnStatus())
                    {
                        if (ThreadListenBlockchain != null && (ThreadListenBlockchain.IsAlive || ThreadListenBlockchain != null))
                        {
                            ThreadListenBlockchain.Abort();
                            GC.SuppressFinalize(ThreadListenBlockchain);
                        }
                        if (ThreadAskMiningMethod != null && (ThreadAskMiningMethod.IsAlive || ThreadAskMiningMethod != null))
                        {
                            ThreadAskMiningMethod.Abort();
                            GC.SuppressFinalize(ThreadAskMiningMethod);
                        }
                        ClassMiningPoolGlobalStats.CurrentBlockTemplate = string.Empty;
                        ClassMiningPoolGlobalStats.CurrentBlockId = string.Empty;
                        IsConnected = false;
                        LoginAccepted = false;
                        while (!await ConnectToBlockchainAsync())
                        {
                            ClassLog.ConsoleWriteLog("Can't connect to the network, retry in 5 seconds..", ClassLogEnumeration.IndexPoolGeneralErrorLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
                            Thread.Sleep(5000);
                        }
                        ClassLog.ConsoleWriteLog("Connection success, generate dynamic certificate for the network.", ClassLogEnumeration.IndexPoolGeneralErrorLog, ClassLogConsoleEnumeration.IndexPoolConsoleYellowLog, true);
                        Program.Certificate = ClassUtils.GenerateCertificate();
                        ClassLog.ConsoleWriteLog("Certificate generate, send to the network..", ClassLogEnumeration.IndexPoolGeneralErrorLog, ClassLogConsoleEnumeration.IndexPoolConsoleYellowLog, true);
                        if (!await SendPacketToNetworkBlockchain(Program.Certificate, false))
                        {
                            ClassLog.ConsoleWriteLog("Can't send certificate, reconnect now..",  ClassLogEnumeration.IndexPoolGeneralErrorLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
                            IsConnected = false;
                        }
                        else
                        {
                            Thread.Sleep(1000);
                            ClassLog.ConsoleWriteLog("Certificate sent, start to login..", ClassLogEnumeration.IndexPoolGeneralErrorLog, ClassLogConsoleEnumeration.IndexPoolConsoleYellowLog, true);
                            ListenBlockchain();
                            if (!await SendPacketToNetworkBlockchain(ClassConnectorSettingEnumeration.MinerLoginType + "|" + MiningPoolSetting.MiningPoolWalletAddress, true))
                            {
                                ClassLog.ConsoleWriteLog("Can't login to the network, reconnect now.", ClassLogEnumeration.IndexPoolGeneralErrorLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
                                IsConnected = false;
                            }
                            else
                            {
                                ClassLog.ConsoleWriteLog("Login successfully sent, waiting confirmation.. (Wait 5 seconds maximum.)", ClassLogEnumeration.IndexPoolGeneralErrorLog, ClassLogConsoleEnumeration.IndexPoolConsoleGreenLog, true);
                                IsConnected = true;
                                Thread.Sleep(ClassConnectorSetting.MaxTimeoutConnect);
                                if (!LoginAccepted)
                                {
                                    IsConnected = false;
                                }
                            }
                        }
                    }
                }
            });
            threadCheckConnection.Start();
        }


        /// <summary>
        /// Listen Blockchain packet.
        /// </summary>
        public static void ListenBlockchain()
        {
            if (ThreadListenBlockchain != null && (ThreadListenBlockchain.IsAlive || ThreadListenBlockchain != null))
            {
                ThreadListenBlockchain.Abort();
                GC.SuppressFinalize(ThreadListenBlockchain);
            }
            ThreadListenBlockchain = new Thread(async delegate ()
            {
                while (IsConnected)
                {
                    try
                    {

                        string packet = await classSeedNodeConnector.ReceivePacketFromSeedNodeAsync(Program.Certificate, false, true).ConfigureAwait(false);
                        if (packet == ClassSeedNodeStatus.SeedError)
                        {
                            ClassLog.ConsoleWriteLog("Connection to network lost, reconnect in 5 seconds..", ClassLogEnumeration.IndexPoolGeneralErrorLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
                            IsConnected = false;
                            break;
                        }
                        _lastPacketReceivedFromBlockchain = DateTimeOffset.Now.ToUnixTimeSeconds();

                        if (packet.Contains("*"))
                        {
                            var splitPacket = packet.Split(new[] { "*" }, StringSplitOptions.None);
                            if (splitPacket.Length > 1)
                            {
                                foreach (var packetEach in splitPacket)
                                {
                                    if (packetEach != null)
                                    {
                                        if (!string.IsNullOrEmpty(packetEach))
                                        {

                                            await HandlePacketNetworkAsync(packetEach.Replace("*", ""));
                                        }
                                    }
                                }
                            }
                            else
                            {
                                await HandlePacketNetworkAsync(packet.Replace("*", ""));
                            }
                        }
                        else
                        {
                            await HandlePacketNetworkAsync(packet);
                        }
                    }
                    catch
                    {
                        ClassLog.ConsoleWriteLog("Connection to network lost, reconnect in 5 seconds..", ClassLogEnumeration.IndexPoolGeneralErrorLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
                        IsConnected = false;
                        break;
                    }
                }
            });
            ThreadListenBlockchain.Start();
        }

        #endregion

        #region Handle packet received functions

        /// <summary>
        /// Send packet share for attempt to unlock the block.
        /// </summary>
        /// <param name="encryptedShare"></param>
        /// <param name="result"></param>
        /// <param name="math"></param>
        /// <param name="hashShare"></param>
        public static async void SendPacketBlockFound(string encryptedShare, decimal result, string math, string hashShare)
        {
            string packetShare = ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration.ReceiveJob + "|" + encryptedShare + "|" + result + "|" + math + "|" + hashShare + "|" + ClassMiningPoolGlobalStats.CurrentBlockId + "|Mining Pool Tool - " + Assembly.GetExecutingAssembly().GetName().Version + "R";
            if (!await SendPacketToNetworkBlockchain(packetShare, true))
            {
                IsConnected = false;
                ClassLog.ConsoleWriteLog("Warning cannot send packet for attempt to unlock the block id " + ClassMiningPoolGlobalStats.CurrentBlockId + ", network connection lost.", ClassLogEnumeration.IndexPoolGeneralErrorLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
            }
        }

        /// <summary>
        /// Handle packet received from the network.
        /// </summary>
        /// <param name="packet"></param>
        private static async Task HandlePacketNetworkAsync(string packet)
        {

            packet = packet.Replace("*", "");
            var packetSplit = packet.Split(new[] { "|" }, StringSplitOptions.None);

            switch (packetSplit[0])
            {
                case ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.SendLoginAccepted:
                    LoginAccepted = true;
                    ClassLog.ConsoleWriteLog("Mining pool logged successfully to the blockchain network.", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleGreenLog, true);
                    AskMiningMethod();
                    break;
                case ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.SendListBlockMethod:
                    var methodList = packetSplit[1];
                    if (methodList.Contains("#"))
                    {
                        var splitMethodList = methodList.Split(new[] { "#" }, StringSplitOptions.None);
                        if (ListOfMiningMethodName.Count > 1)
                        {
                            foreach (var methodName in splitMethodList)
                            {
                                if (!string.IsNullOrEmpty(methodName))
                                {
                                    if (ListOfMiningMethodName.Contains(methodName) == false)
                                    {
                                        ListOfMiningMethodName.Add(methodName);
                                    }
                                    if (!await SendPacketToNetworkBlockchain(ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration.ReceiveAskContentBlockMethod + "|" + methodName, true).ConfigureAwait(false))
                                    {
                                        IsConnected = false;
                                        break;
                                    }

                                    await Task.Delay(1000);
                                }
                            }
                        }
                        else
                        {

                            foreach (var methodName in splitMethodList)
                            {
                                if (!string.IsNullOrEmpty(methodName))
                                {
                                    if (ListOfMiningMethodName.Contains(methodName) == false)
                                    {
                                        ListOfMiningMethodName.Add(methodName);
                                    }
                                    if (!await SendPacketToNetworkBlockchain(ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration.ReceiveAskContentBlockMethod + "|" + methodName, true).ConfigureAwait(false))
                                    {
                                        IsConnected = false;
                                        break;
                                    }
                                    await Task.Delay(1000);
                                }
                            }
                        }
                    }
                    else
                    {
                        if (ListOfMiningMethodName.Contains(methodList) == false)
                        {
                            ListOfMiningMethodName.Add(methodList);
                        }

                        if (!await SendPacketToNetworkBlockchain(ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration.ReceiveAskContentBlockMethod + "|" + methodList, true).ConfigureAwait(false))
                        {
                            IsConnected = false;
                        }
                    }
                    break;
                case ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.SendContentBlockMethod:
                    if (ListOfMiningMethodContent.Count == 0)
                    {
                        ListOfMiningMethodContent.Add(packetSplit[1]);
                    }
                    else
                    {
                        ListOfMiningMethodContent[0] = packetSplit[1];
                    }
                    break;
                case ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.SendCurrentBlockMining:
                    if (packetSplit[1] != ClassMiningPoolGlobalStats.CurrentBlockTemplate)
                    {
                        var splitBlockContent = packetSplit[1].Split(new[] { "&" }, StringSplitOptions.None);
                        if (splitBlockContent[0].Replace("ID=", "") != "" && splitBlockContent[0].Replace("ID=", "").Length > 0)
                        {
                            if (splitBlockContent[0].Replace("ID=", "") != ClassMiningPoolGlobalStats.CurrentBlockId)
                            {

                                ClassMiningPoolGlobalStats.CurrentBlockId = splitBlockContent[0].Replace("ID=", "");
                                ClassMiningPoolGlobalStats.CurrentBlockHash = splitBlockContent[1].Replace("HASH=", "");
                                ClassMiningPoolGlobalStats.CurrentBlockAlgorithm = splitBlockContent[2].Replace("ALGORITHM=", "");
                                ClassMiningPoolGlobalStats.CurrentBlockSize = splitBlockContent[3].Replace("SIZE=", "");
                                ClassMiningPoolGlobalStats.CurrentBlockMethod = splitBlockContent[4].Replace("METHOD=", "");
                                ClassMiningPoolGlobalStats.CurrentBlockKey = splitBlockContent[5].Replace("KEY=", "");
                                ClassMiningPoolGlobalStats.CurrentBlockJob = splitBlockContent[6].Replace("JOB=", "");
                                ClassMiningPoolGlobalStats.CurrentBlockJobMinRange = decimal.Parse(ClassMiningPoolGlobalStats.CurrentBlockJob.Split(new[] { ";" }, StringSplitOptions.None)[0]);
                                ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange = decimal.Parse(ClassMiningPoolGlobalStats.CurrentBlockJob.Split(new[] { ";" }, StringSplitOptions.None)[1]);
                                ClassMiningPoolGlobalStats.CurrentBlockReward = splitBlockContent[7].Replace("REWARD=", "");
                                ClassMiningPoolGlobalStats.CurrentBlockDifficulty = splitBlockContent[8].Replace("DIFFICULTY=", "");
                                ClassMiningPoolGlobalStats.CurrentBlockTimestampCreate = splitBlockContent[9].Replace("TIMESTAMP=", "");
                                ClassMiningPoolGlobalStats.CurrentBlockIndication = splitBlockContent[10].Replace("INDICATION=", "");
                                ClassMiningPoolGlobalStats.CurrentBlockTemplate = packetSplit[1];
                                int idMethod = 0;
                                if (ListOfMiningMethodName.Count > 0)
                                {
                                    for (int i = 0; i < ListOfMiningMethodName.Count; i++)
                                    {
                                        if (i < ListOfMiningMethodName.Count)
                                        {
                                            if (ListOfMiningMethodName[i] == ClassMiningPoolGlobalStats.CurrentBlockMethod)
                                            {
                                                idMethod = i;
                                            }
                                        }
                                    }
                                }
                                var splitMethod = ListOfMiningMethodContent[idMethod].Split(new[] { "#" }, StringSplitOptions.None);
                                ClassMiningPoolGlobalStats.CurrentRoundAesRound = int.Parse(splitMethod[0]);
                                ClassMiningPoolGlobalStats.CurrentRoundAesSize = int.Parse(splitMethod[1]);
                                ClassMiningPoolGlobalStats.CurrentRoundAesKey = splitMethod[2];
                                ClassMiningPoolGlobalStats.CurrentRoundXorKey = int.Parse(splitMethod[3]);
                                if (ClassMinerStats.DictionaryMinerStats.Count > 0)
                                {
                                    foreach (var miner in ClassMinerStats.DictionaryMinerStats)
                                    {
                                        if (miner.Value.ListOfMinerTcpObject.Count > 0)
                                        {
                                            for (int i = 0; i < miner.Value.ListOfMinerTcpObject.Count; i++)
                                            {
                                                if (i < miner.Value.ListOfMinerTcpObject.Count)
                                                {
                                                    if (miner.Value.ListOfMinerTcpObject[i] != null)
                                                    {
                                                        if (miner.Value.ListOfMinerTcpObject[i].IsLogged)
                                                        {
                                                            miner.Value.ListOfMinerTcpObject[i].MiningPoolSendJobAsync(miner.Value.ListOfMinerTcpObject[i].CurrentMiningJobDifficulty);
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                ClassLog.ConsoleWriteLog("New block to mining id: " + ClassMiningPoolGlobalStats.CurrentBlockId + " difficulty: " + ClassMiningPoolGlobalStats.CurrentBlockDifficulty + " hash: " + ClassMiningPoolGlobalStats.CurrentBlockHash, ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleBlueLog, true);
                                ClassLog.ConsoleWriteLog("Current Mining Method: " + ClassMiningPoolGlobalStats.CurrentBlockMethod + " = AES ROUND: " + ClassMiningPoolGlobalStats.CurrentRoundAesRound + " AES SIZE: " + ClassMiningPoolGlobalStats.CurrentRoundAesSize + " AES BYTE KEY: " + ClassMiningPoolGlobalStats.CurrentRoundAesKey + " XOR KEY: " + ClassMiningPoolGlobalStats.CurrentRoundXorKey, ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleBlueLog, true);
                            }
                            else
                            {
                                if (splitBlockContent[1].Replace("HASH=", "") != ClassMiningPoolGlobalStats.CurrentBlockHash)
                                {
                                    ClassMiningPoolGlobalStats.CurrentBlockId = splitBlockContent[0].Replace("ID=", "");
                                    ClassMiningPoolGlobalStats.CurrentBlockHash = splitBlockContent[1].Replace("HASH=", "");
                                    ClassMiningPoolGlobalStats.CurrentBlockAlgorithm = splitBlockContent[2].Replace("ALGORITHM=", "");
                                    ClassMiningPoolGlobalStats.CurrentBlockSize = splitBlockContent[3].Replace("SIZE=", "");
                                    ClassMiningPoolGlobalStats.CurrentBlockMethod = splitBlockContent[4].Replace("METHOD=", "");
                                    ClassMiningPoolGlobalStats.CurrentBlockKey = splitBlockContent[5].Replace("KEY=", "");
                                    ClassMiningPoolGlobalStats.CurrentBlockJob = splitBlockContent[6].Replace("JOB=", "");
                                    ClassMiningPoolGlobalStats.CurrentBlockJobMinRange = decimal.Parse(ClassMiningPoolGlobalStats.CurrentBlockJob.Split(new[] { ";" }, StringSplitOptions.None)[0]);
                                    ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange = decimal.Parse(ClassMiningPoolGlobalStats.CurrentBlockJob.Split(new[] { ";" }, StringSplitOptions.None)[1]);
                                    ClassMiningPoolGlobalStats.CurrentBlockReward = splitBlockContent[7].Replace("REWARD=", "");
                                    ClassMiningPoolGlobalStats.CurrentBlockDifficulty = splitBlockContent[8].Replace("DIFFICULTY=", "");
                                    ClassMiningPoolGlobalStats.CurrentBlockTimestampCreate = splitBlockContent[9].Replace("TIMESTAMP=", "");
                                    ClassMiningPoolGlobalStats.CurrentBlockIndication = splitBlockContent[10].Replace("INDICATION=", "");
                                    ClassMiningPoolGlobalStats.CurrentBlockTemplate = packetSplit[1];

                                    int idMethod = 0;
                                    if (ListOfMiningMethodName.Count > 0)
                                    {
                                        for (int i = 0; i < ListOfMiningMethodName.Count; i++)
                                        {
                                            if (i < ListOfMiningMethodName.Count)
                                            {
                                                if (ListOfMiningMethodName[i] == ClassMiningPoolGlobalStats.CurrentBlockMethod)
                                                {
                                                    idMethod = i;
                                                }
                                            }
                                        }
                                    }
                                    var splitMethod = ListOfMiningMethodContent[idMethod].Split(new[] { "#" }, StringSplitOptions.None);
                                    ClassMiningPoolGlobalStats.CurrentRoundAesRound = int.Parse(splitMethod[0]);
                                    ClassMiningPoolGlobalStats.CurrentRoundAesSize = int.Parse(splitMethod[1]);
                                    ClassMiningPoolGlobalStats.CurrentRoundAesKey = splitMethod[2];
                                    ClassMiningPoolGlobalStats.CurrentRoundXorKey = int.Parse(splitMethod[3]);
                                    if (ClassMinerStats.DictionaryMinerStats.Count > 0)
                                    {
                                        foreach (var miner in ClassMinerStats.DictionaryMinerStats)
                                        {
                                            if (miner.Value.ListOfMinerTcpObject.Count > 0)
                                            {
                                                for (int i = 0; i < miner.Value.ListOfMinerTcpObject.Count; i++)
                                                {
                                                    if (i < miner.Value.ListOfMinerTcpObject.Count)
                                                    {
                                                        if (miner.Value.ListOfMinerTcpObject[i] != null)
                                                        {
                                                            if (miner.Value.ListOfMinerTcpObject[i].IsLogged)
                                                            {
                                                                miner.Value.ListOfMinerTcpObject[i].MiningPoolSendJobAsync(miner.Value.ListOfMinerTcpObject[i].CurrentMiningJobDifficulty);
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    ClassLog.ConsoleWriteLog("Renewed block to mining id: " + ClassMiningPoolGlobalStats.CurrentBlockId + " difficulty: " + ClassMiningPoolGlobalStats.CurrentBlockDifficulty + " hash: " + ClassMiningPoolGlobalStats.CurrentBlockHash, ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleBlueLog, true);
                                    ClassLog.ConsoleWriteLog("Current Mining Method: " + ClassMiningPoolGlobalStats.CurrentBlockMethod + " = AES ROUND: " + ClassMiningPoolGlobalStats.CurrentRoundAesRound + " AES SIZE: " + ClassMiningPoolGlobalStats.CurrentRoundAesSize + " AES BYTE KEY: " + ClassMiningPoolGlobalStats.CurrentRoundAesKey + " XOR KEY: " + ClassMiningPoolGlobalStats.CurrentRoundXorKey, ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleBlueLog, true);
                                }
                            }
                        }
                    }
                    break;
                case ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.SendJobStatus:
                    switch (packetSplit[1])
                    {
                        case ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.ShareUnlock:
                            ClassLog.ConsoleWriteLog("Block ID: " + packetSplit[2] + " has been successfully found and accepted by Blockchain !", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleGreenLog, true);
                            ClassMiningPoolGlobalStats.ListBlockFound.Add(ClassMiningPoolGlobalStats.ListBlockFound.Count, int.Parse(packetSplit[2]) + "|" + ClassUtility.GetCurrentDateInSecond());
                            await Task.Factory.StartNew(() => ClassPayment.ProceedMiningScoreRewardAsync(packetSplit[2]), CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Current).ConfigureAwait(false);
                            break;
                        case ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.ShareBad:
                            ClassLog.ConsoleWriteLog("Block ID: " + packetSplit[2] + " has been found by someone else before the pool or the share sent is invalid.", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
                            break;
                        case ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.ShareAleady:
                            ClassLog.ConsoleWriteLog("Block ID: " + packetSplit[2] + " is already found by someone else.", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
                            break;
                    }
                    break;

            }

        }

        #endregion

        #region Send packet functions

        /// <summary>
        /// Method for ask and update mining methods.
        /// </summary>
        private static void AskMiningMethod()
        {
            if (ThreadAskMiningMethod != null && (ThreadAskMiningMethod.IsAlive || ThreadAskMiningMethod != null))
            {
                ThreadAskMiningMethod.Abort();
                GC.SuppressFinalize(ThreadAskMiningMethod);
            }
            ThreadAskMiningMethod = new Thread(async delegate ()
            {
                while (IsConnected)
                {
                    if (!await classSeedNodeConnector.SendPacketToSeedNodeAsync(ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration.ReceiveAskListBlockMethod, Program.Certificate, false, true).ConfigureAwait(false))
                    {
                        IsConnected = false;
                        break;
                    }

                    Thread.Sleep(1000);

                    if (!await classSeedNodeConnector.SendPacketToSeedNodeAsync(ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration.ReceiveAskCurrentBlockMining, Program.Certificate, false, true).ConfigureAwait(false))
                    {
                        IsConnected = false;
                        break;
                    }
                    Thread.Sleep(1000);
                }
            });
            ThreadAskMiningMethod.Start();
        }


        /// <summary>
        /// Send packet to the network of blockchain.
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="encrypted"></param>
        public static async Task<bool> SendPacketToNetworkBlockchain(string packet, bool encrypted)
        {
            if (!encrypted)
            {
                if (!await classSeedNodeConnector.SendPacketToSeedNodeAsync(packet, string.Empty, false, false))
                {
                    IsConnected = false;
                    return false;
                }
            }
            else
            {
                if (!await classSeedNodeConnector.SendPacketToSeedNodeAsync(packet, Program.Certificate, false, true))
                {
                    IsConnected = false;
                    return false;
                }
            }
            return true;
        }



        #endregion

        #region Check packet functions

        public static async Task<bool> CheckWalletAddressExistAsync(string walletAddress)
        {
            try
            {

                string request = ClassConnectorSettingEnumeration.WalletTokenType + "|" + ClassRpcWalletCommand.TokenCheckWalletAddressExist+"|"+walletAddress;
                string result = await ClassRemoteApi.ProceedHttpRequest("http://" + classSeedNodeConnector.ReturnCurrentSeedNodeHost() + ":" + ClassConnectorSetting.SeedNodeTokenPort + "/", request);
                if (result == string.Empty || result == ClassApiEnumeration.PacketNotExist)
                {
                    return false;
                }
                else
                {
                    JObject resultJson = JObject.Parse(result);
                    if (resultJson.ContainsKey(ClassMiningPoolRequest.SubmitResult))
                    {
                        string resultCheckWalletAddress = resultJson[ClassMiningPoolRequest.SubmitResult].ToString();
                        if (resultCheckWalletAddress.Contains("|"))
                        {
                            var splitResultCheckWalletAddress = resultCheckWalletAddress.Split(new[] { "|" }, StringSplitOptions.None);
                            if (splitResultCheckWalletAddress[0] == ClassRpcWalletCommand.SendTokenCheckWalletAddressInvalid)
                            {
                                return false;
                            }
                            else if (splitResultCheckWalletAddress[0] == ClassRpcWalletCommand.SendTokenCheckWalletAddressValid)
                            {
                                return true;
                            }
                            else
                            {
                                return false;
                            }
                        }
                        else
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        #endregion

    }
}
