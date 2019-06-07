using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using Xiropht_Connector_All.Setting;
using Xiropht_Connector_All.Utils;
using Xiropht_Mining_Pool.Api;
using Xiropht_Mining_Pool.Database;
using Xiropht_Mining_Pool.Log;
using Xiropht_Mining_Pool.Miner;
using Xiropht_Mining_Pool.Mining;
using Xiropht_Mining_Pool.Network;
using Xiropht_Mining_Pool.Payment;
using Xiropht_Mining_Pool.Setting;
using Xiropht_Mining_Pool.Utility;

namespace Xiropht_Mining_Pool
{
    class Program
    {
        private const string UnexpectedExceptionFile = "\\error_mining_pool.txt";
        public static Dictionary<int, ClassMiningPool> ListMiningPool = new Dictionary<int, ClassMiningPool>();
        public static bool Exit;
        private static Thread ThreadNetworkBlockchain;
        private static Thread ThreadMiningPoolCommandLines;
        public static CultureInfo GlobalCultureInfo = new CultureInfo("fr-FR"); // Set the global culture info, I don't suggest to change this, this one is used by the blockchain and by the whole network.
        public static string Certificate;

        static void Main(string[] args)
        {
            EnableCatchUnexpectedException();
            Console.CancelKeyPress += Console_CancelKeyPress;

            Thread.CurrentThread.Name = Path.GetFileName(Environment.GetCommandLineArgs()[0]);
            Console.WriteLine(ClassConnectorSetting.CoinName+" Mining Pool - " + Assembly.GetExecutingAssembly().GetName().Version + "R");

            if (!InitializeMiningPool())
            {
                Exit = true;
                Console.WriteLine("Press Enter to exit the program.");
                Console.ReadLine();
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Initialization of the mining pool.
        /// </summary>
        /// <returns></returns>
        private static bool InitializeMiningPool()
        {
            ClassLog.ConsoleWriteLog("Initialize Log system..", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleYellowLog, true);

            if (ClassLog.LogInitialization())
            {
                ClassLog.ConsoleWriteLog("Initialize pool settings..", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleYellowLog, true);
                if (MiningPoolSettingInitialization.InitializationPoolSettingFile())
                {
                    ClassLog.ConsoleWriteLog("Pool settings initialized.", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleGreenLog, true);

                    ClassLog.ConsoleWriteLog("Intialize pool databases..", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleYellowLog, true);
                    if (ClassMiningPoolDatabase.InitializationMiningPoolDatabases())
                    {
                        ClassMiningPoolDatabase.AutoSaveMiningPoolDatabases();

                        ClassLog.ConsoleWriteLog("Pool databases initialized.", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleGreenLog, true);

                        ClassLog.ConsoleWriteLog("Log System initialized.", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleGreenLog, true);

                        ThreadNetworkBlockchain = new Thread(async delegate ()
                        {
                            ClassLog.ConsoleWriteLog("Connect Pool to the network for retrieve current blocktemplate..", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleYellowLog, true);

                            bool notConnected = true;
                            while (notConnected)
                            {
                                Certificate = ClassUtils.GenerateCertificate();

                                while (!await ClassNetworkBlockchain.ConnectToBlockchainAsync())
                                {
                                    Thread.Sleep(5000);
                                    ClassLog.ConsoleWriteLog("Can't connect Pool to the network, retry in 5 seconds.. (Press CTRL+C to cancel and close the pool.)", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
                                }
                                Certificate = ClassUtils.GenerateCertificate();
                                ClassLog.ConsoleWriteLog("Certificate generate, send to the network..", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleYellowLog, true);
                                if (!await ClassNetworkBlockchain.SendPacketToNetworkBlockchain(Certificate, false))
                                {
                                    ClassLog.ConsoleWriteLog("Can't send certificate, reconnect now..", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
                                }
                                else
                                {
                                    ClassLog.ConsoleWriteLog("Certificate sent, start to login..", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleYellowLog, true);
                                    ClassNetworkBlockchain.ListenBlockchain();
                                    Thread.Sleep(1000);
                                    if (!await ClassNetworkBlockchain.SendPacketToNetworkBlockchain(ClassConnectorSettingEnumeration.MinerLoginType + "|" + MiningPoolSetting.MiningPoolWalletAddress, true))
                                    {
                                        ClassLog.ConsoleWriteLog("Can't login to the network, reconnect now.", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
                                    }
                                    else
                                    {
                                        ClassLog.ConsoleWriteLog("Login successfully sent, waiting confirmation..", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleYellowLog, true);
                                        notConnected = false;
                                    }
                                }
                                if (notConnected)
                                {
                                    ClassLog.ConsoleWriteLog("Can't long Pool to the network, retry in 5 seconds.. (Press CTRL+C to cancel and close the pool.)", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleYellowLog, true);
                                    Thread.Sleep(5000);
                                }
                                else
                                {
                                    break;
                                }
                            }
                        })
                        {
                            IsBackground = true
                        };
                        ThreadNetworkBlockchain.Start();
                        if (MiningPoolSetting.MiningPoolEnableFiltering)
                        {
                            ClassLog.ConsoleWriteLog("Enable Filtering Miner System..", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleYellowLog, true);
                            ClassFilteringMiner.EnableFilteringMiner();
                        }
                        if (MiningPoolSetting.MiningPoolEnableCheckMinerStats)
                        {
                            ClassLog.ConsoleWriteLog("Enable Check Miner Stats System..", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleYellowLog, true);
                            ClassMinerStats.EnableCheckMinerStats();
                        }
                        if (MiningPoolSetting.MiningPoolEnableTrustedShare)
                        {
                            ClassLog.ConsoleWriteLog("Enable Trusted Share System..", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleYellowLog, true);
                            ClassMinerStats.EnableCheckTrustedMinerStats();
                        }
                        if (MiningPoolSetting.MiningPoolEnablePayment)
                        {
                            ClassPayment.EnableAutoPaymentSystem();
                        }
                        if (MiningPoolSetting.MiningPoolMiningPort.Count > 0)
                        {
                            foreach (var miningPoolPort in MiningPoolSetting.MiningPoolMiningPort)
                            {

                                var miningPoolObject = new ClassMiningPool(miningPoolPort.Key, miningPoolPort.Value);
                                miningPoolObject.StartMiningPool();
                                ListMiningPool.Add(miningPoolPort.Key, miningPoolObject);

                            }
                            ClassApi.StartApiHttpServer();
                            EnableMiningPoolCommandLine();
                        }
                        else
                        {
                            ClassLog.ConsoleWriteLog("Cannot start mining pool, their is any ports on the setting.", ClassLogEnumeration.IndexPoolGeneralErrorLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
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
            else
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Enable mining pool command line.
        /// </summary>
        private static void EnableMiningPoolCommandLine()
        {
            ThreadMiningPoolCommandLines = new Thread(delegate ()
            {
                while (!Exit)
                {

                    string commandLine = Console.ReadLine();
                    try
                    {
                        var splitCommandLine = commandLine.Split(new char[0], StringSplitOptions.None);
                        switch (splitCommandLine[0].ToLower())
                        {
                            case MiningPoolCommandLinesEnumeration.MiningPoolCommandLineHelp:
                                ClassLog.ConsoleWriteLog(MiningPoolCommandLinesEnumeration.MiningPoolCommandLineHelp + " - Command line to get list of commands details.", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleMagentaLog, true);
                                ClassLog.ConsoleWriteLog(MiningPoolCommandLinesEnumeration.MiningPoolCommandLineStats + " - Show mining pool stats.", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleMagentaLog, true);
                                ClassLog.ConsoleWriteLog(MiningPoolCommandLinesEnumeration.MiningPoolCommandLineBanMiner + " - ban a miner wallet address, syntax: " + MiningPoolCommandLinesEnumeration.MiningPoolCommandLineBanMiner + " wallet_address time", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleMagentaLog, true);
                                ClassLog.ConsoleWriteLog(MiningPoolCommandLinesEnumeration.MiningPoolCommandLineBanMinerList + " - Show the list of miner wallet address banned.", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleMagentaLog, true);
                                ClassLog.ConsoleWriteLog(MiningPoolCommandLinesEnumeration.MiningPoolCommandLineUnBanMiner + " - Permit to unban a wallet address, syntax: " + MiningPoolCommandLinesEnumeration.MiningPoolCommandLineUnBanMiner + " wallet_address", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleMagentaLog, true);
                                ClassLog.ConsoleWriteLog(MiningPoolCommandLinesEnumeration.MiningPoolCommandLineExit + " - Stop mining pool, save and exit.", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleMagentaLog, true);

                                break;
                            case MiningPoolCommandLinesEnumeration.MiningPoolCommandLineStats:
                                ClassLog.ConsoleWriteLog("Mining Pool Stats: ", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleMagentaLog, true);
                                ClassLog.ConsoleWriteLog("Total miners connected: " + ClassMiningPoolGlobalStats.TotalWorkerConnected, ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleMagentaLog, true);
                                ClassLog.ConsoleWriteLog("Total blocks found: " + ClassMiningPoolGlobalStats.TotalBlockFound, ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleMagentaLog, true);
                                ClassLog.ConsoleWriteLog("Total miners hashrate: " + ClassMiningPoolGlobalStats.TotalMinerHashrate.ToString("F2"), ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleMagentaLog, true);
                                ClassLog.ConsoleWriteLog("Pool Wallet Total Balance: " + ClassMiningPoolGlobalStats.PoolCurrentBalance + " " + ClassConnectorSetting.CoinNameMin, ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleMagentaLog, true);
                                ClassLog.ConsoleWriteLog("Pool Wallet Total Balance in Pending: " + ClassMiningPoolGlobalStats.PoolPendingBalance + " " + ClassConnectorSetting.CoinNameMin, ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleMagentaLog, true);
                                if (ClassNetworkBlockchain.IsConnected)
                                {
                                    ClassLog.ConsoleWriteLog("Mining pool is connected to retrieve last blocktemplate.", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleMagentaLog, true);
                                }
                                else
                                {
                                    ClassLog.ConsoleWriteLog("Mining pool is not connected to retrieve last blocktemplate.", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
                                }
                                break;
                            case MiningPoolCommandLinesEnumeration.MiningPoolCommandLineBanMiner:
                                string walletAddress = splitCommandLine[1];
                                if (!ClassMinerStats.ManualBanWalletAddress(walletAddress, int.Parse(splitCommandLine[2])))
                                {
                                    ClassLog.ConsoleWriteLog("Cannot ban wallet address: "+walletAddress+" because this one not exist.", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
                                }
                                else
                                {
                                    ClassLog.ConsoleWriteLog("Wallet address: " + walletAddress + " is banned successfully pending "+splitCommandLine[2]+" second(s).", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
                                }
                                break;
                            case MiningPoolCommandLinesEnumeration.MiningPoolCommandLineBanMinerList:
                                if (ClassMinerStats.DictionaryMinerStats.Count > 0)
                                {
                                    int totalBanned = 0;
                                    foreach(var minerStats in ClassMinerStats.DictionaryMinerStats)
                                    {
                                        if (minerStats.Value.IsBanned)
                                        {
                                            long minerBanTime = minerStats.Value.DateOfBan - DateTimeOffset.Now.ToUnixTimeSeconds();
                                            ClassLog.ConsoleWriteLog("Wallet address: " + minerStats.Key + " is banned pending: " + minerBanTime + " second(s).", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
                                            totalBanned++;
                                        }
                                        else
                                        {
                                            if (minerStats.Value.TotalBan > MiningPoolSetting.MiningPoolMaxTotalBanMiner)
                                            {
                                                ClassLog.ConsoleWriteLog("Wallet address: " + minerStats.Key + " is banned forever (until to restart the pool or manual unban).", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
                                                totalBanned++;
                                            }
                                        }
                                    }
                                    if (totalBanned == 0)
                                    {
                                        ClassLog.ConsoleWriteLog("Their is any miner(s) banned.", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleGreenLog, true);
                                    }
                                }
                                break;
                            case MiningPoolCommandLinesEnumeration.MiningPoolCommandLineUnBanMiner:
                                if (ClassMinerStats.DictionaryMinerStats.Count > 0)
                                {
                                    if (ClassMinerStats.DictionaryMinerStats.ContainsKey(splitCommandLine[1]))
                                    {
                                        ClassMinerStats.DictionaryMinerStats[splitCommandLine[1]].DateOfBan = 0;
                                        ClassMinerStats.DictionaryMinerStats[splitCommandLine[1]].IsBanned = false;
                                        ClassMinerStats.DictionaryMinerStats[splitCommandLine[1]].TotalBan = 0;
                                        ClassLog.ConsoleWriteLog("Miner wallet address: " + splitCommandLine[1] + " is unbanned.", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleGreenLog, true);

                                    }
                                    else
                                    {
                                        ClassLog.ConsoleWriteLog("Miner wallet address: "+splitCommandLine[1]+" not exist.", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleYellowLog, true);
                                    }
                                }
                                else
                                {
                                    ClassLog.ConsoleWriteLog("Miner wallet address: " + splitCommandLine[1] + " not exist.", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleYellowLog, true);
                                }
                                break;
                            case MiningPoolCommandLinesEnumeration.MiningPoolCommandLineExit:
                                if (ClassPayment.PoolOnSendingTransaction)
                                {
                                    ClassLog.ConsoleWriteLog("Can't close mining pool, the pool is currently on sending transaction(s).", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
                                }
                                else
                                {
                                    if (ClassPayment.PoolOnProceedBlockReward)
                                    {
                                        ClassLog.ConsoleWriteLog("Can't close mining pool, the pool is currently on proceed block reward(s).", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
                                    }
                                    else
                                    {
                                        Exit = true;
                                        new Thread(delegate ()
                                        {
                                            if (ListMiningPool.Count > 0)
                                            {
                                                foreach (var miningPool in ListMiningPool)
                                                {
                                                    miningPool.Value.StopMiningPool();
                                                }
                                            }
                                            ClassApi.StopApiHttpServer();
                                            ClassMinerStats.StopCheckMinerStats();
                                            ClassPayment.StopAutoPaymentSystem();
                                            ClassFilteringMiner.StopFileringMiner();
                                            ClassMiningPoolDatabase.StopAutoSaveMiningPoolDatabases();
                                            ClassLog.ConsoleWriteLog("Mining pool stopped.", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleBlueLog, true);
                                            ClassLog.StopLogSystem();
                                            if (ThreadMiningPoolCommandLines != null && (ThreadMiningPoolCommandLines.IsAlive || ThreadMiningPoolCommandLines != null))
                                            {
                                                ThreadMiningPoolCommandLines.Abort();
                                                GC.SuppressFinalize(ThreadMiningPoolCommandLines);
                                            }
                                        }).Start();
                                    }
                                }
                                break;
                        }
                    }
                    catch(Exception error)
                    {
                        ClassLog.ConsoleWriteLog("Error on command line: " + commandLine + " exception: " + error.Message, ClassLogEnumeration.IndexPoolGeneralErrorLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
                    }
                }
            });
            ThreadMiningPoolCommandLines.Start();
        }

        /// <summary>
        /// Catch unexpected exception and them to a log file.
        /// </summary>
        private static void EnableCatchUnexpectedException()
        {
            AppDomain.CurrentDomain.UnhandledException += delegate (object sender, UnhandledExceptionEventArgs args2)
            {
                var filePath = ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory+ UnexpectedExceptionFile);
                var exception = (Exception)args2.ExceptionObject;
                using (var writer = new StreamWriter(filePath, true))
                {
                    writer.WriteLine("Message :" + exception.Message + "<br/>" + Environment.NewLine +
                                     "StackTrace :" +
                                     exception.StackTrace +
                                     "" + Environment.NewLine + "Date :" + DateTime.Now);
                    writer.WriteLine(Environment.NewLine +
                                     "-----------------------------------------------------------------------------" +
                                     Environment.NewLine);
                }

                Trace.TraceError(exception.StackTrace);
                Console.WriteLine("Unexpected error catched, check the error file: " + ClassUtility.ConvertPath(System.AppDomain.CurrentDomain.BaseDirectory+ UnexpectedExceptionFile));
                Environment.Exit(1);

            };
        }

        /// <summary>
        /// Event for detect Cancel Key pressed by the user for close the program.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Exit = true;
            e.Cancel = true;
            Console.WriteLine("Close pool tool.");
            Process.GetCurrentProcess().Kill();
        }
    }

    public class MiningPoolCommandLinesEnumeration
    {
        public const string MiningPoolCommandLineHelp = "help";
        public const string MiningPoolCommandLineStats = "stats";
        public const string MiningPoolCommandLineBanMiner = "banminer";
        public const string MiningPoolCommandLineBanMinerList = "banminerlist";
        public const string MiningPoolCommandLineUnBanMiner = "unbanminer";
        public const string MiningPoolCommandLineExit = "exit";
    }
}
