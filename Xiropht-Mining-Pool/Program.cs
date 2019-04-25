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

                            Certificate = ClassUtils.GenerateCertificate();
                            while (!await ClassNetworkBlockchain.ConnectPoolToBlockchainNetworkAsync())
                            {
                                Thread.Sleep(5000);
                                ClassLog.ConsoleWriteLog("Can't connect Pool to the network, retry in 5 seconds.. (Press CTRL+C to cancel and close the pool.)", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleYellowLog, true);
                            }
                            ClassLog.ConsoleWriteLog("Pool connected to the network for retrieve current blocktemplate.", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleGreenLog, true);
                        })
                        {
                            IsBackground = true,
                            Priority = ThreadPriority.Highest
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
                                ClassLog.ConsoleWriteLog(MiningPoolCommandLinesEnumeration.MiningPoolCommandLineExit + " - Stop mining pool, save and exit.", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleMagentaLog, true);

                                break;
                            case MiningPoolCommandLinesEnumeration.MiningPoolCommandLineStats:
                                ClassLog.ConsoleWriteLog("Mining Pool Stats: ", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleMagentaLog, true);
                                ClassLog.ConsoleWriteLog("Total miners connected: " + ClassMiningPoolGlobalStats.TotalWorkerConnected, ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleMagentaLog, true);
                                ClassLog.ConsoleWriteLog("Total blocks found: " + ClassMiningPoolGlobalStats.TotalBlockFound, ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleMagentaLog, true);
                                ClassLog.ConsoleWriteLog("Total miners hashrate: " + ClassMiningPoolGlobalStats.TotalMinerHashrate, ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleMagentaLog, true);
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
                            case MiningPoolCommandLinesEnumeration.MiningPoolCommandLineExit:
                                ClassLog.ConsoleWriteLog("Stop mining pool..", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
                                ClassNetworkBlockchain.StopNetworkBlockchain();
                                Exit = true;
                                if (ListMiningPool.Count > 0)
                                {
                                    foreach(var miningPool in ListMiningPool)
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
                var filePath = ClassUtility.ConvertPath(System.AppDomain.CurrentDomain.BaseDirectory+ UnexpectedExceptionFile);
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
        public const string MiningPoolCommandLineExit = "exit";
    }
}
