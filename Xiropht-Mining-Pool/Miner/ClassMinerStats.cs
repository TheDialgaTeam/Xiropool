using System;
using System.Collections.Generic;
using System.Threading;
using Xiropht_Mining_Pool.Database;
using Xiropht_Mining_Pool.Log;
using Xiropht_Mining_Pool.Mining;
using Xiropht_Mining_Pool.Setting;
using Xiropht_Mining_Pool.Utility;

namespace Xiropht_Mining_Pool.Miner
{

    public class ClassMinerStatsObject
    {
        /// <summary>
        /// Static informations.
        /// </summary>
        public long TotalGoodShare;
        public float TotalInvalidShare;
        public long TotalMiningScore;
        public decimal TotalBalance;
        public decimal TotalPaid;
        public decimal CustomMinimumPayment;

        /// <summary>
        /// Dynamic informations for filtering
        /// </summary>
        public float CurrentTotalHashrate;
        public long CurrentTotalGoodShare;
        public long CurrentTotalInvalidShare;
        public long DateOfLastGoodShare;
        public long DateOfLastInvalidShare;
        public bool IsBanned;
        public long DateOfTrustedShare;
        public bool IsTrusted;
        public long DateOfBan;
        public List<MinerTcpObject> ListOfMinerTcpObject = new List<MinerTcpObject>();
    }

    public class ClassMinerStats
    {
        public static Dictionary<string, ClassMinerStatsObject> DictionaryMinerStats = new Dictionary<string, ClassMinerStatsObject>();
        public static Dictionary<string, List<string>> DictionaryMinerTransaction = new Dictionary<string, List<string>>();
        public static Dictionary<int, string> DictionaryPoolTransaction = new Dictionary<int, string>();

        private static Thread ThreadCheckMinerStats;
        private const int ThreadCheckMinerStatsInterval = 1000;

        private static Thread ThreadCheckTrustedMinerStats;

