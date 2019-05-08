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
using Xiropht_Mining_Pool.Payment;
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
                        await TcpListenerMiningPool.AcceptTcpClientAsync().ContinueWith(async clientTask =>
                        {
                            var client = await clientTask;
                            await HandleMiner(client).ConfigureAwait(false);
                        }).ConfigureAwait(false);
                    }
                    catch (Exception error)
                    {
                        ClassLog.ConsoleWriteLog("Error on Listen incoming connection to Mining Pool port: " + MiningPoolPort + ", exception: " + error.Message, ClassLogEnumeration.IndexPoolMinerErrorLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
                    }
                }
            });
            ThreadMiningPool.Start();
        }

        /// <summary>
        /// Stop mining pool.
        /// </summary>
        public void StopMiningPool()
        {
            ClassLog.ConsoleWriteLog("Stop mining pool port " + MiningPoolPort + "..", ClassLogEnumeration.IndexPoolMinerLog, ClassLogConsoleEnumeration.IndexPoolConsoleYellowLog, true);
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
            ClassLog.ConsoleWriteLog("Mining pool port " + MiningPoolPort + " stopped.", ClassLogEnumeration.IndexPoolMinerLog, ClassLogConsoleEnumeration.IndexPoolConsoleGreenLog, true);
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
                    await Task.Delay(ClassUtility.GetRandomBetween(100, 500)); // Insert a delay.
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
        public float CurrentMiningJobDifficulty;
        private float PreviousMiningJob;
        private float MiningDifficultyStart;
        public int TotalGoodShare;
        public long TotalGoodShareDone;
        public float TotalMiningScore;

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
            if (MiningDifficultyStart > maxRange)
            {
                MiningDifficultyStart = maxRange;
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
                if (MiningDifficultyStart > 0 && MiningDifficultyStart <= maxRange)
                {
                    float realDifficulty = (maxRange / MiningDifficultyStart) * ClassUtility.RandomOperatorCalculation.Length;
                    float realJob = (float)Math.Round((maxRange / realDifficulty), 0);

                    if (realJob != 0 && (realJob >= ClassMiningPoolGlobalStats.CurrentBlockJobMinRange && realJob <= ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange))
                    {
                        CurrentMiningJob = realJob;
                        CurrentBlockHashOnMining = ClassMiningPoolGlobalStats.CurrentBlockHash;
                        minRange = ClassMiningPoolGlobalStats.CurrentBlockJobMinRange;
                        maxRange = ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange;
                    }
                    else
                    {
                        while (CurrentMiningJob == PreviousMiningJob || ListOfJob.Contains(CurrentMiningJob) || CurrentMiningJob < minRange || CurrentMiningJob > maxRange && CurrentMiningJob == 0)
                        {
                            if (realJob <= maxRange)
                            {
                                CurrentMiningJob = ClassUtility.GetRandomBetweenJob(minRange, realJob);
                                CurrentBlockHashOnMining = ClassMiningPoolGlobalStats.CurrentBlockHash;
                                minRange = ClassMiningPoolGlobalStats.CurrentBlockJobMinRange;
                                maxRange = ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange;
                            }
                            else
                            {
                                CurrentMiningJob = ClassUtility.GetRandomBetweenJob(minRange, realJob);
                                CurrentBlockHashOnMining = ClassMiningPoolGlobalStats.CurrentBlockHash;
                                minRange = ClassMiningPoolGlobalStats.CurrentBlockJobMinRange;
                                maxRange = ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange;
                            }
                        }
                    }
                }
                else
                {
                    while (CurrentMiningJob == PreviousMiningJob || ListOfJob.Contains(CurrentMiningJob) || CurrentMiningJob < minRange || CurrentMiningJob > maxRange && CurrentMiningJob == 0)
                    {
                        if (CurrentHashEffort <= maxRange)
                        {
                            CurrentMiningJob = ClassUtility.GetRandomBetweenJob(minRange, maxRange);
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
            ListOfJob.Add(PreviousMiningJob);
            LastJobDateReceive = ClassUtility.GetCurrentDateInSecond();
            LastShareReceived = ClassUtility.GetCurrentDateInMilliSecond();
            TotalGoodShare = 0;
            float currentMiningJobDifficultyTmp = CurrentMiningJob * ClassUtility.RandomOperatorCalculation.Length;
            if (currentMiningJobDifficultyTmp > 0)
            {
                CurrentMiningJobDifficulty = (float)Math.Round(currentMiningJobDifficultyTmp, 0);
            }
            else
            {
                CurrentMiningJobDifficulty = maxRange;
            }
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
                [ClassMiningPoolRequest.TypeJobMiningMethodXorKey] = ClassMiningPoolGlobalStats.CurrentRoundXorKey,
                [ClassMiningPoolRequest.TypeJobDifficulty] = CurrentMiningJobDifficulty
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
        private string CheckMinerShare(float result, string mathCalculation, string share, string hash, bool trustedShare)
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
                    if (result > ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange)
                    {
                        return ClassMiningPoolRequest.TypeResultShareInvalid;
                    }
                    if (result < ClassMiningPoolGlobalStats.CurrentBlockJobMinRange)
                    {
                        return ClassMiningPoolRequest.TypeResultShareInvalid;
                    }
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
                            encryptedShare = ClassUtility.EncryptAesShareAsync(encryptedShare, ClassMiningPoolGlobalStats.CurrentBlockKey, Encoding.UTF8.GetBytes(ClassMiningPoolGlobalStats.CurrentRoundAesKey), ClassMiningPoolGlobalStats.CurrentRoundAesSize);
                        }
                        encryptedShare = ClassUtility.EncryptXorShare(encryptedShare, ClassMiningPoolGlobalStats.CurrentRoundXorKey.ToString());
                        encryptedShare = ClassUtility.EncryptAesShareAsync(encryptedShare, ClassMiningPoolGlobalStats.CurrentBlockKey, Encoding.UTF8.GetBytes(ClassMiningPoolGlobalStats.CurrentRoundAesKey), ClassMiningPoolGlobalStats.CurrentRoundAesSize);
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

                    if (hash == ClassMiningPoolGlobalStats.CurrentBlockIndication)
                    {
                        CheckShareHashWithBlockIndicationAsync(result, mathCalculation, share, hash);
                    }
                    try
                    {
                        if (!ListOfShare.ContainsKey(share))
                        {
                            ListOfShare.Add(share, CurrentMiningJob);
                        }
                        else
                        {
                            return ClassMiningPoolRequest.TypeResultShareDuplicate;
                        }
                    }
                    catch
                    {
                        return ClassMiningPoolRequest.TypeResultShareDuplicate;
                    }

                    TotalMiningScore += CurrentMiningJobDifficulty;
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
        private async void CheckShareHashWithBlockIndicationAsync(float result, string mathCalculation, string share, string hash)
        {
            ClassLog.ConsoleWriteLog("Miner IP " + Ip + " with Wallet Address: " + MinerWalletAddress + " seems to have found the block " + ClassMiningPoolGlobalStats.CurrentBlockId + ", waiting confirmation.", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleYellowLog, true);
            await Task.Factory.StartNew(() => ClassNetworkBlockchain.SendPacketBlockFound(share, result, mathCalculation, hash), CancellationToken.None, TaskCreationOptions.RunContinuationsAsynchronously, PriorityScheduler.AboveNormal).ConfigureAwait(false);
        }

        /// <summary>
        /// Calculate the hashrate of the miner.
        /// </summary>
        /// <returns></returns>
        private async Task CalculateHashrate()
        {
            while (IsConnected && IsLogged)
            {
                try
                {

                    float lastJobReceived = ((ClassUtility.GetCurrentDateInSecond() - LastJobDateReceive));
                    float timeSpendConnected = ClassUtility.GetCurrentDateInSecond() - LoginDate;
                    float lastShareReceived = ((ClassUtility.GetCurrentDateInMilliSecond() - LastShareReceived) / 1000.0f);
                    float totalSharePerSecond = TotalGoodShareDone / timeSpendConnected;
                    float maxRangePossibility = (ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange / ClassMiningPoolGlobalStats.CurrentBlockJobMinRange) * ClassUtility.RandomOperatorCalculation.Length;
                    float maxJobPossibility = (ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange / CurrentMiningJob) * ClassUtility.RandomOperatorCalculation.Length;
                    float maxJobDoneInSecond = (maxJobPossibility / totalSharePerSecond);
                    float totalJobDone = maxJobPossibility * totalSharePerSecond;
                    float maxRangeDoneInSecond = maxRangePossibility / totalSharePerSecond;
                    float miningEffortPourcent = (maxJobDoneInSecond / maxRangeDoneInSecond) * 100;
                    float miningBestJob = (float)Math.Round((CurrentMiningJobDifficulty * totalSharePerSecond) * miningEffortPourcent, 0);

                    #region calculation comments
                    //Console.WriteLine("Current Mining Job: " + CurrentMiningJob);
                    //Console.WriteLine("Current Max Range Job: " + ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange);
                    //Console.WriteLine("Total good share done: " + TotalGoodShareDone);
                    //Console.WriteLine("Total Good Share Per Second: " + totalSharePerSecond);
                    //Console.WriteLine("Max Range Possibility: " + maxRangePossibility);
                    //Console.WriteLine("Max Job Possibility: " + maxJobPossibility);
                    //Console.WriteLine("Job total possibility done in: " + maxJobDoneInSecond + " second(s)");
                    //Console.WriteLine("Max total possibility can be done in: " + maxRangeDoneInSecond + " second(s)");
                    //Console.WriteLine("Mining Effort Difficulty pourcent: " + miningEffortPourcent + " %");
                    //Console.WriteLine("Mining Best Job Difficulty to target: " + miningBestJob);
                    #endregion

                    float estimatedHashrate = ((TotalMiningScore / timeSpendConnected) * ((ClassUtility.RandomOperatorCalculation.Length * 2)) / 2) / 1.75f; // Total math operator * 2 combinaison (normal & inverted math calculation)
                    if (estimatedHashrate != float.PositiveInfinity && estimatedHashrate != float.NegativeInfinity && !float.IsNaN(estimatedHashrate))
                    {
                        if (estimatedHashrate > 0)
                        {
                            CurrentHashrate = estimatedHashrate;
                        }
                    }

                    if (miningBestJob > ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange)
                    {
                        ClassMinerStats.InsertInvalidShareToMiner(MinerWalletAddress);
                        EndMinerConnection();
                    }
                    else
                    {
                        CurrentHashEffort = miningBestJob;

                        if (!UseCustomDifficulty || totalSharePerSecond > 5 || (miningBestJob > CurrentMiningJob * 5)) // Change the difficulty if the miner don't use a custom difficulty or send too much share per second for prevent flood and overloading.
                        {
                            UseCustomDifficulty = false;
                            if (miningBestJob * 5 < CurrentMiningJob)
                            {
                                MiningPoolSendJobAsync(miningBestJob);
                            }
                            else
                            {
                                if (miningBestJob > CurrentMiningJob * 5)
                                {
                                    MiningPoolSendJobAsync(miningBestJob);
                                }
                            }
                        }
                    }
                }
                catch
                {

                }
                await Task.Delay(1000);
            }
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
                await Task.Factory.StartNew(CheckMinerConnection, CancellationToken.None, TaskCreationOptions.DenyChildAttach, PriorityScheduler.Lowest).ConfigureAwait(false);
                await ListenMinerConnection();
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
                            while ((received = await networkReader.ReadAsync(bufferPacket.buffer, 0, bufferPacket.buffer.Length)) > 0)
                            {
                                if (!IsConnected || IsDisposed || Program.Exit)
                                {
                                    break;
                                }
                                LastPacketReceived = ClassUtility.GetCurrentDateInSecond();
                                TotalPacketReceivedPerSecond++;
                                bufferPacket.packet = Encoding.UTF8.GetString(bufferPacket.buffer, 0, received);
                                if (bufferPacket.packet.Contains(Environment.NewLine))
                                {
                                    var splitPacket = bufferPacket.packet.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                                    foreach (var packetEach in splitPacket)
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
                                        ClassLog.ConsoleWriteLog("Incoming miner connection IP " + Ip + " login packet received - Wallet Address: " + ClassUtility.RemoveSpecialCharacters(splitMinerInfo[0]) + " | Custom Difficulty: "+customDifficulty+" | Version: " + MinerVersion + ".", ClassLogEnumeration.IndexPoolMinerLog);

                                    }
                                    MinerWalletAddress = ClassUtility.RemoveSpecialCharacters(splitMinerInfo[0]);
                                    if (!ClassFilteringMiner.CheckMinerInvalidWalletAddress(MinerWalletAddress))
                                    {
                                        if (!ClassMinerStats.DictionaryMinerStats.ContainsKey(MinerWalletAddress))
                                        {
                                            if (!await ClassNetworkBlockchain.CheckWalletAddressExistAsync(MinerWalletAddress))
                                            {
                                                ClassLog.ConsoleWriteLog("Incoming miner connection IP " + Ip + " login packet received - Wallet Address: " + MinerWalletAddress + " is not valid.", ClassLogEnumeration.IndexPoolMinerErrorLog);
                                                ClassFilteringMiner.InsertInvalidPacket(Ip);
                                                ClassFilteringMiner.InsertInvalidWalletAddress(MinerWalletAddress, Ip);
                                                JObject jsonWrongLoginPacket = new JObject
                                                {
                                                    { "type", ClassMiningPoolRequest.TypeLogin },
                                                    { ClassMiningPoolRequest.TypeLoginWrong, "wrong wallet address." }
                                                };
                                                await SendPacketToMiner(jsonWrongLoginPacket.ToString(Formatting.None));
                                                EndMinerConnection();
                                                return;
                                            }
                                        }
                                        if (!ClassMinerStats.CheckMinerIsBannedByWalletAddress(MinerWalletAddress))
                                        {
                                            MinerVersion = packetJson[ClassMiningPoolRequest.SubmitVersion].ToString();
                                            IsLogged = true;
                                            LoginDate = DateTimeOffset.Now.ToUnixTimeSeconds();
                                            ClassLog.ConsoleWriteLog("Incoming miner connection IP " + Ip + " login packet received - Wallet Address: " + MinerWalletAddress + " | Version: " + MinerVersion + ".", ClassLogEnumeration.IndexPoolMinerLog);
                                            ClassMinerStats.InsertMinerTcpObject(MinerWalletAddress, this);
                                            JObject jsonLoginOkPacket = new JObject
                                            {
                                                { "type", ClassMiningPoolRequest.TypeLogin },
                                                { ClassMiningPoolRequest.TypeLoginOk, "login accepted." }
                                            };
                                            await SendPacketToMiner(jsonLoginOkPacket.ToString(Formatting.None));
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

                                        ClassLog.ConsoleWriteLog("Incoming miner connection IP " + Ip + " login packet received - Wallet Address: " + MinerWalletAddress + " | is not valid.", ClassLogEnumeration.IndexPoolMinerErrorLog);
                                        ClassFilteringMiner.InsertInvalidPacket(Ip);
                                        ClassFilteringMiner.InsertInvalidWalletAddress(MinerWalletAddress, Ip);
                                        JObject jsonWrongLoginPacket = new JObject
                                        {
                                            { "type", ClassMiningPoolRequest.TypeLogin },
                                            { ClassMiningPoolRequest.TypeLoginWrong, "wrong wallet address." }
                                        };
                                        await SendPacketToMiner(jsonWrongLoginPacket.ToString(Formatting.None));
                                        EndMinerConnection();
                                    }
                                }
                                else
                                {
                                    MinerWalletAddress = ClassUtility.RemoveSpecialCharacters(minerWalletAddressTmp);
                                    if (!ClassFilteringMiner.CheckMinerInvalidWalletAddress(MinerWalletAddress))
                                    {
                                        if (!ClassMinerStats.DictionaryMinerStats.ContainsKey(MinerWalletAddress))
                                        {
                                            if (!await ClassNetworkBlockchain.CheckWalletAddressExistAsync(MinerWalletAddress))
                                            {
                                                ClassLog.ConsoleWriteLog("Incoming miner connection IP " + Ip + " login packet received - Wallet Address: " + MinerWalletAddress + " is not valid.", ClassLogEnumeration.IndexPoolMinerErrorLog);
                                                EndMinerConnection();
                                                ClassFilteringMiner.InsertInvalidPacket(Ip);
                                                ClassFilteringMiner.InsertInvalidWalletAddress(MinerWalletAddress, Ip);
                                                JObject jsonWrongLoginPacket = new JObject
                                                {
                                                    { "type", ClassMiningPoolRequest.TypeLogin },
                                                    { ClassMiningPoolRequest.TypeLoginWrong, "wrong wallet address." }
                                                };
                                                await SendPacketToMiner(jsonWrongLoginPacket.ToString(Formatting.None));
                                                EndMinerConnection();
                                                return;
                                            }
                                        }
                                        if (!ClassMinerStats.CheckMinerIsBannedByWalletAddress(MinerWalletAddress))
                                        {
                                            MinerVersion = packetJson[ClassMiningPoolRequest.SubmitVersion].ToString();
                                            IsLogged = true;
                                            LoginDate = DateTimeOffset.Now.ToUnixTimeSeconds();
                                            ClassLog.ConsoleWriteLog("Incoming miner connection IP " + Ip + " login packet received - Wallet Address: " + MinerWalletAddress + " | Version: " + MinerVersion + ".", ClassLogEnumeration.IndexPoolMinerLog);
                                            JObject jsonLoginOkPacket = new JObject
                                            {
                                                { "type", ClassMiningPoolRequest.TypeLogin },
                                                { ClassMiningPoolRequest.TypeLoginOk, "login accepted." }
                                            };
                                            await SendPacketToMiner(jsonLoginOkPacket.ToString(Formatting.None));
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
                                        ClassFilteringMiner.InsertInvalidPacket(Ip);
                                        ClassFilteringMiner.InsertInvalidWalletAddress(MinerWalletAddress, Ip);
                                        JObject jsonWrongLoginPacket = new JObject
                                        {
                                            { "type", ClassMiningPoolRequest.TypeLogin },
                                            { ClassMiningPoolRequest.TypeLoginWrong, "wrong wallet address." }
                                        };
                                        await SendPacketToMiner(jsonWrongLoginPacket.ToString(Formatting.None));
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
                                        switch (CheckMinerShare(result, mathCalculation, share, hash, true))
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
                                                ClassLog.ConsoleWriteLog("Miner IP " + Ip + " with Wallet Address: " + MinerWalletAddress + " trusted share accepted. Job: " + CurrentMiningJobDifficulty + "/" + ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange, ClassLogEnumeration.IndexPoolMinerLog);
                                                ClassMinerStats.InsertGoodShareToMiner(MinerWalletAddress, CurrentMiningJobDifficulty);
                                                CheckShareHashWithBlockIndicationAsync(result, mathCalculation, share, hash);
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
                                        switch (CheckMinerShare(result, mathCalculation, share, hash, false))
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

                                                ClassLog.ConsoleWriteLog("Miner IP " + Ip + " with Wallet Address: " + MinerWalletAddress + " good share accepted. Job: " + CurrentMiningJobDifficulty + "/" + ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange, ClassLogEnumeration.IndexPoolMinerLog);
                                                ClassMinerStats.InsertGoodShareToMiner(MinerWalletAddress, CurrentMiningJobDifficulty);
                                                TotalGoodShare++;
                                                TotalGoodShareDone++;
                                                break;
                                        }
                                    }
                                }
                                else
                                {
                                    switch (CheckMinerShare(result, mathCalculation, share, hash, false))
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

                                            ClassLog.ConsoleWriteLog("Miner IP " + Ip + " with Wallet Address: " + MinerWalletAddress + " good share accepted. Job: " + CurrentMiningJobDifficulty + "/" + ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange, ClassLogEnumeration.IndexPoolMinerLog);
                                            ClassMinerStats.InsertGoodShareToMiner(MinerWalletAddress, CurrentMiningJobDifficulty);
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
                        if (ClassFilteringMiner.CheckMinerIsBannedByIP(Ip) || !IsLogged)
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
                if (ClassFilteringMiner.CheckMinerIsBannedByIP(Ip) || !IsLogged)
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

                    if (!ClassUtility.SocketIsConnected(TcpMiner))
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
            MinerWalletAddress = string.Empty;
            IsLogged = false;
            IsConnected = false;
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
                    using (var packetObject = new IncomingConnectionObjectSendPacket(packet + "\n"))
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
