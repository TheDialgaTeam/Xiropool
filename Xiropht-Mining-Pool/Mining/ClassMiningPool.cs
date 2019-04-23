using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xiropht_Connector_All.Setting;
using Xiropht_Connector_All.Utils;
using Xiropht_Mining_Pool.Log;
using Xiropht_Mining_Pool.Miner;
using Xiropht_Mining_Pool.Network;
using Xiropht_Mining_Pool.Setting;
using Xiropht_Mining_Pool.Threading;
using Xiropht_Mining_Pool.Utility;

namespace Xiropht_Mining_Pool.Mining
{
    public class ClassMiningPool
    {
        public int MiningPoolPort;
        public float MiningPoolDifficultyStart;
        private TcpListener TcpListenerMiningPool;
        private Thread ThreadMiningPool;
        public bool MiningPoolStatus;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="miningPoolPort"></param>
        public ClassMiningPool(int miningPoolPort, float miningPoolDifficultyStart)
        {
            MiningPoolPort = miningPoolPort;
            MiningPoolDifficultyStart = miningPoolDifficultyStart;
        }


        /// <summary>
        /// Start mining pool
        /// </summary>
        public void StartMiningPool()
        {
            ClassLog.ConsoleWriteLog("Start mining pool port " + MiningPoolPort + " with difficulty start of: "+MiningPoolDifficultyStart, ClassLogEnumeration.IndexPoolMinerLog, ClassLogConsoleEnumeration.IndexPoolConsoleGreenLog, true);

            MiningPoolStatus = true;
            TcpListenerMiningPool = new TcpListener(IPAddress.Any, MiningPoolPort);
            TcpListenerMiningPool.Start();

            ThreadMiningPool = new Thread(async delegate ()
            {
                while (!Program.Exit && MiningPoolStatus)
                {
                    try
                    {
                        var client = await TcpListenerMiningPool.AcceptTcpClientAsync().ConfigureAwait(false);
                        await HandleMiner(client).ConfigureAwait(false);
                    }
                    catch (Exception error)
                    {
                        ClassLog.ConsoleWriteLog("Error on Listen incoming connection to Mining Pool port: " + MiningPoolPort + ", exception: " + error.Message, ClassLogEnumeration.IndexPoolMinerErrorLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
                    }
                }
            })
            {
                Priority = ThreadPriority.BelowNormal,
                IsBackground = true
            };
            ThreadMiningPool.Start();
        }

        /// <summary>
        /// Stop mining pool.
        /// </summary>
        public void StopMiningPool()
        {
            ClassLog.ConsoleWriteLog("Stop mining pool port " + MiningPoolPort, ClassLogEnumeration.IndexPoolMinerLog, ClassLogConsoleEnumeration.IndexPoolConsoleGreenLog, true);
            MiningPoolStatus = false;
            if (ThreadMiningPool != null && (ThreadMiningPool.IsAlive || ThreadMiningPool != null))
            {
                ThreadMiningPool.Abort();
                GC.SuppressFinalize(ThreadMiningPool);
            }
            try
            {
                TcpListenerMiningPool.Stop();
            }
            catch
            {

            }
        }

        /// <summary>
        /// Handle incoming connection from miner.
        /// </summary>
        /// <param name="tcpMiner"></param>
        private async Task HandleMiner(TcpClient tcpMiner)
        {
            await Task.Factory.StartNew(async () =>
            {
                using (var minerTcpObject = new MinerTcpObject(tcpMiner, MiningPoolPort))
                {
                    await minerTcpObject.HandleIncomingMiner(MiningPoolPort, MiningPoolDifficultyStart);
                }
            }, CancellationToken.None, TaskCreationOptions.LongRunning, PriorityScheduler.Lowest).ConfigureAwait(false);
        }
    }

    public class IncomingPacketConnectionObject : IDisposable
    {
        public byte[] buffer;
        public string packet;
        private bool disposed;

        public IncomingPacketConnectionObject()
        {
            buffer = new byte[ClassConnectorSetting.MaxNetworkPacketSize];
            packet = string.Empty;
        }