        /// <summary>
        /// Check miner stats.
        /// </summary>
        public static void EnableCheckMinerStats()
        {

            if (MiningPoolSetting.MiningPoolMinimumInvalidShare > 0)
            {
                if (MiningPoolSetting.MiningPoolMinerBanTime > 0)
                {
                    ThreadCheckMinerStats = new Thread(delegate ()
                    {
                        while (!Program.Exit)
                        {
                            try
                            {
                                if (DictionaryMinerStats.Count > 0)
                                {
                                    int totalConnectedMiner = 0;
                                    int totalConnectedWorker = 0;
                                    foreach (var minerStats in DictionaryMinerStats)
                                    {
                                        if (minerStats.Value.IsBanned)
                                        {
                                            if (minerStats.Value.DateOfBan < DateTimeOffset.Now.ToUnixTimeSeconds())
                                            {
                                                minerStats.Value.IsBanned = false;
                                                minerStats.Value.CurrentTotalInvalidShare = 0;
                                                ClassLog.ConsoleWriteLog("Unban Wallet Address: " + minerStats.Key + ".", ClassLogEnumeration.IndexPoolCheckStatsLog);
                                            }
                                        }
                                        else
                                        {
                                            if(minerStats.Value.DateOfBan >= DateTimeOffset.Now.ToUnixTimeSeconds())
                                            {
                                                minerStats.Value.IsBanned = true;
                                                minerStats.Value.DateOfBan = DateTimeOffset.Now.ToUnixTimeSeconds() + MiningPoolSetting.MiningPoolMinerBanTime;
                                                ClassLog.ConsoleWriteLog("Banned Wallet Address: " + minerStats.Key + ".", ClassLogEnumeration.IndexPoolCheckStatsLog);
                                            }
                                            else if (minerStats.Value.CurrentTotalInvalidShare >= MiningPoolSetting.MiningPoolMinimumInvalidShare)
                                            {
                                                minerStats.Value.IsBanned = true;
                                                minerStats.Value.DateOfBan = DateTimeOffset.Now.ToUnixTimeSeconds() + MiningPoolSetting.MiningPoolMinerBanTime;
                                                ClassLog.ConsoleWriteLog("Banned Wallet Address: " + minerStats.Key + " for too much invalid packets.", ClassLogEnumeration.IndexPoolCheckStatsLog);
                                            }
                                            try
                                            {
                                                if (!minerStats.Value.IsBanned)
                                                {
                                                    if (minerStats.Value.ListOfMinerTcpObject.Count > 0)
                                                    {
                                                        float tmpTotalHashrate = 0;
                                                        bool connectedChecked = false;
                                                        for (int i = 0; i < minerStats.Value.ListOfMinerTcpObject.Count; i++)
                                                        {
                                                            if (i < minerStats.Value.ListOfMinerTcpObject.Count)
                                                            {
                                                                if (minerStats.Value.ListOfMinerTcpObject[i] != null)
                                                                {
                                                                    if (minerStats.Value.ListOfMinerTcpObject[i].IsConnected && minerStats.Value.ListOfMinerTcpObject[i].IsLogged)
                                                                    {
                                                                        if (!connectedChecked)
                                                                        {
                                                                            connectedChecked = true;
                                                                            totalConnectedMiner++;
                                                                        }
                                                                        totalConnectedWorker++;
                                                                        tmpTotalHashrate += minerStats.Value.ListOfMinerTcpObject[i].CurrentHashrate;

                                                                    }
                                                                }
                                                            }
                                                        }
                                                        minerStats.Value.CurrentTotalHashrate = tmpTotalHashrate;
                                                    }
                                                    else
                                                    {
                                                        minerStats.Value.CurrentTotalHashrate = 0;
                                                    }
                                                }
                                                else
                                                {
                                                    minerStats.Value.CurrentTotalHashrate = 0;
                                                }
                                            }
                                            catch
                                            {

                                            }
                                        }
                                    }
                                    ClassMiningPoolGlobalStats.TotalMinerConnected = totalConnectedMiner;
                                    ClassMiningPoolGlobalStats.TotalWorkerConnected = totalConnectedWorker;
                                    ClassMiningPoolGlobalStats.TotalMinerHashrate = GetTotalMinerHashrate();
                                }
                            }
                            catch (Exception error)
                            {
                                ClassLog.ConsoleWriteLog("Check miner stats system exception error: " + error.Message, ClassLogEnumeration.IndexPoolCheckStatsErrorLog);
                            }
                            Thread.Sleep(ThreadCheckMinerStatsInterval);
                        }
                    });
                    ThreadCheckMinerStats.Start();
                }
                else
                {
                    // Warning, the ban time should be higher than 0 seconds, the system will not be enabled.
                    ClassLog.ConsoleWriteLog("Warning, the ban time should be higher than 0 seconds, the system will not be enabled.", ClassLogEnumeration.IndexPoolCheckStatsErrorLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
                }
            }
            else
            {
                // Warning, the limit of invalid share need to be higher than 0, the system will not be enabled.
                ClassLog.ConsoleWriteLog("Warning, the limit of invalid share need to be higher than 0, the system will not be enabled.", ClassLogEnumeration.IndexPoolCheckStatsErrorLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
            }
        }

        /// <summary>
        /// Check miner trusted stats.
        /// </summary>
        public static void EnableCheckTrustedMinerStats()
        {
            if (MiningPoolSetting.MiningPoolMinimumTrustedGoodShare > 0)
            {
                if (MiningPoolSetting.MiningPoolMinimumIntervalTrustedShare > 0)
                {
                    if (MiningPoolSetting.MiningPoolIntervalTrustedShare > 0)
                    {
                        ThreadCheckTrustedMinerStats = new Thread(delegate ()
                        {
                            while(!Program.Exit)
                            {
                                try
                                {
                                    if (DictionaryMinerStats.Count > 0)
                                    {
                                        foreach(var minerStats in DictionaryMinerStats)
                                        {
                                            if (minerStats.Value.IsBanned)
                                            {
                                                if (minerStats.Value.DateOfBan < DateTimeOffset.Now.ToUnixTimeSeconds())
                                                {
                                                    minerStats.Value.IsBanned = false;
                                                    minerStats.Value.DateOfBan = 0;
                                                    minerStats.Value.IsTrusted = false; // Keep the miner not trusted at the unban moment just in case.
                                                    ClassLog.ConsoleWriteLog("Unban Wallet Address: " + minerStats.Key + ".", ClassLogEnumeration.IndexPoolCheckStatsLog);
                                                }
                                                else
                                                {
                                                    minerStats.Value.IsTrusted = false; // Keep the miner not trusted just in case even if he is banned.
                                                }
                                            }
                                            else
                                            {
                                                if (minerStats.Value.DateOfLastInvalidShare < DateTimeOffset.Now.ToUnixTimeSeconds())
                                                {
                                                    minerStats.Value.CurrentTotalInvalidShare = 0;
                                                }
                                                if (minerStats.Value.CurrentTotalInvalidShare >= MiningPoolSetting.MiningPoolMinimumInvalidShare)
                                                {
                                                    minerStats.Value.DateOfBan = DateTimeOffset.Now.ToUnixTimeSeconds() + MiningPoolSetting.MiningPoolMinerBanTime;
                                                    minerStats.Value.IsBanned = true;
                                                    minerStats.Value.IsTrusted = false;
                                                    ClassLog.ConsoleWriteLog("Banning Wallet Address: " + minerStats.Key + " for too much invalid share.", ClassLogEnumeration.IndexPoolCheckStatsLog);
                                                }
                                                else
                                                {
                                                    if (minerStats.Value.IsTrusted)
                                                    {
                                                        if (minerStats.Value.DateOfTrustedShare < DateTimeOffset.Now.ToUnixTimeSeconds())
                                                        {
                                                            minerStats.Value.IsTrusted = false;
                                                            ClassLog.ConsoleWriteLog("End of trusting share for Wallet Address: " + minerStats.Key + ".", ClassLogEnumeration.IndexPoolCheckStatsLog);
                                                        }
                                                    }
                                                    else
                                                    {
                                                        if (minerStats.Value.DateOfLastGoodShare > DateTimeOffset.Now.ToUnixTimeSeconds())
                                                        {
                                                            if (minerStats.Value.CurrentTotalGoodShare > MiningPoolSetting.MiningPoolMinimumTrustedGoodShare)
                                                            {
                                                                minerStats.Value.IsTrusted = true;
                                                                minerStats.Value.DateOfTrustedShare = DateTimeOffset.Now.ToUnixTimeSeconds() + MiningPoolSetting.MiningPoolIntervalTrustedShare;
                                                                ClassLog.ConsoleWriteLog("Start trusting share for Wallet Address: " + minerStats.Key + ".", ClassLogEnumeration.IndexPoolCheckStatsLog);
                                                            }
                                                        }
                                                        else
                                                        {
                                                            minerStats.Value.CurrentTotalGoodShare = 0;
                                                        }
                                                    }
                                                }

                                            }
 
                                        }
                                    }
                                }
                                catch(Exception error)
                                {
                                    ClassLog.ConsoleWriteLog("Check trusted miner stats system exception error: "+error.Message, ClassLogEnumeration.IndexPoolCheckStatsErrorLog);
                                }
                                Thread.Sleep(ThreadCheckMinerStatsInterval);
                            }
                        });
                        ThreadCheckTrustedMinerStats.Start();
                    }
                    else
                    {
                        // Warning, the interval of time of trusted share require to be higher than 0, the system will not be enabled.
                        ClassLog.ConsoleWriteLog("Warning, the interval of time of trusted share require to be higher than 0, the system will not be enabled.",ClassLogEnumeration.IndexPoolCheckStatsErrorLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
                    }
                }
                else
                {
                    // Warning, minimum interval of time of trusted share require to be higher than 0 second, the system will not be enabled.
                    ClassLog.ConsoleWriteLog("Warning, minimum interval of time of trusted share require to be higher than 0 second, the system will not be enabled.", ClassLogEnumeration.IndexPoolCheckStatsErrorLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
                }
            }
            else
            {
                // Warning, minimum of trusted good share require to be higher than 0, the system will not be enabled.
                ClassLog.ConsoleWriteLog("Warning, minimum of trusted good share require to be higher than 0, the system will not be enabled.", ClassLogEnumeration.IndexPoolCheckStatsErrorLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);

            }
        }

        /// <summary>
        /// Stop check miner stats/trusted stats system.
        /// </summary>
        public static void StopCheckMinerStats()
        {
            if (ThreadCheckMinerStats != null && (ThreadCheckMinerStats.IsAlive || ThreadCheckMinerStats != null))
            {
                ThreadCheckMinerStats.Abort();
                GC.SuppressFinalize(ThreadCheckMinerStats);
            }

            if (ThreadCheckTrustedMinerStats != null && (ThreadCheckTrustedMinerStats.IsAlive || ThreadCheckTrustedMinerStats != null))
            {
                ThreadCheckTrustedMinerStats.Abort();
                GC.SuppressFinalize(ThreadCheckTrustedMinerStats);
            }
        }

        /// <summary>
        /// Check if the miner is banned on his own wallet address.
        /// </summary>
        /// <param name="walletAddress"></param>
        /// <returns></returns>
        public static bool CheckMinerIsBannedByWalletAddress(string walletAddress)
        {
            if (DictionaryMinerStats.ContainsKey(walletAddress))
            {
                if (DictionaryMinerStats[walletAddress].IsBanned)
                {
                    return true;
                }
            }
            else
            {
                try
                {
                    DictionaryMinerStats.Add(walletAddress, new ClassMinerStatsObject());
                }
                catch
                {

                }
            }
            return false;
        }

        /// <summary>
        /// Increment total good share and current good share.
        /// </summary>
        /// <param name="walletAddress"></param>
        public static void InsertGoodShareToMiner(string walletAddress, float result)
        {
            if (DictionaryMinerStats.ContainsKey(walletAddress))
            {
                DictionaryMinerStats[walletAddress].TotalGoodShare++;
                if (DictionaryMinerStats[walletAddress].IsTrusted)
                {
                    DictionaryMinerStats[walletAddress].CurrentTotalGoodShare++;
                }
                DictionaryMinerStats[walletAddress].TotalMiningScore += (long)result;
            }
            else
            {
                try
                {
                    DictionaryMinerStats.Add(walletAddress, new ClassMinerStatsObject() { TotalGoodShare = 1 });
                }
                catch
                {

                }
                try
                {
                    DictionaryMinerTransaction.Add(walletAddress, new List<string>());
                }
                catch
                {

                }
            }
        }

        /// <summary>
        /// Increment total invalid share and current invalid share.
        /// </summary>
        /// <param name="walletAddress"></param>
        public static void InsertInvalidShareToMiner(string walletAddress, bool duplicate = false)
        {
            if (DictionaryMinerStats.ContainsKey(walletAddress))
            {
                if (!duplicate)
                {
                    DictionaryMinerStats[walletAddress].TotalInvalidShare++;
                    DictionaryMinerStats[walletAddress].CurrentTotalInvalidShare++;
                }
                else
                {
                    DictionaryMinerStats[walletAddress].TotalInvalidShare += 0.5f; // Duplicate share are take in count has a half invalid share.
                }
            }
            else
            {
                try
                {
                    DictionaryMinerStats.Add(walletAddress, new ClassMinerStatsObject() { TotalInvalidShare = 1 });
                }
                catch
                {

                }
            }
        }

        /// <summary>
        /// Return the total hashrate of the pool.
        /// </summary>
        /// <returns></returns>
        private static float GetTotalMinerHashrate()
        {
            float totalHashrate = 0;

            if (DictionaryMinerStats.Count > 0)
            {
                foreach(var minerStats in DictionaryMinerStats)
                {
                    if(minerStats.Value.CurrentTotalHashrate > 0)
                    {
                        totalHashrate += minerStats.Value.CurrentTotalHashrate;
                    }
                }
            }

            return totalHashrate;
        }
    
        /// <summary>
        /// Check if the miner is trusted.
        /// </summary>
        /// <param name="walletAddress"></param>
        public static bool CheckMinerIsTrusted(string walletAddress)
        {
            if (DictionaryMinerStats.ContainsKey(walletAddress))
            {
                if (DictionaryMinerStats[walletAddress].IsTrusted)
                {
                    return true;
                }
            }
            else
            {
                try
                {
                    DictionaryMinerStats.Add(walletAddress, new ClassMinerStatsObject());
                }
                catch
                {

                }
            }
            return false;
        }

        /// <summary>
        /// Insert a miner tcp object.
        /// </summary>
        /// <param name="walletAddress"></param>
        /// <param name="minerTcpObject"></param>
        public static void InsertMinerTcpObject(string walletAddress, MinerTcpObject minerTcpObject)
        {
            if (DictionaryMinerStats.ContainsKey(walletAddress))
            {
                DictionaryMinerStats[walletAddress].ListOfMinerTcpObject.Add(minerTcpObject);
            }
        }

        /// <summary>
        /// Permit to ban manualy a wallet address with a delay selected.
        /// </summary>
        /// <param name="wallet"></param>
        /// <param name="time"></param>
        /// <returns></returns>
        public static bool ManualBanWalletAddress(string walletAddress, int time)
        {
            if (DictionaryMinerStats.ContainsKey(walletAddress))
            {
                DictionaryMinerStats[walletAddress].DateOfBan = ClassUtility.GetCurrentDateInSecond() + time;
                DictionaryMinerStats[walletAddress].IsBanned = true;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Insert a transaction sent from payment system to a miner target.
        /// </summary>
        /// <param name="walletAddress"></param>
        /// <param name="hash"></param>
        /// <param name="minerBalance"></param>
        public static void InsertTransactionPayment(string walletAddress, string hash, decimal minerBalance, long dateSent)
        {
            if (DictionaryMinerTransaction.ContainsKey(walletAddress))
            {
                DictionaryMinerTransaction[walletAddress].Add(hash + "|" + minerBalance + "|" + MiningPoolSetting.MiningPoolFeeTransactionPayment + "|" + dateSent);
            }
            else
            {
                DictionaryMinerTransaction.Add(walletAddress, new List<string>() { hash + "|" + minerBalance + "|" + MiningPoolSetting.MiningPoolFeeTransactionPayment + "|" + dateSent });
            }
            DictionaryPoolTransaction.Add(DictionaryPoolTransaction.Count, hash + "|" + minerBalance + "|" + MiningPoolSetting.MiningPoolFeeTransactionPayment + "|" + dateSent);
            ClassMiningPoolDatabase.SaveTransactionPoolDatabase(walletAddress + "|" + hash + "|" + minerBalance + "|" + MiningPoolSetting.MiningPoolFeeTransactionPayment + "|" + dateSent);
        }
    }
}
