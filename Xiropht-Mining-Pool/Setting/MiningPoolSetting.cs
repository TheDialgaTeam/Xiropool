using System.Collections.Generic;
using Xiropht_Connector_All.Setting;

namespace Xiropht_Mining_Pool.Setting
{
    public class MiningPoolSetting
    {
        #region General Settings

        /// <summary>
        /// Mining pool wallet address
        /// </summary>
        public static string MiningPoolWalletAddress;

        /// <summary>
        /// Mining pool list of mining port(s).
        /// </summary>
        public static Dictionary<int, float> MiningPoolMiningPort = new Dictionary<int, float>();

        /// <summary>


        /// <summary>
        /// Mining pool api port.
        /// </summary>
        public static int MiningPoolApiPort = 4040;

        /// <summary>
        /// List of ip authorized ip to access on the api administration, if you set any ip, everybody can submit a password for try to access on the administration.
        /// </summary>
        public static List<string> MiningPoolWhilistApiAdmin = new List<string>();

        /// <summary>
        /// Api password for access to the administration.
        /// </summary>
        public static string MiningPoolApiAdminPassword;

        /// <summary>
        /// The maximum of time for keep alive a session to the administration.
        /// </summary>
        public static int MiningPoolApiAdminMaxKeepAliveSession;

        #endregion

        #region Mining Settings

        /// <summary>
        /// Enable check of miner stats. (Recommended)
        /// </summary>
        public static bool MiningPoolEnableCheckMinerStats;

        /// <summary>
        /// Enable trusting system. (Not recommended)
        /// </summary>
        public static bool MiningPoolEnableTrustedShare;

        /// <summary>
        /// Amount of good share who are required to be trusted.
        /// </summary>
        public static int MiningPoolMinimumTrustedGoodShare;

        /// <summary>
        /// Amount of time interval required for trust a miner, for example if you set 30 seconds, the miner have to reach the minimum of Good Share in less than 30 seconds for be trusted.
        /// </summary>
        public static int MiningPoolMinimumIntervalTrustedShare;

        /// <summary>
        /// Interval of time for trusted state of a miner, for proceed to another check, for example if you set 5 seconds, the mining pool will recheck share of a trusted miner every 5 seconds.
        /// </summary>
        public static int MiningPoolIntervalTrustedShare;

        /// <summary>
        /// Amount of time inserted to ban a miner.
        /// </summary>
        public static int MiningPoolMinerBanTime;

        /// <summary>
        /// Minimum of invalid share to reach for ban a miner.
        /// </summary>
        public static int MiningPoolMinimumInvalidShare;

        /// <summary>
        /// Interval of time for clean up total invalid share done by a miner.
        /// </summary>
        public static int MiningPoolIntervalCleanInvalidShare;

        /// <summary>
        /// Interval of time for change mining job if the miner make too much time for found a share. For example if you set 15 seconds and the miner don't found any share, the pool will send another job.
        /// </summary>
        public static int MiningPoolIntervalChangeJob;

        /// <summary>
        /// Maximum of time of waiting a response from a miner. If the time is reach the miner is disconnected.
        /// </summary>
        public static int MiningPoolTimeout;

        #endregion

        #region Payout Settings

        /// <summary>
        /// Enable payout system.
        /// </summary>
        public static bool MiningPoolEnablePayment;

        /// <summary>
        /// Use the encryption key to communicate with the RPC Wallet
        /// </summary>
        public static bool MiningPoolRpcWalletUseEncryptionKey;

        /// <summary>
        /// The encryption key of the rpc wallet.
        /// </summary>
        public static string MiningPoolRpcWalletEncryptionKey;

        /// <summary>
        /// The rpc wallet host.
        /// </summary>
        public static string MiningPoolRpcWalletHost;

        /// <summary>
        /// The rpc wallet port.
        /// </summary>
        public static int MiningPoolRpcWalletPort;

        /// <summary>
        /// Minimum of balance to reach by a miner for get a payment.
        /// </summary>
        public static decimal MiningPoolMinimumBalancePayment;

        /// <summary>
        /// Transaction fee spend for make a payment to a miner.
        /// </summary>
        public static decimal MiningPoolFeeTransactionPayment;

        /// <summary>
        /// Pourcentage of fee give to the pool owner.
        /// </summary>
        public static decimal MiningPoolFee;

        /// <summary>
        /// Interval of time for proceed payment(s), if you set for example 10 seconds, the mining pool will check every miners balance every 10 seconds for proceed transactions.
        /// </summary>
        public static int MiningPoolIntervalPayment;


        #endregion

        #region Remote Node Settings

        /// <summary>
        /// Remote Node HOST/IP
        /// </summary>
        public static string MiningPoolRemoteNodeHost = "127.0.0.1";

        /// <summary>
        /// Remote node API HTTP Port, do not use Sync Port.
        /// </summary>
        public static int MiningPoolRemoteNodePort = ClassConnectorSetting.RemoteNodeHttpPort;

        #endregion

        #region Filtering Settings

        /// <summary>
        /// Enable filtering system by IP. (Recommended)
        /// </summary>
        public static bool MiningPoolEnableFiltering;

        /// <summary>
        /// Minimum invalid packet to reach for ban the ip.
        /// </summary>
        public static int MiningPoolFilteringMinimumInvalidPacket;

        /// <summary>
        /// Interval of time to clean invalid packets, if you select 10 seconds, you will clean every 10 seconds invalid packets.
        /// </summary>
        public static int MiningPoolFilteringIntervalCleanInvalidPacket;

        /// <summary>
        /// Bantime
        /// </summary>
        public static int MiningPoolFileringBanTime;

        /// <summary>
        /// Enable filtering system by IP to a firewall system (Support iptables(Linux) and PF(BSD)).
        /// </summary>
        public static bool MiningPoolEnableLinkFirewallFiltering;

        /// <summary>
        /// The name of the firewall system (iptables (Linux) or PF (BSD))
        /// </summary>
        public static string MiningPoolLinkFirewallFilteringName;

        /// <summary>
        /// The name of a chain (iptables) or a table (PF).
        /// </summary>
        public static string MiningPoolLinkFirewallFilteringTableName;

        #endregion

        #region Log Settings

        /// <summary>
        /// Interval of time to write logs.
        /// </summary>
        public static int MiningPoolWriteLogInterval = 10 * 1000;

        /// <summary>
        /// The minimum amount of log lines required to write logs.
        /// </summary>
        public static int MiningPoolWriteLogMinimumLogLine = 1000;

        #endregion
    }
}