        ~IncomingPacketConnectionObject()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                buffer = null;
                packet = null;
            }
            disposed = true;
        }
    }

    public class MinerTcpObject : IDisposable
    {
        /// <summary>
        /// Dispose information.
        /// </summary>
        private bool IsDisposed;

        /// <summary>
        /// Network informations.
        /// </summary>
        public bool IsConnected;
        public bool IsLogged;
        public bool UseCustomDifficulty;
        private long LastPacketReceived;
        public long LastShareReceived;
        public long TotalShareReceived;
        public float CurrentHashrate;
        public float CurrentHashEffort;
        private long TotalPacketReceivedPerSecond;
        private long LastJobDateReceive;
        public long LoginDate;
        private string Ip;
        private TcpClient TcpMiner;
        private int MiningPoolPort;
        private string CurrentBlockHashOnMining;

        /// <summary>
        /// Miner informations.
        /// </summary>
        public string MinerWalletAddress;
        private string MinerVersion;
        public float CurrentMiningJob;
        private float PreviousMiningJob;
        private float MiningDifficultyStart;
        public int TotalGoodShare;
        public long TotalGoodShareDone;

        private Dictionary<string, float> ListOfShare;
        public List<float> ListOfJob;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="tcpMiner"></param>

        public MinerTcpObject(TcpClient tcpMiner, int miningPoolPort)
        {
            TcpMiner = tcpMiner;
            MinerWalletAddress = string.Empty;
            MiningPoolPort = miningPoolPort;
            ListOfShare = new Dictionary<string, float>();
            ListOfJob = new List<float>();
        }

        #region Dispose functions

        ~MinerTcpObject()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (IsDisposed)
                return;

            IsConnected = false;
            IsDisposed = true;
        }

        #endregion

        #region Mining Functions

        /// <summary>
        /// Spead job to the miner.
        /// </summary>
        public async void MiningPoolSendJobAsync(float jobTarget = 0)
        {
            CurrentBlockHashOnMining = ClassMiningPoolGlobalStats.CurrentBlockHash;
            float minRange = ClassMiningPoolGlobalStats.CurrentBlockJobMinRange;
            float maxRange = ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange;
            PreviousMiningJob = CurrentMiningJob;

            if (ListOfJob.Count >= maxRange)
            {
                ListOfJob.Clear();
            }

            if (!UseCustomDifficulty)
            {
                if (jobTarget != 0 && (jobTarget >= ClassMiningPoolGlobalStats.CurrentBlockJobMinRange && jobTarget <= ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange))
                {
                    if (!ListOfJob.Contains(jobTarget))
                    {
                        CurrentMiningJob = jobTarget;
                        CurrentBlockHashOnMining = ClassMiningPoolGlobalStats.CurrentBlockHash;
                        minRange = ClassMiningPoolGlobalStats.CurrentBlockJobMinRange;
                        maxRange = ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange;
                    }
                    else
                    {
                        while (CurrentMiningJob == PreviousMiningJob || ListOfJob.Contains(CurrentMiningJob) || CurrentMiningJob < minRange || CurrentMiningJob > maxRange && CurrentMiningJob == 0)
                        {
                            if (jobTarget <= maxRange)
                            {
                                CurrentMiningJob = ClassUtility.GetRandomBetweenJob(minRange, jobTarget);
                                CurrentBlockHashOnMining = ClassMiningPoolGlobalStats.CurrentBlockHash;
                                minRange = ClassMiningPoolGlobalStats.CurrentBlockJobMinRange;
                                maxRange = ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange;
                            }
                            else
                            {
                                CurrentMiningJob = ClassUtility.GetRandomBetweenJob(minRange, maxRange);
                                CurrentBlockHashOnMining = ClassMiningPoolGlobalStats.CurrentBlockHash;
                                minRange = ClassMiningPoolGlobalStats.CurrentBlockJobMinRange;
                                maxRange = ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange;
                            }
                        }
                    }
                }
                else
                {
                    if (PreviousMiningJob != 0)
                    {

                        while (CurrentMiningJob == PreviousMiningJob || ListOfJob.Contains(CurrentMiningJob) || CurrentMiningJob < minRange || CurrentMiningJob > maxRange && CurrentMiningJob == 0)
                        {
                            if (CurrentHashEffort <= maxRange)
                            {
                                CurrentMiningJob = ClassUtility.GetRandomBetweenJob(minRange, CurrentHashEffort);
                                CurrentBlockHashOnMining = ClassMiningPoolGlobalStats.CurrentBlockHash;
                                minRange = ClassMiningPoolGlobalStats.CurrentBlockJobMinRange;
                                maxRange = ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange;
                            }
                            else
                            {
                                CurrentMiningJob = ClassUtility.GetRandomBetweenJob(minRange, maxRange);
                                CurrentBlockHashOnMining = ClassMiningPoolGlobalStats.CurrentBlockHash;
                                minRange = ClassMiningPoolGlobalStats.CurrentBlockJobMinRange;
                                maxRange = ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange;
                            }
                        }

                    }
                    else
                    {
                        if (MiningDifficultyStart <= maxRange)
                        {
                            if (MiningDifficultyStart <= maxRange)
                            {
                                CurrentMiningJob = MiningDifficultyStart;
                                CurrentBlockHashOnMining = ClassMiningPoolGlobalStats.CurrentBlockHash;
                                minRange = ClassMiningPoolGlobalStats.CurrentBlockJobMinRange;
                                maxRange = ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange;
                            }
                            else
                            {
                                CurrentMiningJob = maxRange;
                                CurrentBlockHashOnMining = ClassMiningPoolGlobalStats.CurrentBlockHash;
                                minRange = ClassMiningPoolGlobalStats.CurrentBlockJobMinRange;
                                maxRange = ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange;
                            }
                        }
                        else
                        {
                            CurrentMiningJob = maxRange;
                            CurrentBlockHashOnMining = ClassMiningPoolGlobalStats.CurrentBlockHash;
                            minRange = ClassMiningPoolGlobalStats.CurrentBlockJobMinRange;
                            maxRange = ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange;
                        }
                    }
                }
            }
            else
            {
                if (MiningDifficultyStart != 0 && (MiningDifficultyStart >= ClassMiningPoolGlobalStats.CurrentBlockJobMinRange && MiningDifficultyStart <= ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange))
                {
                    CurrentMiningJob = MiningDifficultyStart;
                    CurrentBlockHashOnMining = ClassMiningPoolGlobalStats.CurrentBlockHash;
                    minRange = ClassMiningPoolGlobalStats.CurrentBlockJobMinRange;
                    maxRange = ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange;
                }
                else
                {
                    while (CurrentMiningJob == PreviousMiningJob || ListOfJob.Contains(CurrentMiningJob) || CurrentMiningJob < minRange || CurrentMiningJob > maxRange && CurrentMiningJob == 0)
                    {
                        if (MiningDifficultyStart <= maxRange)
                        {
                            CurrentMiningJob = ClassUtility.GetRandomBetweenJob(minRange, MiningDifficultyStart);
                            CurrentBlockHashOnMining = ClassMiningPoolGlobalStats.CurrentBlockHash;
                            minRange = ClassMiningPoolGlobalStats.CurrentBlockJobMinRange;
                            maxRange = ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange;
                        }
                        else
                        {
                            CurrentMiningJob = ClassUtility.GetRandomBetweenJob(minRange, MiningDifficultyStart);
                            CurrentBlockHashOnMining = ClassMiningPoolGlobalStats.CurrentBlockHash;
                            minRange = ClassMiningPoolGlobalStats.CurrentBlockJobMinRange;
                            maxRange = ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange;
                        }
                    }
                }
            }
            ListOfJob.Add(PreviousMiningJob);
            LastJobDateReceive = ClassUtility.GetCurrentDateInSecond();
            LastShareReceived = ClassUtility.GetCurrentDateInMilliSecond();
            TotalGoodShare = 0;

            JObject jobRequest = new JObject
            {
                ["type"] = ClassMiningPoolRequest.TypeJob,
                [ClassMiningPoolRequest.TypeBlock] = ClassMiningPoolGlobalStats.CurrentBlockId,
                [ClassMiningPoolRequest.TypeBlockTimestampCreate] = ClassMiningPoolGlobalStats.CurrentBlockTimestampCreate,
                [ClassMiningPoolRequest.TypeBlockKey] = ClassMiningPoolGlobalStats.CurrentBlockKey,
                [ClassMiningPoolRequest.TypeResult] = CurrentMiningJob,
                [ClassMiningPoolRequest.TypeMinRange] = minRange,
                [ClassMiningPoolRequest.TypeMaxRange] = maxRange,
                [ClassMiningPoolRequest.TypeJobMiningMethodName] = ClassMiningPoolGlobalStats.CurrentBlockMethod,
                [ClassMiningPoolRequest.TypeBlockIndication] = ClassMiningPoolGlobalStats.CurrentBlockIndication,
                [ClassMiningPoolRequest.TypeJobMiningMethodAesRound] = ClassMiningPoolGlobalStats.CurrentRoundAesRound,
                [ClassMiningPoolRequest.TypeJobMiningMethodAesSize] = ClassMiningPoolGlobalStats.CurrentRoundAesSize,
                [ClassMiningPoolRequest.TypeJobMiningMethodAesKey] = ClassMiningPoolGlobalStats.CurrentRoundAesKey,
                [ClassMiningPoolRequest.TypeJobMiningMethodXorKey] = ClassMiningPoolGlobalStats.CurrentRoundXorKey
            };
            try
            {
                ListOfShare?.Clear();
            }
            catch
            {

            }
            if (!await SendPacketToMiner(jobRequest.ToString(Formatting.None)))
            {
                EndMinerConnection();
            }
        }

        /// <summary>
        /// Check share received.
        /// </summary>
        /// <param name="result"></param>
        /// <param name="mathCalculation"></param>
        /// <param name="share"></param>
        /// <param name="hash"></param>
        /// <returns></returns>
        private async Task<string> CheckMinerShare(float result, string mathCalculation, string share, string hash, bool trustedShare)
        {
            TotalShareReceived++;
            if (hash != ClassMiningPoolGlobalStats.CurrentBlockIndication)
            {
                if (ListOfShare.ContainsKey(share))
                {
                    return ClassMiningPoolRequest.TypeResultShareDuplicate;
                }


                if (PreviousMiningJob != 0)
                {
                    if (result != CurrentMiningJob && result != PreviousMiningJob)
                    {
                        return ClassMiningPoolRequest.TypeResultShareInvalid;
                    }
                }
                else
                {
                    if (result != CurrentMiningJob)
                    {
                        return ClassMiningPoolRequest.TypeResultShareInvalid;
                    }
                }
            }
            var splitMathCalculation = mathCalculation.Split(new[] { " " }, StringSplitOptions.None);
            if (float.Parse(splitMathCalculation[0]) >= ClassMiningPoolGlobalStats.CurrentBlockJobMinRange && float.Parse(splitMathCalculation[0]) <= ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange)
            {
                if (float.Parse(splitMathCalculation[2]) >= ClassMiningPoolGlobalStats.CurrentBlockJobMinRange && float.Parse(splitMathCalculation[2]) <= ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange)
                {

                    if (ClassUtility.ComputeCalculation(splitMathCalculation[0], splitMathCalculation[1], splitMathCalculation[2]) != result)
                    {
                        return ClassMiningPoolRequest.TypeResultShareInvalid;
                    }


                    byte[] reverseShare = Convert.FromBase64String(share);

                    if (reverseShare.Length != 96)
                    {
                        return ClassMiningPoolRequest.TypeResultShareInvalid;
                    }

                    byte[] reverseHash = Convert.FromBase64String(hash);

                    if (reverseShare.Length != 96)
                    {
                        return ClassMiningPoolRequest.TypeResultShareInvalid;
                    }

                    if (!trustedShare)
                    {
                        string encryptedShare = ClassUtility.StringToHexString(mathCalculation + ClassMiningPoolGlobalStats.CurrentBlockTimestampCreate);

                        encryptedShare = ClassUtility.EncryptXorShare(encryptedShare, ClassMiningPoolGlobalStats.CurrentRoundXorKey.ToString());
                        for (int i = 0; i < ClassMiningPoolGlobalStats.CurrentRoundAesRound; i++)
                        {
                            encryptedShare = await ClassUtility.EncryptAesShareAsync(encryptedShare, ClassMiningPoolGlobalStats.CurrentBlockKey, Encoding.UTF8.GetBytes(ClassMiningPoolGlobalStats.CurrentRoundAesKey), ClassMiningPoolGlobalStats.CurrentRoundAesSize);
                        }
                        encryptedShare = ClassUtility.EncryptXorShare(encryptedShare, ClassMiningPoolGlobalStats.CurrentRoundXorKey.ToString());
                        encryptedShare = await ClassUtility.EncryptAesShareAsync(encryptedShare, ClassMiningPoolGlobalStats.CurrentBlockKey, Encoding.UTF8.GetBytes(ClassMiningPoolGlobalStats.CurrentRoundAesKey), ClassMiningPoolGlobalStats.CurrentRoundAesSize);
                        encryptedShare = ClassUtility.GenerateSHA512(encryptedShare);

                        if (encryptedShare != share)
                        {
                            return ClassMiningPoolRequest.TypeResultShareInvalid;
                        }

                        string hashShare = ClassUtility.GenerateSHA512(encryptedShare);

                        if (hashShare != hash)
                        {
                            return ClassMiningPoolRequest.TypeResultShareInvalid;
                        }
                    }
                    await CheckShareHashWithBlockIndicationAsync(result, mathCalculation, share, hash).ConfigureAwait(false);
                    if (!ListOfShare.ContainsKey(share))
                    {
                        try
                        {
                            ListOfShare.Add(share, CurrentMiningJob);
                        }
                        catch
                        {
                            return ClassMiningPoolRequest.TypeResultShareDuplicate;
                        }
                    }

                    return ClassMiningPoolRequest.TypeResultShareOk;
                }
                else
                {
                    ClassLog.ConsoleWriteLog("Miner IP " + Ip + " with Wallet Address: " + MinerWalletAddress + " bad number used: "+splitMathCalculation[2], ClassLogEnumeration.IndexPoolMinerErrorLog);
                    return ClassMiningPoolRequest.TypeResultShareLowDifficulty;
                }
            }
            else
            {
                ClassLog.ConsoleWriteLog("Miner IP " + Ip + " with Wallet Address: " + MinerWalletAddress + " bad number used: " + splitMathCalculation[0], ClassLogEnumeration.IndexPoolMinerErrorLog);
                return ClassMiningPoolRequest.TypeResultShareLowDifficulty;
            }
        }

        /// <summary>
        /// Check Share Hash with Block Indication
        /// </summary>
        /// <param name="result"></param>
        /// <param name="mathCalculation"></param>
        /// <param name="share"></param>
        /// <param name="hash"></param>
        /// <returns></returns>
        private async Task CheckShareHashWithBlockIndicationAsync(float result, string mathCalculation, string share, string hash)
        {

            if (hash == ClassMiningPoolGlobalStats.CurrentBlockIndication)
            {

                ClassLog.ConsoleWriteLog("Miner IP " + Ip + " with Wallet Address: " + MinerWalletAddress + " seems to have found the block " + ClassMiningPoolGlobalStats.CurrentBlockId + ", waiting confirmation.", ClassLogEnumeration.IndexPoolMinerLog, ClassLogConsoleEnumeration.IndexPoolConsoleYellowLog, true);
                await Task.Factory.StartNew(() => ClassNetworkBlockchain.SendPacketBlockFound(share, result, mathCalculation, hash), CancellationToken.None, TaskCreationOptions.RunContinuationsAsynchronously, PriorityScheduler.AboveNormal).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Calculate the hashrate of the miner.
        /// </summary>
        /// <returns></returns>
        private async Task CalculateHashrate()
        {

            while (TotalGoodShareDone == 0)
            {
                await Task.Delay(100);
                if (!IsConnected)
                {
                    break;
                }
            }
            float totalShareDone = TotalGoodShareDone;
            string lastBlockHash = ClassMiningPoolGlobalStats.CurrentBlockHash;
            Dictionary<float, string> listShare = new Dictionary<float, string>(); // Job, hashrate
            await Task.Delay(MiningPoolSetting.MiningPoolIntervalChangeJob * 1000);
            while (IsConnected)
            {
                try
                {
                    if (lastBlockHash != ClassMiningPoolGlobalStats.CurrentBlockHash)
                    {
                        lastBlockHash = ClassMiningPoolGlobalStats.CurrentBlockHash;
                    }
                    //Console.WriteLine("Current Mining Job: " + CurrentMiningJob);
                    //Console.WriteLine("Current Max Range Job: " + ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange);
                    float lastJobReceived = ((ClassUtility.GetCurrentDateInSecond() - LastJobDateReceive));
                    float timeSpendConnected = ClassUtility.GetCurrentDateInSecond() - LoginDate;
                    float lastShareReceived = ((ClassUtility.GetCurrentDateInMilliSecond() - LastShareReceived) / 1000.0f);
                    //Console.WriteLine("Total good share done: " + TotalGoodShareDone);
                    float totalSharePerSecond = TotalGoodShareDone / timeSpendConnected;
                    //Console.WriteLine("Total Good Share Per Second: " + totalSharePerSecond);
                    float maxRangePossibility = (ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange / ClassMiningPoolGlobalStats.CurrentBlockJobMinRange) * ClassUtility.RandomOperatorCalculation.Length;
                    //Console.WriteLine("Max Range Possibility: " + maxRangePossibility);
                    float maxJobPossibility = (ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange / CurrentMiningJob) * ClassUtility.RandomOperatorCalculation.Length;
                    //Console.WriteLine("Max Job Possibility: " + maxJobPossibility);
                    float maxJobDoneInSecond = (maxJobPossibility / totalSharePerSecond);
                    float totalJobDone = maxJobPossibility * totalSharePerSecond;
                    //Console.WriteLine("Job total possibility done in: " + maxJobDoneInSecond + " second(s)");
                    float maxRangeDoneInSecond = maxRangePossibility / totalSharePerSecond;
                    //Console.WriteLine("Max total possibility can be done in: " + maxRangeDoneInSecond + " second(s)");
                    float miningEffortPourcent = (maxJobDoneInSecond / maxRangeDoneInSecond) * 100;
                    //Console.WriteLine("Mining Effort Difficulty pourcent: " + miningEffortPourcent + " %");
                    float miningBestJob = (float)Math.Round((CurrentMiningJob * totalSharePerSecond) * miningEffortPourcent, 0);
                    //Console.WriteLine("Mining Best Job Difficulty to target: " + miningBestJob);
                    float estimatedHashrate = 0;
                    if (totalShareDone > 0)
                    {
                        if (lastShareReceived < MiningPoolSetting.MiningPoolIntervalChangeJob)
                        {

                            totalShareDone = (TotalGoodShareDone - totalShareDone);
                            if (totalShareDone > 0)
                            {
                                estimatedHashrate = totalShareDone * CurrentMiningJob;
                                if (listShare.Count > 1)
                                {

                                    if (!listShare.ContainsKey(CurrentMiningJob))
                                    {
                                        listShare.Add(CurrentMiningJob, totalShareDone + "|" + ClassMiningPoolGlobalStats.CurrentBlockId);
                                    }
                                    else
                                    {
                                        var splitShare = listShare[CurrentMiningJob].Split(new[] { "|" }, StringSplitOptions.None);
                                        var totalShare = float.Parse(splitShare[0]);
                                        if (totalShare < totalShareDone)
                                        {
                                            listShare[CurrentMiningJob] = (totalShare + totalShareDone) + "|" + ClassMiningPoolGlobalStats.CurrentBlockId;
                                        }
                                    }
                                    float tmpHashrate = 0;
                                    foreach (var hashrate in listShare)
                                    {
                                        var splitShare = hashrate.Value.Split(new[] { "|" }, StringSplitOptions.None);
                                        var totalShare = float.Parse(splitShare[0]);
                                        var blockIdShare = long.Parse(splitShare[1]);
                                        if (blockIdShare == int.Parse(ClassMiningPoolGlobalStats.CurrentBlockId))
                                        {
                                            tmpHashrate += (hashrate.Key * totalShare);
                                        }
                                    }
                                    estimatedHashrate = (tmpHashrate / (listShare.Count-1));
                                    if (estimatedHashrate != float.PositiveInfinity && estimatedHashrate != float.NegativeInfinity && !float.IsNaN(estimatedHashrate))
                                    {
                                        CurrentHashrate = estimatedHashrate * 3;
                                    }
                                }
                                else
                                {
                                    if (estimatedHashrate != float.PositiveInfinity && estimatedHashrate != float.NegativeInfinity && !float.IsNaN(estimatedHashrate))
                                    {
                                        CurrentHashrate = estimatedHashrate * 3;
                                    }
                                    if (listShare.ContainsKey(CurrentMiningJob))
                                    {
                                        var splitShare = listShare[CurrentMiningJob].Split(new[] { "|" }, StringSplitOptions.None);
                                        var totalShare = float.Parse(splitShare[0]);
                                        if (totalShare < totalShareDone)
                                        {
                                            listShare[CurrentMiningJob] = (totalShare + totalShareDone) + "|" + ClassMiningPoolGlobalStats.CurrentBlockId;
                                        }
                                    }
                                    else
                                    {
                                        listShare.Add(CurrentMiningJob, totalShareDone + "|" + ClassMiningPoolGlobalStats.CurrentBlockId);
                                    }
                                }
                            }
                        }
                    }


                    CurrentHashEffort = miningBestJob;
                    if (!UseCustomDifficulty || totalSharePerSecond > 10 || (miningBestJob > CurrentMiningJob * 1.35f)) // Change the difficulty if the miner don't use a custom difficulty or send too much share per second for prevent flood and overloading.
                    {
                        UseCustomDifficulty = false;
                        if (miningBestJob * 1.35f < CurrentMiningJob)
                        {
                            MiningPoolSendJobAsync(miningBestJob);
                        }
                        else
                        {
                            if (miningBestJob > CurrentMiningJob * 1.35f)
                            {
                                MiningPoolSendJobAsync(miningBestJob);
                            }
                        }
                    }
                }
                catch
                {

                }
                totalShareDone = TotalGoodShareDone;
                await Task.Delay(1000);
            }
            listShare.Clear();
        }

        #endregion

        #region Network Functions

        /// <summary>
        /// Handle incoming miner, check if his ip is banned.
        /// </summary>
        /// <returns></returns>
        public async Task HandleIncomingMiner(int miningPoolPort, float difficultyStart)
        {
            MiningDifficultyStart = difficultyStart;
            Ip = ((IPEndPoint)(TcpMiner.Client.RemoteEndPoint)).Address.ToString();
            ClassLog.ConsoleWriteLog("Incoming miner connection IP " + Ip + " on mining pool port: " + miningPoolPort + " with difficulty start of: "+MiningDifficultyStart, ClassLogEnumeration.IndexPoolMinerLog);

            if (!ClassFilteringMiner.CheckMinerIsBannedByIP(Ip)) // The miner IP is not banned.
            {
                TcpMiner.SetSocketKeepAliveValues(20 * 60 * 1000, 30 * 1000);
                IsConnected = true;
                LastPacketReceived = ClassUtility.GetCurrentDateInSecond();
                await Task.Factory.StartNew(CheckMinerConnection, CancellationToken.None, TaskCreationOptions.LongRunning, PriorityScheduler.Lowest).ConfigureAwait(false);
                await ListenMinerConnection().ConfigureAwait(false);
            }
            else
            {
                ClassLog.ConsoleWriteLog("Miner IP " + Ip + " is banned, disconnect the miner..", ClassLogEnumeration.IndexPoolMinerErrorLog);
                EndMinerConnection();
            }
        }

        /// <summary>
        /// Listen packets received from the miner connected.
        /// </summary>
        private async Task ListenMinerConnection()
        {
            while (IsConnected)
            {
                try
                {
                    if (!IsConnected || IsDisposed || Program.Exit)
                    {
                        break;
                    }

                    using (NetworkStream networkReader = new NetworkStream(TcpMiner.Client))
                    {
                        using (var bufferPacket = new IncomingPacketConnectionObject())
                        {
                            int received = 0;
                            while ((received = await networkReader.ReadAsync(bufferPacket.buffer, 0, bufferPacket.buffer.Length).ConfigureAwait(false)) > 0)
                            {
                                if (received > 0)
                                {
                                    LastPacketReceived = ClassUtility.GetCurrentDateInSecond();
                                    TotalPacketReceivedPerSecond++;
                                    bufferPacket.packet = Encoding.UTF8.GetString(bufferPacket.buffer, 0, received);
                                    if(bufferPacket.packet.Contains(Environment.NewLine))
                                    {
                                        var splitPacket = bufferPacket.packet.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                                        foreach(var packetEach in splitPacket)
                                        {
                                            if (packetEach != null)
                                            {
                                                if (!string.IsNullOrEmpty(packetEach))
                                                {
                                                    if (packetEach.Length > 1)
                                                    {
                                                        await Task.Factory.StartNew(() => HandleMinerPacketAsync(packetEach), CancellationToken.None, TaskCreationOptions.RunContinuationsAsynchronously, PriorityScheduler.Lowest).ConfigureAwait(false);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        await Task.Factory.StartNew(() => HandleMinerPacketAsync(bufferPacket.packet), CancellationToken.None, TaskCreationOptions.RunContinuationsAsynchronously, PriorityScheduler.Lowest).ConfigureAwait(false);

                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception error)
                {
                    ClassLog.ConsoleWriteLog("Miner IP " + Ip + " listen connection error: " + error.Message + ", disconnect the miner..", ClassLogEnumeration.IndexPoolMinerErrorLog);
                    break;
                }
            }
            EndMinerConnection();
        }

        /// <summary>
        /// Handle packet received from miner.
        /// </summary>
        /// <param name="packet"></param>
        private async void HandleMinerPacketAsync(string packetReceived)
        {
            try
            {
                try
                {
                    JObject packetJson = JObject.Parse(packetReceived);
                    switch (packetJson["type"].ToString().ToLower())
                    {
                        case ClassMiningPoolRequest.TypeLogin:
                            if (!IsLogged)
                            {
                                var minerWalletAddressTmp = packetJson[ClassMiningPoolRequest.SubmitWalletAddress].ToString();
                                if (minerWalletAddressTmp.Contains("."))
                                {
                                    var splitMinerInfo = minerWalletAddressTmp.Split(new[] { "." }, StringSplitOptions.None);
                                    if (long.TryParse(splitMinerInfo[1], out var customDifficulty))
                                    {
                                        MiningDifficultyStart = customDifficulty;
                                        UseCustomDifficulty = true;
                                        ClassLog.ConsoleWriteLog("Incoming miner connection IP " + Ip + " login packet received - Wallet Address: " + splitMinerInfo[0] + " | Custom Difficulty: "+customDifficulty+" | Version: " + MinerVersion + ".", ClassLogEnumeration.IndexPoolMinerLog);

                                    }
                                    MinerWalletAddress = splitMinerInfo[0];
                                    if (!ClassMinerStats.CheckMinerIsBannedByWalletAddress(MinerWalletAddress))
                                    {
                                        MinerVersion = packetJson[ClassMiningPoolRequest.SubmitVersion].ToString();
                                        IsLogged = true;
                                        LoginDate = DateTimeOffset.Now.ToUnixTimeSeconds();
                                        ClassLog.ConsoleWriteLog("Incoming miner connection IP " + Ip + " login packet received - Wallet Address: " + MinerWalletAddress + " | Version: " + MinerVersion + ".", ClassLogEnumeration.IndexPoolMinerLog);
                                        ClassMinerStats.InsertMinerTcpObject(MinerWalletAddress, this);
                                        await Task.Factory.StartNew(CalculateHashrate, CancellationToken.None, TaskCreationOptions.LongRunning, PriorityScheduler.Lowest).ConfigureAwait(false);
                                        MiningPoolSendJobAsync(MiningDifficultyStart);
                                    }
                                    else
                                    {
                                        ClassLog.ConsoleWriteLog("Incoming miner connection IP " + Ip + " login packet received - Wallet Address: " + MinerWalletAddress + " | Version: " + MinerVersion + " is banned.", ClassLogEnumeration.IndexPoolMinerErrorLog);
                                        EndMinerConnection();
                                    }
                                }
                                else
                                {
                                    MinerWalletAddress = minerWalletAddressTmp;
                                    if (!ClassMinerStats.CheckMinerIsBannedByWalletAddress(MinerWalletAddress))
                                    {
                                        MinerVersion = packetJson[ClassMiningPoolRequest.SubmitVersion].ToString();
                                        IsLogged = true;
                                        LoginDate = DateTimeOffset.Now.ToUnixTimeSeconds();
                                        ClassLog.ConsoleWriteLog("Incoming miner connection IP " + Ip + " login packet received - Wallet Address: " + MinerWalletAddress + " | Version: " + MinerVersion + ".", ClassLogEnumeration.IndexPoolMinerLog);
                                        ClassMinerStats.InsertMinerTcpObject(MinerWalletAddress, this);
                                        await Task.Factory.StartNew(CalculateHashrate, CancellationToken.None, TaskCreationOptions.LongRunning, PriorityScheduler.Lowest).ConfigureAwait(false);
                                        MiningPoolSendJobAsync(MiningDifficultyStart);
                                    }
                                    else
                                    {
                                        ClassLog.ConsoleWriteLog("Incoming miner connection IP " + Ip + " login packet received - Wallet Address: " + MinerWalletAddress + " | Version: " + MinerVersion + " is banned.", ClassLogEnumeration.IndexPoolMinerErrorLog);
                                        EndMinerConnection();
                                    }
                                }
                            }
                            else
                            {
                                ClassLog.ConsoleWriteLog("Miner IP " + Ip + " with Wallet Address: " + MinerWalletAddress + " is already connected to this opened connection, insert invalid packet to filtering.", ClassLogEnumeration.IndexPoolMinerErrorLog);
                                ClassFilteringMiner.InsertInvalidPacket(Ip);
                                EndMinerConnection();
                            }
                            break;
                        case ClassMiningPoolRequest.TypeSubmit:
                            if (!IsLogged)
                            {
                                ClassLog.ConsoleWriteLog("Miner IP " + Ip + " is not connected and try to send a share: " + packetReceived + ", insert invalid packet to filtering.", ClassLogEnumeration.IndexPoolMinerErrorLog);
                                ClassFilteringMiner.InsertInvalidPacket(Ip);
                                EndMinerConnection();
                            }
                            else
                            {
                                LastShareReceived = ClassUtility.GetCurrentDateInMilliSecond();
                                float result = float.Parse(packetJson[ClassMiningPoolRequest.SubmitResult].ToString());
                                string firstNumber = packetJson[ClassMiningPoolRequest.SubmitFirstNumber].ToString();
                                string secondNumber = packetJson[ClassMiningPoolRequest.SubmitSecondNumber].ToString();
                                string operatorMath = packetJson[ClassMiningPoolRequest.SubmitOperator].ToString();
                                string share = packetJson[ClassMiningPoolRequest.SubmitShare].ToString();
                                string hash = packetJson[ClassMiningPoolRequest.SubmitHash].ToString();
                                string mathCalculation = firstNumber + " " + operatorMath + " " + secondNumber;
                                if (MiningPoolSetting.MiningPoolEnableTrustedShare)
                                {
                                    if (ClassMinerStats.CheckMinerIsTrusted(MinerWalletAddress))
                                    {
                                        switch (await CheckMinerShare(result, mathCalculation, share, hash, true))
                                        {
                                            case ClassMiningPoolRequest.TypeResultShareInvalid:

                                                ClassLog.ConsoleWriteLog("Miner IP " + Ip + " with Wallet Address: " + MinerWalletAddress + " send a invalid share.", ClassLogEnumeration.IndexPoolMinerErrorLog);
                                                ClassMinerStats.InsertInvalidShareToMiner(MinerWalletAddress);
                                                JObject jobRequestInvalid = new JObject
                                                {
                                                    ["type"] = ClassMiningPoolRequest.TypeShare,
                                                    [ClassMiningPoolRequest.TypeResult] = ClassMiningPoolRequest.TypeResultShareInvalid
                                                };
                                                if (!await SendPacketToMiner(jobRequestInvalid.ToString(Formatting.None)))
                                                {
                                                    EndMinerConnection();
                                                }

                                                break;
                                            case ClassMiningPoolRequest.TypeResultShareDuplicate:
                                                ClassLog.ConsoleWriteLog("Miner IP " + Ip + " with Wallet Address: " + MinerWalletAddress + " send a duplicate difficulty share.", ClassLogEnumeration.IndexPoolMinerLog);
                                                ClassMinerStats.InsertInvalidShareToMiner(MinerWalletAddress, true);
                                                JObject jobRequestDuplicate = new JObject
                                                {
                                                    ["type"] = ClassMiningPoolRequest.TypeShare,
                                                    [ClassMiningPoolRequest.TypeResult] = ClassMiningPoolRequest.TypeResultShareDuplicate
                                                };
                                                if (!await SendPacketToMiner(jobRequestDuplicate.ToString(Formatting.None)))
                                                {
                                                    EndMinerConnection();
                                                }

                                                break;
                                            case ClassMiningPoolRequest.TypeResultShareLowDifficulty:
                                                ClassLog.ConsoleWriteLog("Miner IP " + Ip + " with Wallet Address: " + MinerWalletAddress + " send a low difficulty share.", ClassLogEnumeration.IndexPoolMinerErrorLog);
                                                ClassMinerStats.InsertInvalidShareToMiner(MinerWalletAddress);
                                                JObject jobRequestLowDifficulty = new JObject
                                                {
                                                    ["type"] = ClassMiningPoolRequest.TypeShare,
                                                    [ClassMiningPoolRequest.TypeResult] = ClassMiningPoolRequest.TypeResultShareLowDifficulty
                                                };
                                                if (!await SendPacketToMiner(jobRequestLowDifficulty.ToString(Formatting.None)))
                                                {
                                                    EndMinerConnection();
                                                }

                                                break;
                                            case ClassMiningPoolRequest.TypeResultShareOk:
                                                ClassLog.ConsoleWriteLog("Miner IP " + Ip + " with Wallet Address: " + MinerWalletAddress + " trusted share accepted. Job: " + CurrentMiningJob + "/" + ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange, ClassLogEnumeration.IndexPoolMinerLog);
                                                ClassMinerStats.InsertGoodShareToMiner(MinerWalletAddress, CurrentMiningJob);
                                                await CheckShareHashWithBlockIndicationAsync(result, mathCalculation, share, hash);
                                                TotalGoodShare++;
                                                TotalGoodShareDone++;

                                                JObject jobRequest = new JObject
                                                {
                                                    ["type"] = ClassMiningPoolRequest.TypeShare,
                                                    [ClassMiningPoolRequest.TypeResult] = ClassMiningPoolRequest.TypeResultShareOk
                                                };
                                                if (!await SendPacketToMiner(jobRequest.ToString(Formatting.None)))
                                                {
                                                    EndMinerConnection();
                                                }
                                                break;
                                        }
  
                                    }
                                    else
                                    {
                                        switch (await CheckMinerShare(result, mathCalculation, share, hash, false))
                                        {
                                            case ClassMiningPoolRequest.TypeResultShareInvalid:

                                                ClassLog.ConsoleWriteLog("Miner IP " + Ip + " with Wallet Address: " + MinerWalletAddress + " send a invalid share.", ClassLogEnumeration.IndexPoolMinerErrorLog);
                                                ClassMinerStats.InsertInvalidShareToMiner(MinerWalletAddress);
                                                JObject jobRequestInvalid = new JObject
                                                {
                                                    ["type"] = ClassMiningPoolRequest.TypeShare,
                                                    [ClassMiningPoolRequest.TypeResult] = ClassMiningPoolRequest.TypeResultShareInvalid
                                                };
                                                if (!await SendPacketToMiner(jobRequestInvalid.ToString(Formatting.None)))
                                                {
                                                    EndMinerConnection();
                                                }

                                                break;
                                            case ClassMiningPoolRequest.TypeResultShareDuplicate:
                                                ClassLog.ConsoleWriteLog("Miner IP " + Ip + " with Wallet Address: " + MinerWalletAddress + " send a duplicate difficulty share.", ClassLogEnumeration.IndexPoolMinerLog);
                                                ClassMinerStats.InsertInvalidShareToMiner(MinerWalletAddress, true);
                                                JObject jobRequestDuplicate = new JObject
                                                {
                                                    ["type"] = ClassMiningPoolRequest.TypeShare,
                                                    [ClassMiningPoolRequest.TypeResult] = ClassMiningPoolRequest.TypeResultShareDuplicate
                                                };
                                                if (!await SendPacketToMiner(jobRequestDuplicate.ToString(Formatting.None)))
                                                {
                                                    EndMinerConnection();
                                                }

                                                break;
                                            case ClassMiningPoolRequest.TypeResultShareLowDifficulty:
                                                ClassLog.ConsoleWriteLog("Miner IP " + Ip + " with Wallet Address: " + MinerWalletAddress + " send a low difficulty share.", ClassLogEnumeration.IndexPoolMinerErrorLog);
                                                ClassMinerStats.InsertInvalidShareToMiner(MinerWalletAddress);
                                                JObject jobRequestLowDifficulty = new JObject
                                                {
                                                    ["type"] = ClassMiningPoolRequest.TypeShare,
                                                    [ClassMiningPoolRequest.TypeResult] = ClassMiningPoolRequest.TypeResultShareLowDifficulty
                                                };
                                                if (!await SendPacketToMiner(jobRequestLowDifficulty.ToString(Formatting.None)))
                                                {
                                                    EndMinerConnection();
                                                }

                                                break;
                                            case ClassMiningPoolRequest.TypeResultShareOk:
                                                JObject jobRequestGood = new JObject
                                                {
                                                    ["type"] = ClassMiningPoolRequest.TypeShare,
                                                    [ClassMiningPoolRequest.TypeResult] = ClassMiningPoolRequest.TypeResultShareOk
                                                };
                                                if (!await SendPacketToMiner(jobRequestGood.ToString(Formatting.None)))
                                                {
                                                    EndMinerConnection();
                                                }

                                                ClassLog.ConsoleWriteLog("Miner IP " + Ip + " with Wallet Address: " + MinerWalletAddress + " good share accepted. Job: " + CurrentMiningJob + "/" + ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange, ClassLogEnumeration.IndexPoolMinerLog);
                                                ClassMinerStats.InsertGoodShareToMiner(MinerWalletAddress, CurrentMiningJob);
                                                TotalGoodShare++;
                                                TotalGoodShareDone++;
                                                break;
                                        }
                                    }
                                }
                                else
                                {
                                    switch (await CheckMinerShare(result, mathCalculation, share, hash, false))
                                    {
                                        case ClassMiningPoolRequest.TypeResultShareInvalid:
                                            ClassLog.ConsoleWriteLog("Miner IP " + Ip + " with Wallet Address: " + MinerWalletAddress + " send a invalid share.", ClassLogEnumeration.IndexPoolMinerErrorLog);
                                            ClassMinerStats.InsertInvalidShareToMiner(MinerWalletAddress);
                                            JObject jobRequestInvalid = new JObject
                                            {
                                                ["type"] = ClassMiningPoolRequest.TypeShare,
                                                [ClassMiningPoolRequest.TypeResult] = ClassMiningPoolRequest.TypeResultShareInvalid
                                            };
                                            if (!await SendPacketToMiner(jobRequestInvalid.ToString(Formatting.None)))
                                            {
                                                EndMinerConnection();
                                            }

                                            break;
                                        case ClassMiningPoolRequest.TypeResultShareDuplicate:
                                            ClassLog.ConsoleWriteLog("Miner IP " + Ip + " with Wallet Address: " + MinerWalletAddress + " send a duplicate difficulty share.", ClassLogEnumeration.IndexPoolMinerLog);
                                            ClassMinerStats.InsertInvalidShareToMiner(MinerWalletAddress, true);
                                            JObject jobRequestDuplicate = new JObject
                                            {
                                                ["type"] = ClassMiningPoolRequest.TypeShare,
                                                [ClassMiningPoolRequest.TypeResult] = ClassMiningPoolRequest.TypeResultShareDuplicate
                                            };
                                            if (!await SendPacketToMiner(jobRequestDuplicate.ToString(Formatting.None)))
                                            {
                                                EndMinerConnection();
                                            }

                                            break;
                                        case ClassMiningPoolRequest.TypeResultShareLowDifficulty:
                                            ClassLog.ConsoleWriteLog("Miner IP " + Ip + " with Wallet Address: " + MinerWalletAddress + " send a low difficulty share.", ClassLogEnumeration.IndexPoolMinerErrorLog);
                                            ClassMinerStats.InsertInvalidShareToMiner(MinerWalletAddress);
                                            JObject jobRequestLowDifficulty = new JObject
                                            {
                                                ["type"] = ClassMiningPoolRequest.TypeShare,
                                                [ClassMiningPoolRequest.TypeResult] = ClassMiningPoolRequest.TypeResultShareLowDifficulty
                                            };
                                            if (!await SendPacketToMiner(jobRequestLowDifficulty.ToString(Formatting.None)))
                                            {
                                                EndMinerConnection();
                                            }

                                            break;
                                        case ClassMiningPoolRequest.TypeResultShareOk:
                                            JObject jobRequestGood = new JObject
                                            {
                                                ["type"] = ClassMiningPoolRequest.TypeShare,
                                                [ClassMiningPoolRequest.TypeResult] = ClassMiningPoolRequest.TypeResultShareOk
                                            };
                                            if (!await SendPacketToMiner(jobRequestGood.ToString(Formatting.None)))
                                            {
                                                EndMinerConnection();
                                            }

                                            ClassLog.ConsoleWriteLog("Miner IP " + Ip + " with Wallet Address: " + MinerWalletAddress + " good share accepted. Job: " + CurrentMiningJob + "/" + ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange, ClassLogEnumeration.IndexPoolMinerLog);
                                            ClassMinerStats.InsertGoodShareToMiner(MinerWalletAddress, CurrentMiningJob);
                                            TotalGoodShare++;
                                            TotalGoodShareDone++;
                                            break;
                                    }
                                }
                            }
                            break;
                        default:
                            ClassLog.ConsoleWriteLog("Miner IP " + Ip + " unknown packet received: " + packetReceived + ", insert invalid packet to filtering.", ClassLogEnumeration.IndexPoolMinerErrorLog);
                            ClassFilteringMiner.InsertInvalidPacket(Ip);
                            break;
                    }
                }
                catch(JsonException errorJson)
                {
                    if (!packetReceived.Contains("type"))
                    {
                        ClassLog.ConsoleWriteLog("Miner IP " + Ip + " invalid packet received: " + packetReceived + " json error: " + errorJson.Message + ", insert invalid packet to filtering.", ClassLogEnumeration.IndexPoolMinerErrorLog);
                        ClassFilteringMiner.InsertInvalidPacket(Ip);
                        if (ClassFilteringMiner.CheckMinerIsBannedByIP(Ip))
                        {
                            EndMinerConnection();
                        }
                    }
                }
            }
            catch (Exception error)
            {
                ClassLog.ConsoleWriteLog("Miner IP " + Ip + " invalid packet received: " + packetReceived + " error: " + error.Message + ", insert invalid packet to filtering.", ClassLogEnumeration.IndexPoolMinerErrorLog);
                ClassFilteringMiner.InsertInvalidPacket(Ip);
                if (ClassFilteringMiner.CheckMinerIsBannedByIP(Ip))
                {
                    EndMinerConnection();
                }
            }
        }

        /// <summary>
        /// Check the miner connection status.
        /// </summary>
        private async Task CheckMinerConnection()
        {
            while(IsConnected)
            {
                try
                {
                    if (Program.Exit)
                    {
                        IsConnected = false;
                        break;
                    }
                    if (IsLogged)
                    {
                        if (!string.IsNullOrEmpty(MinerWalletAddress))
                        {
                            if (ClassMinerStats.CheckMinerIsBannedByWalletAddress(MinerWalletAddress))
                            {
                                ClassLog.ConsoleWriteLog("Miner IP " + Ip + " with wallet address: " + MinerWalletAddress + " is banned, disconnect the miner..", ClassLogEnumeration.IndexPoolMinerErrorLog);
                                IsConnected = false;
                                break;
                            }
                        }
                        if (LastShareReceived + (MiningPoolSetting.MiningPoolIntervalChangeJob * 1000) < ClassUtility.GetCurrentDateInMilliSecond())
                        {
                            MiningPoolSendJobAsync(0);
                        }
                        if (CurrentBlockHashOnMining != ClassMiningPoolGlobalStats.CurrentBlockHash || CurrentMiningJob == 0)
                        {
                            ListOfJob.Clear();
                            MiningPoolSendJobAsync(0);
                        }
                        else
                        {
                            if (TotalGoodShare > 0)
                            {
                                float maxExpectedShare = (ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange / CurrentMiningJob) * ClassUtility.RandomOperatorCalculation.Length;
                                maxExpectedShare = (int)Math.Round(maxExpectedShare);
                                if (maxExpectedShare > 1)
                                {
                                    maxExpectedShare--;
                                }
                                if (TotalGoodShare + 1 >= maxExpectedShare - 1)
                                {
                                    MiningPoolSendJobAsync(0);
                                }
                            }
                        }
                        JObject requestKeepAlive = new JObject
                        {
                            ["type"] = ClassMiningPoolRequest.TypeKeepAlive,
                        };
                        if (!await SendPacketToMiner(requestKeepAlive.ToString(Formatting.None)))
                        {
                            IsConnected = false;
                            break;
                        }
                    }

                    if (!ClassUtils.SocketIsConnected(TcpMiner))
                    {
                        ClassLog.ConsoleWriteLog("Miner IP " + Ip + " connection status is dead, disconnect the miner..", ClassLogEnumeration.IndexPoolMinerErrorLog);
                        IsConnected = false;
                        break;
                    }
                    if (ClassFilteringMiner.CheckMinerIsBannedByIP(Ip)) // The miner IP is not banned.
                    {
                        ClassLog.ConsoleWriteLog("Miner IP " + Ip + " is banned, disconnect the miner..", ClassLogEnumeration.IndexPoolMinerErrorLog);
                        IsConnected = false;
                        break;
                    }
                    if (LastPacketReceived + MiningPoolSetting.MiningPoolTimeout < ClassUtility.GetCurrentDateInSecond())
                    {
                        ClassLog.ConsoleWriteLog("Miner IP " + Ip + " timeout, disconnect the miner..", ClassLogEnumeration.IndexPoolMinerErrorLog);
                        IsConnected = false;
                        break;
                    }
                }
                catch (Exception error)
                {
                    ClassLog.ConsoleWriteLog("Miner IP " + Ip + " disconnected, exception error: " + error.Message, ClassLogEnumeration.IndexPoolMinerErrorLog);
                    IsConnected = false;
                    break;
                }
                await Task.Delay(1000);
            }
            EndMinerConnection();
        }

        /// <summary>
        /// Stop to listen the miner connection.
        /// </summary>
        public void EndMinerConnection()
        {
            try
            {
                TcpMiner?.Close();
                TcpMiner?.Dispose();
            }
            catch
            {

            }
            try
            {
                ListOfShare?.Clear();
            }
            catch
            {

            }
            MinerWalletAddress = string.Empty;
            IsLogged = false;
            IsConnected = false;
            Dispose();
        }

        /// <summary>
        /// Send a packet to the miner.
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
        private async Task<bool> SendPacketToMiner(string packet)
        {
            try
            {
                using (var networkStream = new NetworkStream(TcpMiner.Client))
                {
                    using (var packetObject = new IncomingConnectionObjectSendPacket(packet + Environment.NewLine))
                    {
                        await networkStream.WriteAsync(packetObject.packetByte, 0, packetObject.packetByte.Length).ConfigureAwait(false);
                        await networkStream.FlushAsync().ConfigureAwait(false);
                    }
                }
            }
            catch
            {
                return false;
            }
            return true;
        }

        #endregion

    }

    public class IncomingConnectionObjectSendPacket : IDisposable
    {
        public byte[] packetByte;
        private bool disposed;

        public IncomingConnectionObjectSendPacket(string packet)
        {
            packetByte = Encoding.UTF8.GetBytes(packet);
        }

        ~IncomingConnectionObjectSendPacket()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                packetByte = null;
            }

            disposed = true;
        }
    }

}
