﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Xiropht_Mining_Pool.Log;
using Xiropht_Mining_Pool.Miner;
using Xiropht_Mining_Pool.Mining;
using Xiropht_Mining_Pool.Utility;

namespace Xiropht_Mining_Pool.Database
{
    public class ClassMiningPoolMinerDatabaseEnumeration
    {
        public const string DatabaseMinerStart = "[MINER]";
        public const string DatabaseMinerWalletAddress = "[WALLET]";
        public const string DatabaseMinerTotalGoodShare = "[TOTAL_GOOD_SHARE]";
        public const string DatabaseMinerTotalInvalidShare = "[TOTAL_INVALID_SHARE]";
        public const string DatabaseMinerTotalBalance = "[TOTAL_BALANCE]";
        public const string DatabaseMinerTotalPaid = "[TOTAL_PAID]";
        public const string DatabaseMinerTotalMiningScore = "[TOTAL_MINING_SCORE]";
        public const string DatabaseMinerCustomMinimumPayment = "[CUSTOM_MINIMUM_PAYMENT]";
    }

    public class ClassMiningPoolDatabaseEnumeration
    {
        public const string DatabasePoolListBlockFound = "[BLOCK]";
    }

    public class ClassMiningPoolTransactionDatabaseEnumeration
    {
        public const string DatabaseTransactionStart = "[TRANSACTION]";
    }

    public class ClassMiningPoolDatabase
    {
        private const string MinerDatabaseFile = "\\miner.xirdb";
        private const string PoolDatabaseFile = "\\pool.xirdb";
        private const string MinerTransactionDatabaseFile = "\\pooltransaction.xirdb";
        private static Thread ThreadAutoSaveMiningPoolDatabases;
        private const int AutoSaveMiningPoolDatabasesInterval = 1000;

