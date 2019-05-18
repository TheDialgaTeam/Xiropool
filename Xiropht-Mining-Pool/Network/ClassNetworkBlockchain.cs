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
using Xiropht_Mining_Pool.Threading;
using Xiropht_Mining_Pool.Utility;

namespace Xiropht_Mining_Pool.Network
{
    public class ClassNetworkBlockchain
    {
        private static ClassSeedNodeConnector ClassSeedNodeConnector;
        public static bool IsConnected;
        private static long LastPacketReceived;
        private static Thread ThreadCheckConnection;
        private static Thread ThreadListenNetwork;
        private static Thread ThreadAskBlocktemplate;
        private static List<string> ListOfMiningMethodName = new List<string>();
        private static List<string> ListOfMiningMethodContent = new List<string>();
        private const int CheckConnectionInterval = 1000;



        #region Connection functions

        /// <summary>
        /// Connect pool to the blockchain network.
        /// </summary>
        /// <returns></returns>
        public static async Task<bool> ConnectPoolToBlockchainNetworkAsync()
        {

            if (ThreadListenNetwork != null && (ThreadListenNetwork.IsAlive || ThreadListenNetwork != null))
            {
                ThreadListenNetwork.Abort();
                GC.SuppressFinalize(ThreadListenNetwork);
            }
            if (ThreadAskBlocktemplate != null && (ThreadAskBlocktemplate.IsAlive || ThreadAskBlocktemplate != null))
            {
                ThreadAskBlocktemplate.Abort();
                GC.SuppressFinalize(ThreadAskBlocktemplate);
            }
            if (ThreadCheckConnection != null && (ThreadCheckConnection.IsAlive || ThreadCheckConnection != null))
            {
                ThreadCheckConnection.Abort();
                GC.SuppressFinalize(ThreadCheckConnection);
            }
            ClassSeedNodeConnector?.DisconnectToSeed();
            try
            {
                while (ClassSeedNodeConnector.ReturnStatus())
                {
                    ClassSeedNodeConnector?.DisconnectToSeed();
                }
            }
            catch
            {

            }

            ClassSeedNodeConnector?.Dispose();
            ClassSeedNodeConnector = null;
            ClassSeedNodeConnector = new ClassSeedNodeConnector();

            if (!await ClassSeedNodeConnector.StartConnectToSeedAsync(string.Empty))
            {
                IsConnected = false;
                return false;
            }
            IsConnected = true;
            LastPacketReceived = ClassUtility.GetCurrentDateInSecond();
            ListenConnectionAsync();
            return true;
        }

        /// <summary>
        /// Stop the network connection to the blockchain and stop network checker.
        /// </summary>
        public static void StopNetworkBlockchain()
        {
            ClassLog.ConsoleWriteLog("Stop connection to blockchain network..", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleYellowLog, true);
            if (ThreadCheckConnection != null && (ThreadCheckConnection.IsAlive || ThreadCheckConnection != null))
            {
                ThreadCheckConnection.Abort();
                GC.SuppressFinalize(ThreadCheckConnection);
            }
            Thread.Sleep(1000);
            if (ThreadListenNetwork != null && (ThreadListenNetwork.IsAlive || ThreadListenNetwork != null))
            {
                ThreadListenNetwork.Abort();
                GC.SuppressFinalize(ThreadListenNetwork);
            }
            if (ThreadAskBlocktemplate != null && (ThreadAskBlocktemplate.IsAlive || ThreadAskBlocktemplate != null))
            {
                ThreadAskBlocktemplate.Abort();
                GC.SuppressFinalize(ThreadAskBlocktemplate);
            }
            IsConnected = false;
            ClassSeedNodeConnector?.DisconnectToSeed();
            ClassSeedNodeConnector?.Dispose();
            ClassLog.ConsoleWriteLog("Connection to blockchain network stopped.", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleGreenLog, true);
        }

