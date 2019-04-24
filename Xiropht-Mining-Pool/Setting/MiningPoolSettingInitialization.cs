using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Xiropht_Connector_All.Setting;
using Xiropht_Mining_Pool.Utility;

namespace Xiropht_Mining_Pool.Setting
{
    public class MiningPoolSettingEnumeration
    {
        public const string SettingMiningPoolWalletAddress = "MINING_POOL_WALLET_ADDRESS";
        public const string SettingMiningPoolPort = "MINING_POOL_PORT";
        public const string SettingMiningPoolApiPort = "MINING_POOL_API_PORT";
        public const string SettingMiningPoolWhitelistIpApiAdmin = "MINING_POOL_WHITELIST_IP_API_ADMIN";
        public const string SettingMiningPoolApiAdministrationPassword = "MINING_POOL_API_ADMINISTRATION_PASSWORD";
        public const string SettingMiningPoolApiMaxKeepAliveSession = "MINING_POOL_API_MAX_KEEP_ALIVE_SESSION";
        public const string SettingMiningPoolEnableCheckMinerStats = "MINING_POOL_ENABLE_CHECK_MINER_STATS";
        public const string SettingMiningPoolEnableTrustedShare = "MINING_POOL_ENABLE_TRUSTED_SHARE";
        public const string SettingMiningPoolMinimumTrustedGoodShare = "MINING_POOL_MINIMUM_TRUSTED_GOOD_SHARE";
        public const string SettingMiningPoolMinimumIntervalTrustedShare = "MINING_POOL_MINIMUM_INTERVAL_TRUSTED_SHARE";
        public const string SettingMiningPoolIntervalTrustedShare = "MINING_POOL_INTERVAL_TRUSTED_SHARE";
        public const string SettingMiningPoolIntervalChangeJob = "MINING_POOL_INTERVAL_CHANGE_JOB";
        public const string SettingMiningPoolTimeout = "MINING_POOL_TIMEOUT";
        public const string SettingMiningPoolBanTime = "MINING_POOL_MINER_BAN_TIME";
        public const string SettingMiningPoolLimitInvalidShare = "MINING_POOL_LIMIT_INVALID_SHARE";
        public const string SettingMiningPoolIntervalCleanInvalidShare = "MINING_POOL_INTERVAL_CLEAN_INVALID_SHARE";
        public const string SettingMiningPoolEnablePayment = "MINING_POOL_ENABLE_PAYMENT";
        public const string SettingMiningPoolRpcWalletEnableEncryption = "MINING_POOL_RPC_WALLET_ENABLE_ENCRYPTION";
        public const string SettingMiningPoolRpcWalletEncryptionKey = "MINING_POOL_RPC_WALLET_ENCRYPTION_KEY";
        public const string SettingMiningPoolRpcWalletHost = "MINING_POOL_RPC_WALLET_HOST";
        public const string SettingMiningPoolRpcWalletPort = "MINING_POOL_RPC_WALLET_PORT";
        public const string SettingMiningPoolMinimumBalancePayment = "MINING_POOL_MINIMUM_BALANCE_PAYMENT";
        public const string SettingMiningPoolTransactionFeePayment = "MINING_POOL_TRANSACTION_FEE_PAYMENT";
        public const string SettingMiningPoolPoolFee = "MINING_POOL_FEE";
        public const string SettingMiningPoolIntervalPayment = "MINING_POOL_INTERVAL_PAYMENT";
        public const string SettingMiningPoolRemoteNodeHost = "MINING_POOL_REMOTE_NODE_HOST";
        public const string SettingMiningPoolRemoteNodePort = "MINING_POOL_REMOTE_NODE_PORT";
        public const string SettingMiningPoolEnableFiltering = "MINING_POOL_ENABLE_FILTERING";
        public const string SettingMiningPoolFilteringLimitInvalidPacket = "MINING_POOL_FILTERING_LIMIT_INVALID_PACKET";
        public const string SettingMiningPoolFilteringIntervalCleanInvalidPacket = "MINING_POOL_FILTERING_INTERVAL_CLEAN_INVALID_PACKET";
        public const string SettingMiningPoolFilteringBanTme = "MINING_POOL_FILTERING_BANTIME";
        public const string SettingMiningPoolEnableLinkFirewallFiltering = "MINING_POOL_ENABLE_LINK_FIREWALL_FILTERING";
        public const string SettingMiningPoolLinkFirewallFilteringName = "MINING_POOL_LINK_FIREWALL_FILTERING_NAME";
        public const string SettingMiningPoolLinkFirewallFilteringTableName = "MINING_POOL_LINK_FIREWALL_FILTERING_TABLE_NAME";
        public const string SettingMiningPoolWriteLogInterval = "MINING_POOL_WRITE_LOG_INTERVAL";
        public const string SettingMiningPoolWriteLogMinimumLogLine = "MINING_POOL_WRITE_LOG_MINIMUM_LOG_LINE";
    }

