using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xiropht_Connector_All.Setting;
using Xiropht_Mining_Pool.Setting;
using Xiropht_Mining_Pool.Utility;

namespace Xiropht_Mining_Pool.Log
{
    public class ClassLogConsoleEnumeration
    {
        public const int IndexPoolConsoleGreenLog = 0;
        public const int IndexPoolConsoleYellowLog = 1;
        public const int IndexPoolConsoleRedLog = 2;
        public const int IndexPoolConsoleWhiteLog = 3;
        public const int IndexPoolConsoleBlueLog = 4;
        public const int IndexPoolConsoleMagentaLog = 5;
    }

    public class ClassLogEnumeration
    {
        public const int IndexPoolGeneralErrorLog = 0;
        public const int IndexPoolGeneralLog = 1;
        public const int IndexPoolFilteringErrorLog = 2;
        public const int IndexPoolFilteringLog = 3;
        public const int IndexPoolCheckStatsErrorLog = 4;
        public const int IndexPoolCheckStatsLog = 5;
        public const int IndexPoolMinerErrorLog = 6;
        public const int IndexPoolMinerLog = 7;
        public const int IndexPoolPaymentErrorLog = 8;
        public const int IndexPoolPaymentLog = 9;
        public const int IndexPoolWalletErrorLog = 10;
        public const int IndexPoolWalletLog = 11;
        public const int IndexPoolApiErrorLog = 12;
        public const int IndexPoolApiLog = 13;
    }

    public class ClassLog
    {
        /// <summary>
        /// Log file main path.
        /// </summary>
        private const string LogDirectory = "\\Log\\";

        /// <summary>
        /// Log files paths
        /// </summary>
        private const string LogPoolGeneralError = "\\Log\\pool-general-error.log"; // 0
        private const string LogPoolGeneral = "\\Log\\pool-general.log"; // 1
        private const string LogPoolFilteringError = "\\Log\\pool-filtering-error.log"; // 2
        private const string LogPoolFiltering = "\\Log\\pool-filtering.log"; // 3
        private const string LogPoolMinerCheckStatsError = "\\Log\\pool-miner-check-stats-error.log"; // 4
        private const string LogPoolMinerCheckStats = "\\Log\\pool-miner-check-stats.log"; // 5
        private const string LogPoolMinerError = "\\Log\\pool-miner-error.log"; // 6
        private const string LogPoolMiner = "\\Log\\pool-miner.log"; // 7
        private const string LogPoolPaymentError = "\\Log\\pool-payment-error.log"; // 8
        private const string LogPoolPayment = "\\Log\\pool-payment.log"; // 9
        private const string LogPoolWalletError = "\\Log\\pool-wallet-error.log"; // 10
        private const string LogPoolWallet = "\\Log\\pool-wallet.log"; // 11
        private const string LogPoolApiError = "\\Log\\pool-api-error.log"; // 12
        private const string LogPoolApi = "\\Log\\pool-api.log"; // 13

        /// <summary>
        /// Streamwriter's 
        /// </summary>
        private static StreamWriter PoolGeneralErrorLogWriter;
        private static StreamWriter PoolGeneralLogWriter;
        private static StreamWriter PoolFilteringErrorLogWriter;
        private static StreamWriter PoolFilteringLogWriter;
        private static StreamWriter PoolMinerCheckStatsErrorLogWriter;
        private static StreamWriter PoolMinerCheckStatsLogWriter;
        private static StreamWriter PoolMinerErrorLogWriter;
        private static StreamWriter PoolMinerLogWriter;
        private static StreamWriter PoolPaymentErrorLogWriter;
        private static StreamWriter PoolPaymentLogWriter;
        private static StreamWriter PoolWalletErrorLogWriter;
        private static StreamWriter PoolWalletLogWriter;
        private static StreamWriter PoolApiErrorLogWriter;
        private static StreamWriter PoolApiLogWriter;

        /// <summary>
        /// Contains logs to write.
        /// </summary>
        private static List<Tuple<int, string>> ListOfLog = new List<Tuple<int, string>>(); // Structure Tuple => log id, content text.