        /// <summary>
        /// Check network connection opened.
        /// </summary>
        private static void CheckConnection()
        {
            ThreadCheckConnection = new Thread(delegate ()
            {
                while (!Program.Exit)
                {
                    if (!IsConnected || LastPacketReceived + 5 < ClassUtility.GetCurrentDateInSecond() || !ClassSeedNodeConnector.ReturnStatus())
                    {
                        IsConnected = false;
                        ClassLog.ConsoleWriteLog("Pool is disconnected from the network, reconnect now..", ClassLogEnumeration.IndexPoolGeneralErrorLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
                        ListOfMiningMethodName.Clear();

                        var reconnectThread = new Thread(async delegate ()
                        {
                            while (!await ConnectPoolToBlockchainNetworkAsync())
                            {
                                if (Program.Exit)
                                {
                                    break;
                                }
                                ClassLog.ConsoleWriteLog("Can't connect pool to the network, retry in 1 seconds..", ClassLogEnumeration.IndexPoolGeneralErrorLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
                                Thread.Sleep(1000);
                            }
                            ClassLog.ConsoleWriteLog("Pool connected successfully to the network, start to login..", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleYellowLog, true);
                        });
                        reconnectThread.Start();
                        break;

                    }
                    Thread.Sleep(10);
                }
            });
            ThreadCheckConnection.Start();
        }

        #endregion

        #region Handle packet received functions

        /// <summary>
        /// Listen packets received from the network of blockchain.
        /// </summary>
        private static async void ListenConnectionAsync()
        {
            LastPacketReceived = ClassUtility.GetCurrentDateInSecond();
            ThreadListenNetwork = new Thread(async delegate ()
            {
                try
                {
                    while ((IsConnected && ClassSeedNodeConnector.ReturnStatus()) && !Program.Exit)
                    {
                        try
                        {
                            string packet = await ClassSeedNodeConnector.ReceivePacketFromSeedNodeAsync(Program.Certificate, false, true);

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
                                                if (packetEach.Length > 1)
                                                {
                                                    if (packetEach.Replace("*", "") == ClassSeedNodeStatus.SeedError)
                                                    {
                                                        ClassLog.ConsoleWriteLog("Connection to the network lost, reconnect the pool to the network..", ClassLogEnumeration.IndexPoolGeneralErrorLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
                                                        break;
                                                    }
                                                    LastPacketReceived = ClassUtility.GetCurrentDateInSecond();


                                                    await Task.Factory.StartNew(delegate { HandlePacketNetworkAsync(packetEach.Replace("*", "")); }, CancellationToken.None, TaskCreationOptions.RunContinuationsAsynchronously, PriorityScheduler.Lowest).ConfigureAwait(false);
                                                }
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    if (packet.Replace("*", "") == ClassSeedNodeStatus.SeedError)
                                    {
                                        ClassLog.ConsoleWriteLog("Connection to the network lost, reconnect the pool to the network..", ClassLogEnumeration.IndexPoolGeneralErrorLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
                                        break;
                                    }
                                    LastPacketReceived = ClassUtility.GetCurrentDateInSecond();

                                    await Task.Factory.StartNew(delegate { HandlePacketNetworkAsync(packet.Replace("*", "")); }, CancellationToken.None, TaskCreationOptions.RunContinuationsAsynchronously, PriorityScheduler.Lowest).ConfigureAwait(false);

                                }
                            }
                            else
                            {
                                if (packet == ClassSeedNodeStatus.SeedError)
                                {
                                    ClassLog.ConsoleWriteLog("Connection to the network lost, reconnect the pool to the network..", ClassLogEnumeration.IndexPoolGeneralErrorLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
                                    break;
                                }
                                LastPacketReceived = ClassUtility.GetCurrentDateInSecond();
                                await Task.Factory.StartNew(delegate { HandlePacketNetworkAsync(packet); }, CancellationToken.None, TaskCreationOptions.RunContinuationsAsynchronously, PriorityScheduler.Lowest).ConfigureAwait(false);

                            }

                        }
                        catch (Exception error)
                        {
                            ClassLog.ConsoleWriteLog("Listen packet from network blockchain error, exception: " + error.Message, ClassLogEnumeration.IndexPoolGeneralErrorLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
                            break;
                        }
                    }
                    IsConnected = false;
                }
                catch
                {
                    IsConnected = false;
                }
            });
            ThreadListenNetwork.Start();
            if (!await LoginConnection())
            {
                IsConnected = false;
            }
            else
            {
                CheckConnection();
            }
        }

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
        private static async void HandlePacketNetworkAsync(string packet)
        {
            packet = packet.Replace("*", "");
            var packetSplit = packet.Split(new[] { "|" }, StringSplitOptions.None);

            switch (packetSplit[0])
            {
                case ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.SendLoginAccepted:
                    ClassLog.ConsoleWriteLog("Mining pool logged successfully to the blockchain network.", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleGreenLog, true);
                    AskMiningElementsConnectionAsync();
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
                                ClassMiningPoolGlobalStats.CurrentBlockJobMinRange = 2;
                                ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange = 1000000;
                                ClassMiningPoolGlobalStats.CurrentBlockReward = splitBlockContent[7].Replace("REWARD=", "");
                                ClassMiningPoolGlobalStats.CurrentBlockDifficulty = 1000000.ToString();
                                ClassMiningPoolGlobalStats.CurrentBlockTimestampCreate = splitBlockContent[9].Replace("TIMESTAMP=", "");
                                ClassMiningPoolGlobalStats.CurrentBlockIndication = splitBlockContent[10].Replace("INDICATION=", "");
                                ClassMiningPoolGlobalStats.CurrentBlockTemplate = packetSplit[1];
                                int idMethod = 0;
                                if(ListOfMiningMethodName.Count > 0)
                                {
                                    for(int i = 0; i < ListOfMiningMethodName.Count; i++)
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
                                    foreach(var miner in ClassMinerStats.DictionaryMinerStats)
                                    {
                                        if (miner.Value.ListOfMinerTcpObject.Count > 0)
                                        {
                                            for(int i = 0; i < miner.Value.ListOfMinerTcpObject.Count; i++)
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
                                ClassLog.ConsoleWriteLog("New block to mining id: " + ClassMiningPoolGlobalStats.CurrentBlockId + " difficulty: " + ClassMiningPoolGlobalStats.CurrentBlockDifficulty + " hash: "+ClassMiningPoolGlobalStats.CurrentBlockHash, ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleBlueLog, true);
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
                                    ClassMiningPoolGlobalStats.CurrentBlockJobMinRange = 2;
                                    ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange = 1000000;
                                    ClassMiningPoolGlobalStats.CurrentBlockReward = splitBlockContent[7].Replace("REWARD=", "");
                                    ClassMiningPoolGlobalStats.CurrentBlockDifficulty = 1000000.ToString();
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
                                    ClassLog.ConsoleWriteLog("Current Mining Method: "+ClassMiningPoolGlobalStats.CurrentBlockMethod+" = AES ROUND: "+ClassMiningPoolGlobalStats.CurrentRoundAesRound+" AES SIZE: "+ClassMiningPoolGlobalStats.CurrentRoundAesSize+" AES BYTE KEY: "+ClassMiningPoolGlobalStats.CurrentRoundAesKey+" XOR KEY: "+ClassMiningPoolGlobalStats.CurrentRoundXorKey, ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleBlueLog, true);
                                }
                            }
                        }
                    }
                    break;
                case ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.SendJobStatus:
                    switch (packetSplit[1])
                    {
                        case ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.ShareUnlock:
                            ClassLog.ConsoleWriteLog("Block ID: "+packetSplit[2]+" has been successfully found and accepted by Blockchain !", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleGreenLog, true);
                            ClassMiningPoolGlobalStats.ListBlockFound.Add(ClassMiningPoolGlobalStats.ListBlockFound.Count, int.Parse(packetSplit[2])+"|"+ClassUtility.GetCurrentDateInSecond());
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
        /// Login pool to the network.
        /// </summary>
        private static async Task<bool> LoginConnection()
        {
            if (!await SendPacketToNetworkBlockchain(Program.Certificate, false))
            {
                IsConnected = false;
                return false;
            }
            if (!await SendPacketToNetworkBlockchain(ClassConnectorSettingEnumeration.MinerLoginType + "|" + MiningPoolSetting.MiningPoolWalletAddress, true))
            {
                IsConnected = false;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Ask current mining elements, current blocktemplate and current mining method.
        /// </summary>
        private static void AskMiningElementsConnectionAsync()
        {
            ThreadAskBlocktemplate = new Thread(async delegate ()
            {
                try
                {
                    while (IsConnected && ClassSeedNodeConnector.ReturnStatus() && !Program.Exit)
                    {
                        if (!await SendPacketToNetworkBlockchain(ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration.ReceiveAskListBlockMethod, true))
                        {
                            IsConnected = false;
                            break;
                        }
                        while (ListOfMiningMethodContent.Count == 0)
                        {
                            if (!IsConnected)
                            {
                                break;
                            }
                           Thread.Sleep(100);
                        }
                        Thread.Sleep(1000);
                        if (!await SendPacketToNetworkBlockchain(ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration.ReceiveAskCurrentBlockMining, true))
                        {
                            IsConnected = false;
                            break;
                        }
                        Thread.Sleep(1000);
                    }
                }
                catch
                {
                    IsConnected = false;
                }
                IsConnected = false;
            });
            ThreadAskBlocktemplate.Start();
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
                if (!await ClassSeedNodeConnector.SendPacketToSeedNodeAsync(packet, string.Empty, false, false))
                {
                    IsConnected = false;
                    return false;
                }
            }
            else
            {
                if (!await ClassSeedNodeConnector.SendPacketToSeedNodeAsync(packet, Program.Certificate, false, true))
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
                string result = await ClassRemoteApi.ProceedHttpRequest("http://" + ClassSeedNodeConnector.ReturnCurrentSeedNodeHost() + ":" + ClassConnectorSetting.SeedNodeTokenPort + "/", request);
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