    public class MiningPoolSettingInitialization
    {
        private const string MiningPoolSettingFile = "\\config_pool.ini";

        /// <summary>
        /// Read or create mining pool setting file for initialize the pool.
        /// </summary>
        /// <returns></returns>
        public static bool InitializationPoolSettingFile()
        {
            if (File.Exists(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory+ MiningPoolSettingFile)))
            {
                using (var streamReaderConfigPool = new StreamReader(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory+ MiningPoolSettingFile)))
                {
                    int numberOfLines = 0;
                    string line = string.Empty;
                    while((line = streamReaderConfigPool.ReadLine()) != null)
                    {
                        numberOfLines++;
                        if (!string.IsNullOrEmpty(line))
                        {
                            if (!line.StartsWith("/"))
                            {
                                if (line.Contains("="))
                                {
                                    var splitLine = line.Split(new[] { "=" }, StringSplitOptions.None);
                                    if (splitLine.Length > 1)
                                    {
                                        try
                                        {
#if DEBUG
                                            Debug.WriteLine("Config line read: " + splitLine[0] + " argument read: " + splitLine[1]);
#endif
                                            switch (splitLine[0])
                                            {
                                                case MiningPoolSettingEnumeration.SettingMiningPoolWalletAddress:
                                                    MiningPoolSetting.MiningPoolWalletAddress = splitLine[1];
                                                    break;
                                                case MiningPoolSettingEnumeration.SettingMiningPoolPort:
                                                    if (splitLine[1].Contains(";")) // Multiple ports.
                                                    {
                                                        var splitLinePort = splitLine[1].Split(new[] { ";" }, StringSplitOptions.None);
                                                        foreach (var linePort in splitLinePort)
                                                        {
                                                            var splitPort = linePort.Split(new[] { "|" }, StringSplitOptions.None);
                                                            MiningPoolSetting.MiningPoolMiningPort.Add(int.Parse(splitPort[0]), float.Parse(splitPort[1]));
                                                        }
                                                    }
                                                    else
                                                    {
                                                        var splitPort = splitLine[1].Split(new[] { "|" }, StringSplitOptions.None);
                                                        MiningPoolSetting.MiningPoolMiningPort.Add(int.Parse(splitPort[0]), float.Parse(splitPort[1]));
                                                    }
                                                    break;
                                                case MiningPoolSettingEnumeration.SettingMiningPoolApiPort:
                                                    MiningPoolSetting.MiningPoolApiPort = int.Parse(splitLine[1]);
                                                    break;
                                                case MiningPoolSettingEnumeration.SettingMiningPoolWhitelistIpApiAdmin:
                                                    if (splitLine[1].Contains(";")) // Multiple ip.
                                                    {
                                                        var splitLineIp = splitLine[1].Split(new[] { ";" }, StringSplitOptions.None);
                                                        foreach (var lineIp in splitLineIp)
                                                        {
                                                            MiningPoolSetting.MiningPoolWhilistApiAdmin.Add(lineIp);
                                                        }
                                                    }
                                                    else
                                                    {
                                                        MiningPoolSetting.MiningPoolWhilistApiAdmin.Add(splitLine[1]);
                                                    }
                                                    break;
                                                case MiningPoolSettingEnumeration.SettingMiningPoolApiAdministrationPassword:
                                                    MiningPoolSetting.MiningPoolApiAdminPassword = splitLine[1];
                                                    break;
                                                case MiningPoolSettingEnumeration.SettingMiningPoolApiMaxKeepAliveSession:
                                                    MiningPoolSetting.MiningPoolApiAdminMaxKeepAliveSession = int.Parse(splitLine[1]);
                                                    break;
                                                case MiningPoolSettingEnumeration.SettingMiningPoolEnableCheckMinerStats:
                                                    if (splitLine[1].ToLower() == "y")
                                                    {
                                                        MiningPoolSetting.MiningPoolEnableCheckMinerStats = true;
                                                    }
                                                    break;
                                                case MiningPoolSettingEnumeration.SettingMiningPoolEnableTrustedShare:
                                                    if (splitLine[1].ToLower() == "y")
                                                    {
                                                        MiningPoolSetting.MiningPoolEnableTrustedShare = true;
                                                    }
                                                    break;
                                                case MiningPoolSettingEnumeration.SettingMiningPoolMinimumTrustedGoodShare:
                                                    MiningPoolSetting.MiningPoolMinimumTrustedGoodShare = int.Parse(splitLine[1]);
                                                    break;
                                                case MiningPoolSettingEnumeration.SettingMiningPoolMinimumIntervalTrustedShare:
                                                    MiningPoolSetting.MiningPoolMinimumIntervalTrustedShare = int.Parse(splitLine[1]);
                                                    break;
                                                case MiningPoolSettingEnumeration.SettingMiningPoolIntervalTrustedShare:
                                                    MiningPoolSetting.MiningPoolIntervalTrustedShare = int.Parse(splitLine[1]);
                                                    break;
                                                case MiningPoolSettingEnumeration.SettingMiningPoolIntervalChangeJob:
                                                    MiningPoolSetting.MiningPoolIntervalChangeJob = int.Parse(splitLine[1]);
                                                    break;
                                                case MiningPoolSettingEnumeration.SettingMiningPoolTimeout:
                                                    MiningPoolSetting.MiningPoolTimeout = int.Parse(splitLine[1]);
                                                    break;
                                                case MiningPoolSettingEnumeration.SettingMiningPoolBanTime:
                                                    MiningPoolSetting.MiningPoolMinerBanTime = int.Parse(splitLine[1]);
                                                    break;
                                                case MiningPoolSettingEnumeration.SettingMiningPoolLimitInvalidShare:
                                                    MiningPoolSetting.MiningPoolMinimumInvalidShare = int.Parse(splitLine[1]);
                                                    break;
                                                case MiningPoolSettingEnumeration.SettingMiningPoolIntervalCleanInvalidShare:
                                                    MiningPoolSetting.MiningPoolIntervalCleanInvalidShare = int.Parse(splitLine[1]);
                                                    break;
                                                case MiningPoolSettingEnumeration.SettingMiningPoolEnablePayment:
                                                    if (splitLine[1].ToLower() == "y")
                                                    {
                                                        MiningPoolSetting.MiningPoolEnablePayment = true;
                                                    }
                                                    break;
                                                case MiningPoolSettingEnumeration.SettingMiningPoolRpcWalletEnableEncryption:
                                                    if (splitLine[1].ToLower() == "y" || splitLine[1].ToLower() == "true")
                                                    {
                                                        MiningPoolSetting.MiningPoolRpcWalletUseEncryptionKey = true;
                                                    }
                                                    break;
                                                case MiningPoolSettingEnumeration.SettingMiningPoolRpcWalletEncryptionKey:
                                                    MiningPoolSetting.MiningPoolRpcWalletEncryptionKey = splitLine[1];
                                                    break;
                                                case MiningPoolSettingEnumeration.SettingMiningPoolRpcWalletHost:
                                                    MiningPoolSetting.MiningPoolRpcWalletHost = splitLine[1];
                                                    break;
                                                case MiningPoolSettingEnumeration.SettingMiningPoolRpcWalletPort:
                                                    MiningPoolSetting.MiningPoolRpcWalletPort = int.Parse(splitLine[1]);
                                                    break;
                                                case MiningPoolSettingEnumeration.SettingMiningPoolMinimumBalancePayment:
                                                    MiningPoolSetting.MiningPoolMinimumBalancePayment = decimal.Parse(splitLine[1].Replace(".", ","), NumberStyles.Currency, Program.GlobalCultureInfo);
                                                    break;
                                                case MiningPoolSettingEnumeration.SettingMiningPoolTransactionFeePayment:
                                                    MiningPoolSetting.MiningPoolFeeTransactionPayment = decimal.Parse(splitLine[1].Replace(".", ","), NumberStyles.Currency, Program.GlobalCultureInfo);
                                                    break;
                                                case MiningPoolSettingEnumeration.SettingMiningPoolPoolFee:
                                                    MiningPoolSetting.MiningPoolFee = decimal.Parse(splitLine[1].Replace(".", ","), NumberStyles.Currency, Program.GlobalCultureInfo);
                                                    break;
                                                case MiningPoolSettingEnumeration.SettingMiningPoolIntervalPayment:
                                                    MiningPoolSetting.MiningPoolIntervalPayment = int.Parse(splitLine[1]);
                                                    break;
                                                case MiningPoolSettingEnumeration.SettingMiningPoolEnableFiltering:
                                                    if (splitLine[1].ToLower() == "y")
                                                    {
                                                        MiningPoolSetting.MiningPoolEnableFiltering = true;
                                                    }
                                                    break;
                                                case MiningPoolSettingEnumeration.SettingMiningPoolFilteringLimitInvalidPacket:
                                                    MiningPoolSetting.MiningPoolFilteringMinimumInvalidPacket = int.Parse(splitLine[1]);
                                                    break;
                                                case MiningPoolSettingEnumeration.SettingMiningPoolFilteringIntervalCleanInvalidPacket:
                                                    MiningPoolSetting.MiningPoolFilteringIntervalCleanInvalidPacket = int.Parse(splitLine[1]);
                                                    break;
                                                case MiningPoolSettingEnumeration.SettingMiningPoolFilteringBanTme:
                                                    MiningPoolSetting.MiningPoolFileringBanTime = int.Parse(splitLine[1]);
                                                    break;
                                                case MiningPoolSettingEnumeration.SettingMiningPoolEnableLinkFirewallFiltering:
                                                    if (splitLine[1].ToLower() == "y")
                                                    {
                                                        MiningPoolSetting.MiningPoolEnableLinkFirewallFiltering = true;
                                                    }
                                                    break;
                                                case MiningPoolSettingEnumeration.SettingMiningPoolLinkFirewallFilteringName:
                                                    MiningPoolSetting.MiningPoolLinkFirewallFilteringName = splitLine[1];
                                                    break;
                                                case MiningPoolSettingEnumeration.SettingMiningPoolLinkFirewallFilteringTableName:
                                                    MiningPoolSetting.MiningPoolLinkFirewallFilteringTableName = splitLine[1];
                                                    break;
                                                case MiningPoolSettingEnumeration.SettingMiningPoolWriteLogInterval:
                                                    MiningPoolSetting.MiningPoolWriteLogInterval = int.Parse(splitLine[1]);
                                                    break;
                                                case MiningPoolSettingEnumeration.SettingMiningPoolWriteLogMinimumLogLine:
                                                    MiningPoolSetting.MiningPoolWriteLogMinimumLogLine = int.Parse(splitLine[1]);
                                                    break;
                                                case MiningPoolSettingEnumeration.SettingMiningPoolRemoteNodeHost:
                                                    MiningPoolSetting.MiningPoolRemoteNodeHost = splitLine[1];
                                                    break;
                                                case MiningPoolSettingEnumeration.SettingMiningPoolRemoteNodePort:
                                                    MiningPoolSetting.MiningPoolRemoteNodePort = int.Parse(splitLine[1]);
                                                    break;
                                                default:
                                                    Console.WriteLine("Unknown config line: " + splitLine[0] + " with argument: " + splitLine[1] + " on line: " + numberOfLines);
                                                    break;
                                            }
                                        }
                                        catch
                                        {
                                            Console.WriteLine("Error on line:" + numberOfLines);
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("Error on config line: " + splitLine[0] + " on line:" + numberOfLines);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                CreatePoolSettingFile();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Create the setting file.
        /// </summary>
        /// <returns></returns>
        private static void CreatePoolSettingFile()
        {
            Console.WriteLine("No config file found.");
            File.Create(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory+ MiningPoolSettingFile)).Close();
            Console.WriteLine("Write the wallet address of the pool: ");
            string input = Console.ReadLine();

            using (var streamWriterConfigPool = new StreamWriter(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory+ MiningPoolSettingFile)) { AutoFlush = true })
            {
                // Pool General Settings

                streamWriterConfigPool.WriteLine("## Pool General Settings ##");
                streamWriterConfigPool.WriteLine("");

                streamWriterConfigPool.WriteLine("// Mining pool wallet address.");
                streamWriterConfigPool.WriteLine(MiningPoolSettingEnumeration.SettingMiningPoolWalletAddress + "=" + input);
                streamWriterConfigPool.WriteLine("");

                streamWriterConfigPool.WriteLine("// List of mining pool ports with their start difficulty: port|difficulty.");
                streamWriterConfigPool.WriteLine(MiningPoolSettingEnumeration.SettingMiningPoolPort + "=1111|100;2222|1063;3333|2000;4444|8000");
                streamWriterConfigPool.WriteLine("");
                streamWriterConfigPool.WriteLine("");

                // Remote Node Settings

                streamWriterConfigPool.WriteLine("## Remote Node Settings ##");
                streamWriterConfigPool.WriteLine("");

                streamWriterConfigPool.WriteLine("// Host or IP of the remote node target, used for sync blocks, transactions.");
                streamWriterConfigPool.WriteLine(MiningPoolSettingEnumeration.SettingMiningPoolRemoteNodeHost + "=127.0.0.1");
                streamWriterConfigPool.WriteLine("");

                streamWriterConfigPool.WriteLine("// Port of the remote node API.");
                streamWriterConfigPool.WriteLine(MiningPoolSettingEnumeration.SettingMiningPoolRemoteNodePort + "=" + ClassConnectorSetting.RemoteNodeHttpPort);
                streamWriterConfigPool.WriteLine("");
                streamWriterConfigPool.WriteLine("");


                // API Settings
                streamWriterConfigPool.WriteLine("## API Settings ##");
                streamWriterConfigPool.WriteLine("");

                streamWriterConfigPool.WriteLine("// Mining pool api port.");
                streamWriterConfigPool.WriteLine(MiningPoolSettingEnumeration.SettingMiningPoolApiPort + "=4040");
                streamWriterConfigPool.WriteLine("");

                streamWriterConfigPool.WriteLine("// List of ip authorized ip to access on the api administration, if you set any ip, everybody can submit a password for try to access on the administration.");
                streamWriterConfigPool.WriteLine(MiningPoolSettingEnumeration.SettingMiningPoolWhitelistIpApiAdmin + "=127.0.0.1");
                streamWriterConfigPool.WriteLine("");

                streamWriterConfigPool.WriteLine("// Api password for access to the administration.");
                streamWriterConfigPool.WriteLine(MiningPoolSettingEnumeration.SettingMiningPoolApiAdministrationPassword + "=Xiropht");
                streamWriterConfigPool.WriteLine("");

                streamWriterConfigPool.WriteLine("// The maximum of time to keep alive a session on the API administration.");
                streamWriterConfigPool.WriteLine(MiningPoolSettingEnumeration.SettingMiningPoolApiMaxKeepAliveSession + "=600");
                streamWriterConfigPool.WriteLine("");
                streamWriterConfigPool.WriteLine("");


                // Mining Settings
                streamWriterConfigPool.WriteLine("## Mining Settings ##");
                streamWriterConfigPool.WriteLine("");

                streamWriterConfigPool.WriteLine("// Enable check miner stats system (Recommended).");
                streamWriterConfigPool.WriteLine(MiningPoolSettingEnumeration.SettingMiningPoolEnableCheckMinerStats + "=Y");
                streamWriterConfigPool.WriteLine("");

                streamWriterConfigPool.WriteLine("// Enable trusting system.");
                streamWriterConfigPool.WriteLine(MiningPoolSettingEnumeration.SettingMiningPoolEnableTrustedShare + "=Y");
                streamWriterConfigPool.WriteLine("");

                streamWriterConfigPool.WriteLine("// Amount of good share required to trust a miner.");
                streamWriterConfigPool.WriteLine(MiningPoolSettingEnumeration.SettingMiningPoolMinimumTrustedGoodShare + "=10");
                streamWriterConfigPool.WriteLine("");

                streamWriterConfigPool.WriteLine("// Amount of time interval required for trust a miner, for example if you set 30 seconds, the miner have to reach the minimum of Good Share in less than 30 seconds for be trusted.");
                streamWriterConfigPool.WriteLine(MiningPoolSettingEnumeration.SettingMiningPoolMinimumIntervalTrustedShare + "=30");
                streamWriterConfigPool.WriteLine("");

                streamWriterConfigPool.WriteLine("// Interval of time for proceed to another check of the miner, for example if you set 5 seconds, the mining pool will check next share of a trusted miner every 5 seconds.");
                streamWriterConfigPool.WriteLine(MiningPoolSettingEnumeration.SettingMiningPoolIntervalTrustedShare + "=5");
                streamWriterConfigPool.WriteLine("");

                streamWriterConfigPool.WriteLine("// Interval of time for change mining job if the miner make too much time for found a share. For example if you set 15 seconds and the miner don't found any share, the pool will send another job.");
                streamWriterConfigPool.WriteLine(MiningPoolSettingEnumeration.SettingMiningPoolIntervalChangeJob + "=15");
                streamWriterConfigPool.WriteLine("");

                streamWriterConfigPool.WriteLine("// Maximum of time of waiting a response from a miner. If the time is reach the miner is disconnected.");
                streamWriterConfigPool.WriteLine(MiningPoolSettingEnumeration.SettingMiningPoolTimeout + "=60");
                streamWriterConfigPool.WriteLine("");
                streamWriterConfigPool.WriteLine("");

                // Mining Ban Settings
                streamWriterConfigPool.WriteLine("## Mining Ban Settings ##");
                streamWriterConfigPool.WriteLine("");

                streamWriterConfigPool.WriteLine("// Amount of time inserted to ban a miner.");
                streamWriterConfigPool.WriteLine(MiningPoolSettingEnumeration.SettingMiningPoolBanTime + "=60");
                streamWriterConfigPool.WriteLine("");

                streamWriterConfigPool.WriteLine("// Minimum of invalid share to reach for ban a miner.");
                streamWriterConfigPool.WriteLine(MiningPoolSettingEnumeration.SettingMiningPoolLimitInvalidShare + "=20");
                streamWriterConfigPool.WriteLine("");

                streamWriterConfigPool.WriteLine("// Interval of time for clean up total invalid share done by a miner.");
                streamWriterConfigPool.WriteLine(MiningPoolSettingEnumeration.SettingMiningPoolIntervalCleanInvalidShare + "=20");
                streamWriterConfigPool.WriteLine("");
                streamWriterConfigPool.WriteLine("");

                // Payment Settings
                streamWriterConfigPool.WriteLine("## Payment Settings ##");
                streamWriterConfigPool.WriteLine("");

                streamWriterConfigPool.WriteLine("// Enable payment system.");
                streamWriterConfigPool.WriteLine(MiningPoolSettingEnumeration.SettingMiningPoolEnablePayment + "=Y");
                streamWriterConfigPool.WriteLine("");


                streamWriterConfigPool.WriteLine("//  Use the encryption key to communicate with the RPC Wallet.");
                streamWriterConfigPool.WriteLine(MiningPoolSettingEnumeration.SettingMiningPoolRpcWalletEnableEncryption + "=N");
                streamWriterConfigPool.WriteLine("");

                streamWriterConfigPool.WriteLine("// The encryption key of the rpc wallet.");
                streamWriterConfigPool.WriteLine(MiningPoolSettingEnumeration.SettingMiningPoolRpcWalletEncryptionKey + "=");
                streamWriterConfigPool.WriteLine("");

                streamWriterConfigPool.WriteLine("// The rpc wallet host.");
                streamWriterConfigPool.WriteLine(MiningPoolSettingEnumeration.SettingMiningPoolRpcWalletHost + "=127.0.0.1");
                streamWriterConfigPool.WriteLine("");

                streamWriterConfigPool.WriteLine("// The rpc wallet port.");
                streamWriterConfigPool.WriteLine(MiningPoolSettingEnumeration.SettingMiningPoolRpcWalletPort + "=8000");
                streamWriterConfigPool.WriteLine("");

                streamWriterConfigPool.WriteLine("// Minimum of balance to reach by a miner for get a payment.");
                streamWriterConfigPool.WriteLine(MiningPoolSettingEnumeration.SettingMiningPoolMinimumBalancePayment + "=0.1");
                streamWriterConfigPool.WriteLine("");

                streamWriterConfigPool.WriteLine("// Transaction fee spend for make a payment to a miner.");
                streamWriterConfigPool.WriteLine(MiningPoolSettingEnumeration.SettingMiningPoolTransactionFeePayment + "="+ClassConnectorSetting.MinimumWalletTransactionFee);
                streamWriterConfigPool.WriteLine("");

                streamWriterConfigPool.WriteLine("// Pourcentage of fee give to the pool owner.");
                streamWriterConfigPool.WriteLine(MiningPoolSettingEnumeration.SettingMiningPoolPoolFee + "=0.1");
                streamWriterConfigPool.WriteLine("");

                streamWriterConfigPool.WriteLine("// Interval of time for proceed payment(s), if you set for example 10 seconds, the mining pool will check every miners balance every 10 seconds for proceed transactions.");
                streamWriterConfigPool.WriteLine(MiningPoolSettingEnumeration.SettingMiningPoolIntervalPayment + "=10");
                streamWriterConfigPool.WriteLine("");
                streamWriterConfigPool.WriteLine("");

                // Filtering Settings
                streamWriterConfigPool.WriteLine("## Filtering Settings ##");
                streamWriterConfigPool.WriteLine("");

                streamWriterConfigPool.WriteLine("// Enable filtering system by IP (Recommended).");
                streamWriterConfigPool.WriteLine(MiningPoolSettingEnumeration.SettingMiningPoolEnableFiltering + "=Y");
                streamWriterConfigPool.WriteLine("");

                streamWriterConfigPool.WriteLine("// Minimum invalid packet to reach for ban the ip.");
                streamWriterConfigPool.WriteLine(MiningPoolSettingEnumeration.SettingMiningPoolFilteringLimitInvalidPacket + "=100");
                streamWriterConfigPool.WriteLine("");

                streamWriterConfigPool.WriteLine("// Interval of time to clean invalid packets, if you select 60 seconds, you will clean every 10 seconds invalid packets.");
                streamWriterConfigPool.WriteLine(MiningPoolSettingEnumeration.SettingMiningPoolFilteringIntervalCleanInvalidPacket + "=10");
                streamWriterConfigPool.WriteLine("");

                streamWriterConfigPool.WriteLine("// Bantime.");
                streamWriterConfigPool.WriteLine(MiningPoolSettingEnumeration.SettingMiningPoolFilteringBanTme + "=60");
                streamWriterConfigPool.WriteLine("");
                streamWriterConfigPool.WriteLine("");

                // Filtering Advanced Settings.
                streamWriterConfigPool.WriteLine("## Filtering Advanced Settings ##");
                streamWriterConfigPool.WriteLine("");

                streamWriterConfigPool.WriteLine("// Enable filtering system by IP to a firewall system (Support iptables(Linux) and PF(BSD)).");
                streamWriterConfigPool.WriteLine(MiningPoolSettingEnumeration.SettingMiningPoolEnableLinkFirewallFiltering + "=N");
                streamWriterConfigPool.WriteLine("");

                streamWriterConfigPool.WriteLine("// The name of the firewall system (iptables (Linux) or PF (BSD)).");
                streamWriterConfigPool.WriteLine(MiningPoolSettingEnumeration.SettingMiningPoolLinkFirewallFilteringName + "=iptables");
                streamWriterConfigPool.WriteLine("");

                streamWriterConfigPool.WriteLine("// The name of a chain (iptables) or a table (PF).");
                streamWriterConfigPool.WriteLine(MiningPoolSettingEnumeration.SettingMiningPoolLinkFirewallFilteringTableName + "=xirophtban");
                streamWriterConfigPool.WriteLine("");
                streamWriterConfigPool.WriteLine("");

                // Log Settings.
                streamWriterConfigPool.WriteLine("## Log Settings ##");
                streamWriterConfigPool.WriteLine("");

                streamWriterConfigPool.WriteLine("// Interval of time to write logs (in second).");
                streamWriterConfigPool.WriteLine(MiningPoolSettingEnumeration.SettingMiningPoolWriteLogInterval + "=10");
                streamWriterConfigPool.WriteLine("");

                streamWriterConfigPool.WriteLine("// The minimum amount of log lines required to write logs.");
                streamWriterConfigPool.WriteLine(MiningPoolSettingEnumeration.SettingMiningPoolWriteLogMinimumLogLine + "=100");


            }
            Console.WriteLine("The config file: " + ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory+ MiningPoolSettingFile) + " has been writed with few examples inside:");
            Console.WriteLine("The pool program close, do not forget to setup your config file.");
        }
    }
}