        /// <summary>
        /// Write log settings.
        /// </summary>
        private const int WriteLogBufferSize = 8192;

        private static Thread ThreadAutoWriteLog;

        /// <summary>
        /// Log Initialization.
        /// </summary>
        /// <returns></returns>
        public static bool LogInitialization(bool fromThread = false)
        {
            try
            {
                LogInitializationFile();
                LogInitizaliationStreamWriter();
                if (!fromThread)
                {
                    AutoWriteLog();
                }
            }
            catch(Exception error)
            {
                ConsoleWriteLog("Failed to initialize log system, exception error: " + error.Message, 0, 2, true);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Stop log system.
        /// </summary>
        public static void StopLogSystem()
        {
            if (ThreadAutoWriteLog != null && (ThreadAutoWriteLog.IsAlive || ThreadAutoWriteLog != null))
            {
                ThreadAutoWriteLog.Abort();
                GC.SuppressFinalize(ThreadAutoWriteLog);
            }
            LogCloseStreamWriter();
        }

        /// <summary>
        /// Create the log directory and log files if they not exist.
        /// </summary>
        private static bool LogInitializationFile()
        {
            if (Directory.Exists(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory+ LogDirectory)) == false)
            {
                Directory.CreateDirectory(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory+ LogDirectory));
                return false;
            }

            if (!File.Exists(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory+ LogPoolGeneralError)))
            {
                File.Create(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory+ LogPoolGeneralError)).Close();
                return false;
            }
            if (!File.Exists(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory+ LogPoolGeneral)))
            {
                File.Create(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory+ LogPoolGeneral)).Close();
                return false;
            }
            if (!File.Exists(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory+ LogPoolFilteringError)))
            {
                File.Create(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory+ LogPoolFilteringError)).Close();
                return false;
            }
            if (!File.Exists(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory+ LogPoolFiltering)))
            {
                File.Create(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory+ LogPoolFiltering)).Close();
                return false;
            }
            if (!File.Exists(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory+ LogPoolMinerCheckStatsError)))
            {
                File.Create(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory+ LogPoolMinerCheckStatsError)).Close();
                return false;
            }
            if (!File.Exists(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory+ LogPoolMinerCheckStats)))
            {
                File.Create(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory+ LogPoolMinerCheckStats)).Close();
                return false;
            }
            if (!File.Exists(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory+ LogPoolPaymentError)))
            {
                File.Create(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory+ LogPoolPaymentError)).Close();
                return false;
            }
            if (!File.Exists(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory+ LogPoolPayment)))
            {
                File.Create(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory+ LogPoolPayment)).Close();
                return false;
            }
            if (!File.Exists(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory + LogPoolWalletError)))
            {
                File.Create(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory + LogPoolWalletError)).Close();
                return false;
            }
            if (!File.Exists(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory + LogPoolWallet)))
            {
                File.Create(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory + LogPoolWallet)).Close();
                return false;
            }
            if (!File.Exists(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory + LogPoolApiError)))
            {
                File.Create(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory + LogPoolApiError)).Close();
                return false;
            }
            if (!File.Exists(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory + LogPoolApi)))
            {
                File.Create(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory + LogPoolApi)).Close();
                return false;
            }
            return true;
        }

        /// <summary>
        /// Initialize stream writer's for push logs into log files.
        /// </summary>
        private static void LogInitizaliationStreamWriter()
        {
            LogCloseStreamWriter();

            PoolGeneralErrorLogWriter = new StreamWriter(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory+ LogPoolGeneralError), true, Encoding.UTF8, WriteLogBufferSize) { AutoFlush = true };
            PoolGeneralLogWriter = new StreamWriter(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory+ LogPoolGeneral), true, Encoding.UTF8, WriteLogBufferSize) { AutoFlush = true };

            PoolFilteringErrorLogWriter = new StreamWriter(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory+ LogPoolFilteringError), true, Encoding.UTF8, WriteLogBufferSize) { AutoFlush = true };
            PoolFilteringLogWriter = new StreamWriter(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory+ LogPoolFiltering), true, Encoding.UTF8, WriteLogBufferSize) { AutoFlush = true };

            PoolMinerCheckStatsErrorLogWriter = new StreamWriter(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory+ LogPoolMinerCheckStatsError), true, Encoding.UTF8, WriteLogBufferSize) { AutoFlush = true };
            PoolMinerCheckStatsLogWriter = new StreamWriter(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory+ LogPoolMinerCheckStats), true, Encoding.UTF8, WriteLogBufferSize) { AutoFlush = true };

            PoolMinerErrorLogWriter = new StreamWriter(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory+ LogPoolMinerError), true, Encoding.UTF8, WriteLogBufferSize) { AutoFlush = true };
            PoolMinerLogWriter = new StreamWriter(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory+ LogPoolMiner), true, Encoding.UTF8, WriteLogBufferSize) { AutoFlush = true };

            PoolPaymentErrorLogWriter = new StreamWriter(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory+ LogPoolPaymentError), true, Encoding.UTF8, WriteLogBufferSize) { AutoFlush = true };
            PoolPaymentLogWriter = new StreamWriter(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory+ LogPoolPayment), true, Encoding.UTF8, WriteLogBufferSize) { AutoFlush = true };

            PoolWalletErrorLogWriter = new StreamWriter(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory+ LogPoolWalletError), true, Encoding.UTF8, WriteLogBufferSize) { AutoFlush = true };
            PoolWalletLogWriter = new StreamWriter(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory+ LogPoolWallet), true, Encoding.UTF8, WriteLogBufferSize) { AutoFlush = true };

            PoolApiErrorLogWriter = new StreamWriter(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory + LogPoolApiError), true, Encoding.UTF8, WriteLogBufferSize) { AutoFlush = true };
            PoolApiLogWriter = new StreamWriter(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory + LogPoolApi), true, Encoding.UTF8, WriteLogBufferSize) { AutoFlush = true };
        }

        /// <summary>
        /// Close stream writer objects.
        /// </summary>
        private static void LogCloseStreamWriter()
        {
            PoolGeneralErrorLogWriter?.Close();
            PoolGeneralLogWriter?.Close();
            PoolFilteringErrorLogWriter?.Close();
            PoolFilteringLogWriter?.Close();
            PoolMinerCheckStatsErrorLogWriter?.Close();
            PoolMinerCheckStatsLogWriter?.Close();
            PoolMinerErrorLogWriter?.Close();
            PoolMinerLogWriter?.Close();
            PoolPaymentErrorLogWriter?.Close();
            PoolPaymentLogWriter?.Close();
            PoolWalletErrorLogWriter?.Close();
            PoolWalletLogWriter?.Close();
            PoolApiErrorLogWriter?.Close();
            PoolApiLogWriter?.Close();
        }

        /// <summary>
        /// Log on the console.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="logId"></param>
        /// <param name="logLevel"></param>
        /// <param name="writeLog"></param>
        public static void ConsoleWriteLog(string text, int logId, int colorId = 0, bool consoleLog = false)
        {
            text = "<" + ClassConnectorSetting.CoinName + "> - " + DateTime.Now + " | " + text;

            InsertLog(text, logId);
            if (consoleLog)
            {
                switch (colorId)
                {
                    case ClassLogConsoleEnumeration.IndexPoolConsoleGreenLog:
                        Console.ForegroundColor = ConsoleColor.Green;
                        break;
                    case ClassLogConsoleEnumeration.IndexPoolConsoleYellowLog:
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        break;
                    case ClassLogConsoleEnumeration.IndexPoolConsoleRedLog:
                        Console.ForegroundColor = ConsoleColor.Red;
                        break;
                    case ClassLogConsoleEnumeration.IndexPoolConsoleBlueLog:
                        Console.ForegroundColor = ConsoleColor.Blue;
                        break;
                    case ClassLogConsoleEnumeration.IndexPoolConsoleMagentaLog:
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        break;
                    case ClassLogConsoleEnumeration.IndexPoolConsoleWhiteLog:
                    default:
                        Console.ForegroundColor = ConsoleColor.White;
                        break;
                }

                Console.WriteLine(text);
                Console.ForegroundColor = ConsoleColor.White;
            }
        }

        /// <summary>
        /// Insert logs inside the list of logs to write.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="logId"></param>
        private static void InsertLog(string text, int logId)
        {
            try
            {
                ListOfLog.Add(new Tuple<int, string>(logId, text));
            }
            catch
            {

            }
        }

        /// <summary>
        /// Auto write logs
        /// </summary>
        private static void AutoWriteLog()
        {
            if (ThreadAutoWriteLog != null && (ThreadAutoWriteLog.IsAlive || ThreadAutoWriteLog != null))
            {
                ThreadAutoWriteLog.Abort();
                GC.SuppressFinalize(ThreadAutoWriteLog);
            }
            ThreadAutoWriteLog = new Thread(delegate ()
            {
                while(!Program.Exit)
                {
                    try
                    {
                        if (ListOfLog.Count > 0)
                        {
                            if (ListOfLog.Count >= MiningPoolSetting.MiningPoolWriteLogMinimumLogLine)
                            {
                                if (!LogInitializationFile()) // Remake log files if one of them missing, close and open again streamwriter's.
                                {
                                    LogInitizaliationStreamWriter();
                                }
                                var copyOfLog = new List<Tuple<int, string>>(ListOfLog);
                                ListOfLog.Clear();
                                if (copyOfLog.Count > 0)
                                {
                                    foreach(var log in copyOfLog)
                                    {
                                        WriteLog(log.Item2, log.Item1);
                                    }
                                }
                                copyOfLog.Clear();
                            }
                        }
                    }
                    catch
                    {
                        try
                        {
                            ListOfLog.Clear();
                        }
                        catch
                        {
                            LogInitialization(true);
                        }
                    }
                    Thread.Sleep(MiningPoolSetting.MiningPoolWriteLogInterval);
                }
            });
            ThreadAutoWriteLog.Start();
        }

        /// <summary>
        /// Write log on the selected log file in async mode.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="idLog"></param>
        private static void WriteLog(string text, int idLog)
        {
            switch (idLog)
            {
                case ClassLogEnumeration.IndexPoolGeneralErrorLog:
                    PoolGeneralErrorLogWriter.WriteLine(text);
                    break;
                case ClassLogEnumeration.IndexPoolGeneralLog:
                    PoolGeneralLogWriter.WriteLine(text);
                    break;
                case ClassLogEnumeration.IndexPoolFilteringErrorLog:
                    PoolFilteringErrorLogWriter.WriteLine(text);
                    break;
                case ClassLogEnumeration.IndexPoolFilteringLog:
                    PoolFilteringLogWriter.WriteLine(text);
                    break;
                case ClassLogEnumeration.IndexPoolCheckStatsErrorLog:
                    PoolMinerCheckStatsErrorLogWriter.WriteLine(text);
                    break;
                case ClassLogEnumeration.IndexPoolCheckStatsLog:
                    PoolMinerCheckStatsLogWriter.WriteLine(text);
                    break;
                case ClassLogEnumeration.IndexPoolMinerErrorLog:
                    PoolMinerErrorLogWriter.WriteLine(text);
                    break;
                case ClassLogEnumeration.IndexPoolMinerLog:
                    PoolMinerLogWriter.WriteLine(text);
                    break;
                case ClassLogEnumeration.IndexPoolPaymentErrorLog:
                    PoolPaymentErrorLogWriter.WriteLine(text);
                    break;
                case ClassLogEnumeration.IndexPoolPaymentLog:
                    PoolPaymentLogWriter.WriteLine(text);
                    break;
                case ClassLogEnumeration.IndexPoolWalletErrorLog:
                    PoolWalletErrorLogWriter.WriteLine(text);
                    break;
                case ClassLogEnumeration.IndexPoolWalletLog:
                    PoolWalletLogWriter.WriteLine(text);
                    break;
                case ClassLogEnumeration.IndexPoolApiErrorLog:
                    PoolApiErrorLogWriter.WriteLine(text);
                    break;
                case ClassLogEnumeration.IndexPoolApiLog:
                    PoolApiLogWriter.WriteLine(text);
                    break;
            }
        }
    }
}