        /// <summary>
        /// Read database files of the mining pool.
        /// </summary>
        public static bool InitializationMiningPoolDatabases()
        {
            try
            {
                #region Read Database Miner stats
                if (File.Exists(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory + MinerDatabaseFile)))
                {
                    using (FileStream fs = File.Open(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory + MinerDatabaseFile), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        using (BufferedStream bs = new BufferedStream(fs))
                        {
                            using (StreamReader sr = new StreamReader(bs))
                            {
                                string line;
                                while ((line = sr.ReadLine()) != null)
                                {
                                    if (line.StartsWith(ClassMiningPoolMinerDatabaseEnumeration.DatabaseMinerStart))
                                    {
                                        string minerWalletAddress = string.Empty;
                                        var minerLine = line.Replace(ClassMiningPoolMinerDatabaseEnumeration.DatabaseMinerStart, "");
                                        var splitMinerLine = minerLine.Split(new[] { "|" }, StringSplitOptions.None);
                                        foreach (var minerInfo in splitMinerLine)
                                        {
                                            if (minerInfo != null)
                                            {
                                                if (!string.IsNullOrEmpty(minerInfo))
                                                {
                                                    if (minerInfo.StartsWith(ClassMiningPoolMinerDatabaseEnumeration.DatabaseMinerWalletAddress))
                                                    {
                                                        minerWalletAddress = minerInfo.Replace(ClassMiningPoolMinerDatabaseEnumeration.DatabaseMinerWalletAddress, "");
                                                        if (!ClassMinerStats.DictionaryMinerStats.ContainsKey(minerWalletAddress))
                                                        {
                                                            ClassMinerStats.DictionaryMinerStats.Add(minerWalletAddress, new ClassMinerStatsObject());
                                                            ClassMinerStats.DictionaryMinerTransaction.Add(minerWalletAddress, new List<string>());
                                                        }
                                                    }
                                                    if (!string.IsNullOrEmpty(minerWalletAddress))
                                                    {
                                                        if (minerInfo.StartsWith(ClassMiningPoolMinerDatabaseEnumeration.DatabaseMinerTotalGoodShare))
                                                        {
                                                            ClassMinerStats.DictionaryMinerStats[minerWalletAddress].TotalGoodShare = long.Parse(minerInfo.Replace(ClassMiningPoolMinerDatabaseEnumeration.DatabaseMinerTotalGoodShare, ""));
                                                        }
                                                        else if (minerInfo.StartsWith(ClassMiningPoolMinerDatabaseEnumeration.DatabaseMinerTotalInvalidShare))
                                                        {
                                                            ClassMinerStats.DictionaryMinerStats[minerWalletAddress].TotalInvalidShare = float.Parse(minerInfo.Replace(ClassMiningPoolMinerDatabaseEnumeration.DatabaseMinerTotalInvalidShare, ""));
                                                        }
                                                        else if (minerInfo.StartsWith(ClassMiningPoolMinerDatabaseEnumeration.DatabaseMinerTotalMiningScore))
                                                        {
                                                            ClassMinerStats.DictionaryMinerStats[minerWalletAddress].TotalMiningScore = long.Parse(minerInfo.Replace(ClassMiningPoolMinerDatabaseEnumeration.DatabaseMinerTotalMiningScore, ""));
                                                        }
                                                        else if (minerInfo.StartsWith(ClassMiningPoolMinerDatabaseEnumeration.DatabaseMinerTotalBalance))
                                                        {
                                                            ClassMinerStats.DictionaryMinerStats[minerWalletAddress].TotalBalance = decimal.Parse(minerInfo.Replace(ClassMiningPoolMinerDatabaseEnumeration.DatabaseMinerTotalBalance, "").Replace(".", ","), NumberStyles.Currency, Program.GlobalCultureInfo);
                                                        }
                                                        else if (minerInfo.StartsWith(ClassMiningPoolMinerDatabaseEnumeration.DatabaseMinerCustomMinimumPayment))
                                                        {
                                                            ClassMinerStats.DictionaryMinerStats[minerWalletAddress].CustomMinimumPayment = decimal.Parse(minerInfo.Replace(ClassMiningPoolMinerDatabaseEnumeration.DatabaseMinerCustomMinimumPayment, "").Replace(".", ","), NumberStyles.Currency, Program.GlobalCultureInfo);
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    File.Create(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory + MinerDatabaseFile)).Close();
                }
                #endregion

                #region Read Database Pool
                if (File.Exists(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory + PoolDatabaseFile)))
                {
                    using (FileStream fs = File.Open(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory + PoolDatabaseFile), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        using (BufferedStream bs = new BufferedStream(fs))
                        {
                            using (StreamReader sr = new StreamReader(bs))
                            {
                                string line;
                                while ((line = sr.ReadLine()) != null)
                                {
                                    var splitBlockFound = line;
                                    ClassMiningPoolGlobalStats.ListBlockFound.Add(ClassMiningPoolGlobalStats.ListBlockFound.Count, splitBlockFound);
                                }
                            }
                        }
                    }
                }
                else
                {
                    File.Create(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory + PoolDatabaseFile)).Close();
                }
                #endregion

                #region Read Database Transactions
                if (File.Exists(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory + MinerTransactionDatabaseFile)))
                {
                    using (FileStream fs = File.Open(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory + MinerTransactionDatabaseFile), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        using (BufferedStream bs = new BufferedStream(fs))
                        {
                            using (StreamReader sr = new StreamReader(bs))
                            {
                                string line;
                                List<KeyValuePair<long, string>> ListTransactionPool = new List<KeyValuePair<long, string>>();
                                while ((line = sr.ReadLine()) != null)
                                {
                                    if (line.StartsWith(ClassMiningPoolTransactionDatabaseEnumeration.DatabaseTransactionStart))
                                    {
                                        var transactionLine = line.Replace(ClassMiningPoolTransactionDatabaseEnumeration.DatabaseTransactionStart, "");
                                        var splitTransactionLine = transactionLine.Split(new[] { "|" }, StringSplitOptions.None);
                                        if (ClassMinerStats.DictionaryMinerTransaction.ContainsKey(splitTransactionLine[0]))
                                        {
                                            ClassMinerStats.DictionaryMinerTransaction[splitTransactionLine[0]].Add(splitTransactionLine[1] + "|" + splitTransactionLine[2] + "|" + splitTransactionLine[3] + "|" + splitTransactionLine[4]);
                                        }
                                        else
                                        {
                                            ClassMinerStats.DictionaryMinerTransaction.Add(splitTransactionLine[0], new List<string>() { splitTransactionLine[1] + "|" + splitTransactionLine[2] + "|" + splitTransactionLine[3] + "|" + splitTransactionLine[4]});
                                        }
                                        if (!ClassMinerStats.DictionaryMinerStats.ContainsKey(splitTransactionLine[0]))
                                        {
                                            ClassMinerStats.DictionaryMinerStats.Add(splitTransactionLine[0], new ClassMinerStatsObject());
                                        }
                                        long dateSend = long.Parse(splitTransactionLine[4]);
                                        decimal amountPaid = decimal.Parse(splitTransactionLine[2]);
                                        decimal feePaid = decimal.Parse(splitTransactionLine[3]);
                                        ClassMinerStats.DictionaryMinerStats[splitTransactionLine[0]].TotalPaid += (amountPaid + feePaid);
                                        string transactionInfo = splitTransactionLine[1] + "|" + splitTransactionLine[2] + "|" + splitTransactionLine[3] + "|" + splitTransactionLine[4];
                                        KeyValuePair<long, string> transactionKeyValuePair = new KeyValuePair<long, string>(dateSend, transactionInfo);
                                        ListTransactionPool.Add(transactionKeyValuePair);
                                        ClassMiningPoolGlobalStats.PoolTotalPaid += (amountPaid + feePaid);
                                    }
                                }
                                if (ListTransactionPool.Count > 0)
                                {
                                    ListTransactionPool = ListTransactionPool.OrderBy(x => x.Key).ToList();
                                    foreach(var transactionPool in ListTransactionPool)
                                    {
                                        ClassMinerStats.DictionaryPoolTransaction.Add(ClassMinerStats.DictionaryPoolTransaction.Count, transactionPool.Value);
                                    }
                                    ListTransactionPool.Clear();
                                }
                            }
                        }
                    }
                }
                else
                {
                    File.Create(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory + MinerTransactionDatabaseFile)).Close();
                }
                #endregion
            }
            catch (Exception error)
            {
                ClassLog.ConsoleWriteLog("Error on initialization of mining pool databases, exception: " + error.Message, ClassLogEnumeration.IndexPoolGeneralErrorLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Auto save mining pools databases.
        /// </summary>
        public static void AutoSaveMiningPoolDatabases()
        {
            if (ThreadAutoSaveMiningPoolDatabases != null && (ThreadAutoSaveMiningPoolDatabases.IsAlive || ThreadAutoSaveMiningPoolDatabases != null))
            {
                ThreadAutoSaveMiningPoolDatabases.Abort();
                GC.SuppressFinalize(ThreadAutoSaveMiningPoolDatabases);
            }
            ThreadAutoSaveMiningPoolDatabases = new Thread(delegate ()
            {
                while (!Program.Exit)
                {
                    try
                    {
                        #region Save Database Miner Stats
                        if (ClassMinerStats.DictionaryMinerStats.Count > 0)
                        {
                            File.Create(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory + MinerDatabaseFile)).Close();
                            using (var minerWriter = new StreamWriter(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory + MinerDatabaseFile), true, Encoding.UTF8, 8192) { AutoFlush = true })
                            {
                                foreach (var miner in ClassMinerStats.DictionaryMinerStats)
                                {
                                    if (!string.IsNullOrEmpty(miner.Key))
                                    {
                                        string line = ClassMiningPoolMinerDatabaseEnumeration.DatabaseMinerStart +
                                                      ClassMiningPoolMinerDatabaseEnumeration.DatabaseMinerWalletAddress + miner.Key + "|" +
                                                      ClassMiningPoolMinerDatabaseEnumeration.DatabaseMinerTotalGoodShare + miner.Value.TotalGoodShare + "|" +
                                                      ClassMiningPoolMinerDatabaseEnumeration.DatabaseMinerTotalInvalidShare + miner.Value.TotalInvalidShare + "|" +
                                                      ClassMiningPoolMinerDatabaseEnumeration.DatabaseMinerTotalMiningScore + miner.Value.TotalMiningScore + "|" +
                                                      ClassMiningPoolMinerDatabaseEnumeration.DatabaseMinerTotalBalance + miner.Value.TotalBalance + "|" +
                                                      ClassMiningPoolMinerDatabaseEnumeration.DatabaseMinerTotalPaid + miner.Value.TotalPaid + "|" +
                                                      ClassMiningPoolMinerDatabaseEnumeration.DatabaseMinerCustomMinimumPayment + miner.Value.CustomMinimumPayment;
                                        minerWriter.WriteLine(line);
                                    }
                                }
                            }
                            ClassLog.ConsoleWriteLog("Auto save miner database: " + ClassMinerStats.DictionaryMinerStats.Count + " total miners saved.", ClassLogEnumeration.IndexPoolGeneralLog);
                        }
                        #endregion
                        Thread.Sleep(AutoSaveMiningPoolDatabasesInterval);

                        #region Save Database Pool
                        if (ClassMiningPoolGlobalStats.ListBlockFound.Count > 0)
                        {
                            File.Create(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory + PoolDatabaseFile)).Close();
                            int totalBlockFound = ClassMiningPoolGlobalStats.ListBlockFound.Count;
                            using (var poolWriter = new StreamWriter(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory + PoolDatabaseFile), true, Encoding.UTF8, 8192) { AutoFlush = true })
                            {
                                for (int i = 0; i < totalBlockFound; i++)
                                {
                                    if (i < totalBlockFound)
                                    {
                                        poolWriter.WriteLine(ClassMiningPoolGlobalStats.ListBlockFound[i]);
                                    }
                                }
                            }
                            ClassLog.ConsoleWriteLog("Auto save pool database: " + totalBlockFound + " total blocks found saved.", ClassLogEnumeration.IndexPoolGeneralLog);
                        }
                        #endregion
                        Thread.Sleep(AutoSaveMiningPoolDatabasesInterval);

                    }
                    catch(Exception error)
                    {
                        ClassLog.ConsoleWriteLog("Auto save databases of the pool exception: "+error.Message+" retry after few seconds", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
                        Thread.Sleep(5000);
                    }
                }
            });
            ThreadAutoSaveMiningPoolDatabases.Start();
        }

        /// <summary>
        /// Stop auto save mining pool databases.
        /// </summary>
        public static void StopAutoSaveMiningPoolDatabases()
        {
            if (ThreadAutoSaveMiningPoolDatabases != null && (ThreadAutoSaveMiningPoolDatabases.IsAlive || ThreadAutoSaveMiningPoolDatabases != null))
            {
                ThreadAutoSaveMiningPoolDatabases.Abort();
                GC.SuppressFinalize(ThreadAutoSaveMiningPoolDatabases);
            }
            #region Save Database Miner Stats
            if (ClassMinerStats.DictionaryMinerStats.Count > 0)
            {
                File.Create(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory + MinerDatabaseFile)).Close();
                using (var minerWriter = new StreamWriter(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory + MinerDatabaseFile), true, Encoding.UTF8, 8192) { AutoFlush = true })
                {
                    foreach (var miner in ClassMinerStats.DictionaryMinerStats)
                    {
                        if (!string.IsNullOrEmpty(miner.Key))
                        {
                            string line = ClassMiningPoolMinerDatabaseEnumeration.DatabaseMinerStart +
                                          ClassMiningPoolMinerDatabaseEnumeration.DatabaseMinerWalletAddress + miner.Key + "|" +
                                          ClassMiningPoolMinerDatabaseEnumeration.DatabaseMinerTotalGoodShare + miner.Value.TotalGoodShare + "|" +
                                          ClassMiningPoolMinerDatabaseEnumeration.DatabaseMinerTotalInvalidShare + miner.Value.TotalInvalidShare + "|" +
                                          ClassMiningPoolMinerDatabaseEnumeration.DatabaseMinerTotalMiningScore + miner.Value.TotalMiningScore + "|" +
                                          ClassMiningPoolMinerDatabaseEnumeration.DatabaseMinerTotalBalance + miner.Value.TotalBalance + "|" +
                                          ClassMiningPoolMinerDatabaseEnumeration.DatabaseMinerTotalPaid + miner.Value.TotalPaid + "|" +
                                          ClassMiningPoolMinerDatabaseEnumeration.DatabaseMinerCustomMinimumPayment + miner.Value.CustomMinimumPayment;
                            minerWriter.WriteLine(line);
                        }
                    }
                }
                ClassLog.ConsoleWriteLog("Auto save miner database: " + ClassMinerStats.DictionaryMinerStats.Count + " total miners saved.", ClassLogEnumeration.IndexPoolGeneralLog);
            }
            #endregion


            #region Save Database Pool
            if (ClassMiningPoolGlobalStats.ListBlockFound.Count > 0)
            {
                File.Create(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory + PoolDatabaseFile)).Close();
                int totalBlockFound = ClassMiningPoolGlobalStats.ListBlockFound.Count;
                using (var poolWriter = new StreamWriter(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory + PoolDatabaseFile), true, Encoding.UTF8, 8192) { AutoFlush = true })
                {
                    for (int i = 0; i < totalBlockFound; i++)
                    {
                        if (i < totalBlockFound)
                        {
                            poolWriter.WriteLine(ClassMiningPoolGlobalStats.ListBlockFound[i]);
                        }
                    }
                }
                ClassLog.ConsoleWriteLog("Auto save pool database: " + totalBlockFound + " total blocks found saved.", ClassLogEnumeration.IndexPoolGeneralLog);
            }
            #endregion


            #region Save Database Transactions
            if (ClassMinerStats.DictionaryMinerTransaction.Count > 0)
            {
                File.Create(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory + MinerTransactionDatabaseFile)).Close();
                using (var transactionMinerWriter = new StreamWriter(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory + MinerTransactionDatabaseFile), true, Encoding.UTF8, 8192) { AutoFlush = true })
                {
                    foreach (var miner in ClassMinerStats.DictionaryMinerTransaction)
                    {
                        if (!string.IsNullOrEmpty(miner.Key))
                        {
                            if (miner.Value.Count > 0)
                            {
                                foreach (var transaction in miner.Value)
                                {
                                    string line = ClassMiningPoolTransactionDatabaseEnumeration.DatabaseTransactionStart + miner.Key + "|" + transaction;
                                    transactionMinerWriter.WriteLine(line);
                                }
                            }
                        }
                    }
                }
            }

            #endregion

        }

        /// <summary>
        /// Save transaction database.
        /// </summary>
        public static void SaveTransactionPoolDatabase(string transaction)
        {
            using (var transactionWriter = new StreamWriter(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory + MinerTransactionDatabaseFile), true, Encoding.UTF8, 8192) { AutoFlush = true })
            {
                transactionWriter.WriteLine(ClassMiningPoolTransactionDatabaseEnumeration.DatabaseTransactionStart+transaction);
            }
        }
    }
}
