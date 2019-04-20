using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Xiropht_Mining_Pool.Log;
using Xiropht_Mining_Pool.Setting;

namespace Xiropht_Mining_Pool.Miner
{
    public class ClassFilteringEnumerationFirewallName
    {
        public const string IptableFirewall = "iptables";
        public const string PacketFilterFirewall = "pf";
    }

    public class ClassFilteringObjectMiner
    {
        public long DateOfBan;
        public int TotalInvalidPacket;
        public bool IsBanned;
    }

    public class ClassFilteringMiner
    {
        private static Dictionary<string, ClassFilteringObjectMiner> DictionaryFilteringObjectMiner = new Dictionary<string, ClassFilteringObjectMiner>();

        private static Thread ThreadFilteringMiner;
        private static Thread ThreadCleanFilteringMiner;

        private const int CheckFilteringMinerInterval = 1000; // Check every 1000 ms.

        /// <summary>
        /// Enable filtering miner system.
        /// </summary>
        public static void EnableFilteringMiner()
        {
            if (MiningPoolSetting.MiningPoolFilteringMinimumInvalidPacket > 0)
            {
                if (MiningPoolSetting.MiningPoolFileringBanTime > 0)
                {
                    ThreadFilteringMiner = new Thread(delegate ()
                    {
                        while (true)
                        {
                            try
                            {
                                if (DictionaryFilteringObjectMiner.Count > 0)
                                {
                                    foreach (var filteringObject in DictionaryFilteringObjectMiner)
                                    {
                                        if (filteringObject.Value.IsBanned)
                                        {
                                            if (filteringObject.Value.DateOfBan < DateTimeOffset.Now.ToUnixTimeSeconds())
                                            {
                                                filteringObject.Value.IsBanned = false;
                                                filteringObject.Value.TotalInvalidPacket = 0;
                                                ClassLog.ConsoleWriteLog("Unbanned IP: " + filteringObject.Key + ".", 3);
                                                if (MiningPoolSetting.MiningPoolEnableLinkFirewallFiltering)
                                                {
                                                    RemoveFirewallRules(filteringObject.Key);
                                                }
                                            }
                                        }
                                        else
                                        {
                                            if (filteringObject.Value.DateOfBan >= DateTimeOffset.Now.ToUnixTimeSeconds())
                                            {
                                                filteringObject.Value.IsBanned = true;
                                                filteringObject.Value.DateOfBan = DateTimeOffset.Now.ToUnixTimeSeconds() + MiningPoolSetting.MiningPoolFileringBanTime;
                                                ClassLog.ConsoleWriteLog("Banned IP: " + filteringObject.Key + ".", 3);
                                                if(MiningPoolSetting.MiningPoolEnableLinkFirewallFiltering)
                                                {
                                                    InsertFirewallRules(filteringObject.Key);
                                                }
                                            }
                                            else if (filteringObject.Value.TotalInvalidPacket >= MiningPoolSetting.MiningPoolFilteringMinimumInvalidPacket)
                                            {
                                                filteringObject.Value.IsBanned = true;
                                                filteringObject.Value.DateOfBan = DateTimeOffset.Now.ToUnixTimeSeconds() + MiningPoolSetting.MiningPoolFileringBanTime;
                                                ClassLog.ConsoleWriteLog("Banned IP: " + filteringObject.Key + ".", 3);
                                                if (MiningPoolSetting.MiningPoolEnableLinkFirewallFiltering)
                                                {
                                                    InsertFirewallRules(filteringObject.Key);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception error)
                            {
                                ClassLog.ConsoleWriteLog("Filtering system exception error: " + error.Message, 2);
                            }
                            Thread.Sleep(CheckFilteringMinerInterval);
                        }
                    });
                    ThreadFilteringMiner.Start();
                }
                else
                {
                    // Warning ban time should be higher than 0 seconds, the system will not be enabled.
                    ClassLog.ConsoleWriteLog("Warning ban time should be higher than 0 seconds, the system will not be enabled.", 2, 2, true);
                }
            }
            else
            {
                // Warning the limit of invalid share should be higher than 0, the system will not be enabled.
                ClassLog.ConsoleWriteLog("Warning the limit of invalid share should be higher than 0, the system will not be enabled.", 2, 2, true);
            }

            if (MiningPoolSetting.MiningPoolFilteringIntervalCleanInvalidPacket > 0)
            {
                ThreadCleanFilteringMiner = new Thread(delegate ()
                {
                    while (true)
                    {
                        try
                        {
                            if (DictionaryFilteringObjectMiner.Count > 0)
                            {
                                foreach (var filteringObject in DictionaryFilteringObjectMiner)
                                {
                                    if (!filteringObject.Value.IsBanned)
                                    {
                                        filteringObject.Value.TotalInvalidPacket = 0;
                                        ClassLog.ConsoleWriteLog("Cleaning total invalid packet for IP: " + filteringObject.Key + ".", 3);

                                    }
                                }
                            }
                        }
                        catch (Exception error)
                        {
                            ClassLog.ConsoleWriteLog("Filtering clean system exception error: " + error.Message, 2);
                        }
                        Thread.Sleep(MiningPoolSetting.MiningPoolFilteringIntervalCleanInvalidPacket * 1000);
                    }
                });
                ThreadCleanFilteringMiner.Start();
            }
            else
            {
                // Warning the interval of time necessary for clean up total invalid packet should be higher than 0, the cleaning system will not be enabled.
                ClassLog.ConsoleWriteLog("Warning the interval of time necessary for clean up total invalid packet should be higher than 0, the cleaning system will not be enabled.", 2, 2, true);
            }
        }

        /// <summary>
        /// Stop filtering miner system.
        /// </summary>
        public static void StopFileringMiner()
        {
            if (ThreadFilteringMiner != null && (ThreadFilteringMiner.IsAlive || ThreadFilteringMiner != null))
            {
                ThreadFilteringMiner.Abort();
                GC.SuppressFinalize(ThreadFilteringMiner);
            }
            if (ThreadCleanFilteringMiner != null && (ThreadCleanFilteringMiner.IsAlive || ThreadCleanFilteringMiner != null))
            {
                ThreadCleanFilteringMiner.Abort();
                GC.SuppressFinalize(ThreadCleanFilteringMiner);
            }
        }

        /// <summary>
        /// Check if the miner is banned by is own ip.
        /// </summary>
        /// <param name="ip"></param>
        /// <returns></returns>
        public static bool CheckMinerIsBannedByIP(string ip)
        {
            if (DictionaryFilteringObjectMiner.ContainsKey(ip))
            {
                if (DictionaryFilteringObjectMiner[ip].IsBanned)
                {
                    return true;
                }
            }
            else // Not exist insert it.
            {
                try
                {
                    DictionaryFilteringObjectMiner.Add(ip, new ClassFilteringObjectMiner());
                }
                catch
                {

                }
            }
            return false;
        }

        /// <summary>
        /// Increment total amount of invalid packet from an IP.
        /// </summary>
        /// <param name="ip"></param>
        public static void InsertInvalidPacket(string ip)
        {
            if (DictionaryFilteringObjectMiner.ContainsKey(ip))
            {
                DictionaryFilteringObjectMiner[ip].TotalInvalidPacket++;
            }
            else
            {
                try
                {
                    DictionaryFilteringObjectMiner.Add(ip, new ClassFilteringObjectMiner() { TotalInvalidPacket = 1 });
                }
                catch
                {

                }
            }
        }

        /// <summary>
        /// Insert a rules of banning to the firewall.
        /// </summary>
        /// <param name="ip"></param>
        public static void InsertFirewallRules(string ip)
        {
            switch (MiningPoolSetting.MiningPoolLinkFirewallFilteringName.ToLower())
            {
                case ClassFilteringEnumerationFirewallName.IptableFirewall:
                    Process.Start("/bin/bash", "-c \"iptables -A INPUT -p tcp -s " + ip + " -j " + MiningPoolSetting.MiningPoolLinkFirewallFilteringTableName + "\""); // Add iptables rules.
                    break;
                case ClassFilteringEnumerationFirewallName.PacketFilterFirewall:
                    Process.Start("pfctl", "-t " + MiningPoolSetting.MiningPoolLinkFirewallFilteringTableName + " -T add " + ip + ""); // Add iptables rules.
                    break;
                default:
                    ClassLog.ConsoleWriteLog("Warning " + MiningPoolSetting.MiningPoolLinkFirewallFilteringName + " not exist.", 2, 2, true);
                    break;
            }
        }

        /// <summary>
        /// Remove a rules of banning to the firewall.
        /// </summary>
        /// <param name="ip"></param>
        public static void RemoveFirewallRules(string ip)
        {
            switch (MiningPoolSetting.MiningPoolLinkFirewallFilteringName.ToLower())
            {
                case ClassFilteringEnumerationFirewallName.IptableFirewall:
                    Process.Start("/bin/bash", "-c \"iptables -D INPUT -p tcp -s " + ip + " -j " + MiningPoolSetting.MiningPoolLinkFirewallFilteringTableName + "\""); // Add iptables rules.
                    break;
                case ClassFilteringEnumerationFirewallName.PacketFilterFirewall:
                    Process.Start("pfctl", "-t " + MiningPoolSetting.MiningPoolLinkFirewallFilteringTableName + " -T del " + ip + ""); // Add iptables rules.
                    break;
                default:
                    ClassLog.ConsoleWriteLog("Warning " + MiningPoolSetting.MiningPoolLinkFirewallFilteringName + " not exist.", 2, 2, true);
                    break;
            }
        }

    }
}
