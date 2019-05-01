using System.Collections.Generic;


namespace Xiropht_Mining_Pool.Mining
{

    public class ClassMiningPoolGlobalStats
    {
        public static int TotalWorkerConnected;
        public static int TotalMinerConnected;
        public static float TotalMinerHashrate;
        public static int TotalBlockFound;
        public static decimal PoolCurrentBalance;
        public static decimal PoolPendingBalance;
        public static decimal PoolTotalPaid;
        public static Dictionary<int, string> ListBlockFound = new Dictionary<int, string>();

        /// <summary>
        /// Current block informations.
        /// </summary>
        public static string CurrentBlockTemplate;
 		public static string CurrentBlockId;
        public static string CurrentBlockHash;
        public static string CurrentBlockAlgorithm;
        public static string CurrentBlockSize;
        public static string CurrentBlockMethod;
        public static string CurrentBlockKey;
        public static string CurrentBlockJob;
        public static string CurrentBlockReward;
        public static string CurrentBlockDifficulty;
        public static string CurrentBlockTimestampCreate;
        public static string CurrentBlockIndication;
        public static float CurrentBlockJobMinRange;
        public static float CurrentBlockJobMaxRange;


        /// <summary>
        /// Current mining method information.
        /// </summary>
        public static int CurrentRoundAesRound;
        public static int CurrentRoundAesSize;
        public static string CurrentRoundAesKey;
        public static int CurrentRoundXorKey;

    }
}
