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
        public decimal MiningPoolDifficultyStart;
        private TcpListener TcpListenerMiningPool;
        private Thread ThreadMiningPool;
        public bool MiningPoolStatus;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="miningPoolPort"></param>
        public ClassMiningPool(int miningPoolPort, decimal miningPoolDifficultyStart)
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
        private TcpClient TcpMiner;
        public bool IsConnected; // Indicate the status of the connection of the mining instance.
        public bool IsLogged; // Indicate if the mining instance is successfully logged.
        private long LastPacketReceived; // Last packet received on the mining instance.
        private string Ip; // IP of the mining instance.
        public long LoginDate; // Login date of the mining instance.


        /// <summary>
        /// Miner setting informations.
        /// </summary>
        public string MinerWalletAddress; // Miner wallet address of the instance.
        public bool UseCustomDifficulty; // Indicate if the miner use a custom difficulty.
        private int MiningPoolPort; // Mining pool port selected by the miner on the mining instance.
        private decimal MiningPoolPortDifficulty; // Mining pool port difficulty start.
        private string MinerVersion; // Miner version used on the mining instance.

        /// <summary>
        /// Miner stats informations.
        /// </summary>
        public long TotalGoodShareDone; // Total good share done on the mining instance.
        private decimal LastShareDifficultyDone;
        public decimal TotalMiningScore; // Total mining score obtain on the mining instance.
        public decimal TotalMiningScoreExpected; // Total mining score expected on the mining instance.
        public long TotalShareReceived; // Total share received on the mining instance.
        public long LastShareReceived; // Last share received on the mining instance.
        private long TotalPacketReceivedPerSecond; // Total packet received per second.
        private long LastJobDateReceive; // Last job date received.
        public decimal CurrentHashrate; // Current hashrate of the mining instance.

        /// <summary>
        /// Miner jobs informations.
        /// </summary>
        private decimal MiningDifficultyStart; // Difficulty selected at the start, from port or by custom difficulty.
        public decimal CurrentMiningJobDifficulty; // Current mining job difficulty.
        private string CurrentBlockHashOnMining; // Current block hash of the current blocktemplate.
        public decimal CurrentHashEffort; // Current hash efforts provided by the miner.

        /// <summary>
        /// Current job informations.
        /// </summary>
        private decimal CurrentMinRangeJob; // Current min range selected by the pool.
        private decimal CurrentMaxRangeJob; // Current max range selected by the pool.
        private string CurrentShareIndicationHashJob;


        /// <summary>
        /// Dictionary of share provided by the miner saved.
        /// </summary>
        private Dictionary<string, string> ListOfShare;
        private Dictionary<string, Tuple<string, string>> ListOfShareToFound;


        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="tcpMiner"></param>

        public MinerTcpObject(TcpClient tcpMiner, int miningPoolPort)
        {
            TcpMiner = tcpMiner;
            MinerWalletAddress = string.Empty;
            MiningPoolPort = miningPoolPort;
            ListOfShare = new Dictionary<string, string>();
            ListOfShareToFound = new Dictionary<string, Tuple<string, string>>();
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
        public async void MiningPoolSendJobAsync(decimal jobTarget = 0)
        {
            CurrentBlockHashOnMining = ClassMiningPoolGlobalStats.CurrentBlockHash;
            decimal minRange = ClassMiningPoolGlobalStats.CurrentBlockJobMinRange;
            decimal maxRange = ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange;

            ListOfShare.Clear();
            ListOfShareToFound.Clear();

            #region check mining job difficulty
            if (jobTarget < MiningPoolPortDifficulty)
            {
                jobTarget = MiningPoolPortDifficulty;
            }
            if (MiningDifficultyStart < MiningPoolPortDifficulty)
            {
                MiningDifficultyStart = MiningPoolPortDifficulty;
            }

            if (MiningDifficultyStart > maxRange)
            {
                MiningDifficultyStart = maxRange;
            }
            if (MiningDifficultyStart <= minRange)
            {
                MiningDifficultyStart = minRange + 1;
            }
            if (jobTarget > maxRange)
            {
                jobTarget = maxRange;
            }
            if (jobTarget <= minRange)
            {
                jobTarget = minRange + 1;
            }
            if (CurrentHashEffort > maxRange)
            {
                CurrentHashEffort = maxRange;
            }

            #endregion
            if (!UseCustomDifficulty)
            {
                if (jobTarget == 0)
                {
                    if (jobTarget <= maxRange)
                    {
                        CurrentMinRangeJob = minRange;
                        CurrentMaxRangeJob = jobTarget;
                    }
                    else
                    {
                        CurrentMinRangeJob = minRange;
                        CurrentMaxRangeJob = ClassUtility.GetRandomBetweenJob(minRange, maxRange);
                    }
                }
                else
                {
                    CurrentMinRangeJob = minRange;
                    CurrentMaxRangeJob = jobTarget;
                }
            }
            else
            {
                if (MiningDifficultyStart > minRange && MiningDifficultyStart <= maxRange)
                {
                    CurrentMinRangeJob = minRange;
                    CurrentMaxRangeJob = MiningDifficultyStart;
                }
                else
                {
                    CurrentMinRangeJob = minRange;
                    CurrentMaxRangeJob = maxRange;
                }
            }
            LastJobDateReceive = ClassUtility.GetCurrentDateInSecond();
            LastShareReceived = ClassUtility.GetCurrentDateInMilliSecond();
            CurrentMiningJobDifficulty = Math.Round(CurrentMaxRangeJob, 0);
            CurrentMinRangeJob = Math.Round(CurrentMinRangeJob, 0);
            CurrentMaxRangeJob = Math.Round(CurrentMaxRangeJob, 0);
            await GenerateJobAsync(CurrentMinRangeJob, CurrentMaxRangeJob);

            while(CurrentShareIndicationHashJob == null)
            {
                await Task.Delay(100);
            }
            string compressedMiningShareIndicationHashJob = ClassUtils.CompressData(CurrentShareIndicationHashJob);

            JObject jobRequest = new JObject
            {
                ["type"] = ClassMiningPoolRequest.TypeJob,
                [ClassMiningPoolRequest.TypeBlock] = ClassMiningPoolGlobalStats.CurrentBlockId,
                [ClassMiningPoolRequest.TypeBlockTimestampCreate] = ClassMiningPoolGlobalStats.CurrentBlockTimestampCreate,
                [ClassMiningPoolRequest.TypeBlockKey] = ClassMiningPoolGlobalStats.CurrentBlockKey,
                [ClassMiningPoolRequest.TypeJobIndication] = compressedMiningShareIndicationHashJob,
                [ClassMiningPoolRequest.TypeJobDifficulty] = CurrentMiningJobDifficulty,
                [ClassMiningPoolRequest.TypeMinRange] = CurrentMinRangeJob,
                [ClassMiningPoolRequest.TypeMaxRange] = CurrentMaxRangeJob,
                [ClassMiningPoolRequest.TypeJobMiningMethodName] = ClassMiningPoolGlobalStats.CurrentBlockMethod,
                [ClassMiningPoolRequest.TypeBlockIndication] = ClassMiningPoolGlobalStats.CurrentBlockIndication,
                [ClassMiningPoolRequest.TypeBlockDifficulty] = ClassMiningPoolGlobalStats.CurrentBlockDifficulty,
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
        /// Calculate Hashrate;
        /// </summary>
        private async Task CalculateHashrateAsync()
        {
            while (IsConnected && IsLogged)
            {

                if (TotalMiningScore > 0)
                {
                    decimal timeSpendConnected = ClassUtility.GetCurrentDateInSecond() - LoginDate;
                    decimal lastShareReceived = ClassUtility.GetCurrentDateInSecond() - (LastShareReceived / 1000.0m);
                    timeSpendConnected -= lastShareReceived;
                    if (timeSpendConnected <= 0)
                    {
                        timeSpendConnected = 1;
                    }
                    decimal tmpCurrentHashrate = ((TotalMiningScore / timeSpendConnected) * ((ClassUtility.RandomOperatorCalculation.Length * 2)));

                    if (tmpCurrentHashrate < 0)
                    {
                        tmpCurrentHashrate = 0;
                    }
                    else
                    {
                        CurrentHashrate = tmpCurrentHashrate;
                    }

                }



                await Task.Delay(1000);
            }
        }

        /// <summary>
        /// Generate a math calculation possibility.
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        private async Task GenerateJobAsync(decimal min, decimal max)
        {
            bool jobGenerated = false;
#if DEBUG
            Console.WriteLine("Job max range selected:" + max);
            Console.WriteLine("Job min range selected:" + min);
#endif

            int totalRandomJobToFound = ClassUtility.GetRandomBetween(2, 8);
            string currentMiningJobHashIndication = string.Empty;
            while (!jobGenerated)
            {
                if(!IsConnected || !IsLogged)
                {
                    break;
                }
                
                string calculation = string.Empty;
                string randomMathOperator = string.Empty;
                decimal resultCalculation = 0;
                string firstNumber = "0";
                string secondNumber = "0";

                while (decimal.Parse(firstNumber) < min || decimal.Parse(firstNumber) > max || decimal.Parse(secondNumber) < min|| decimal.Parse(secondNumber) > max)
                {


                    firstNumber = ClassUtility.GenerateNumberMathCalculation(min, max);
                    secondNumber = ClassUtility.GenerateNumberMathCalculation(min, max);

                    randomMathOperator = ClassUtility.RandomOperatorCalculation[ClassUtility.GetRandomBetween(0, ClassUtility.RandomOperatorCalculation.Length - 1)];
                    resultCalculation = ClassUtility.ComputeCalculation(firstNumber, randomMathOperator, secondNumber);
                    if (resultCalculation >= min && resultCalculation <= ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange)
                    {
                        if (resultCalculation - Math.Round(resultCalculation, 0) != 0)
                        {
                            firstNumber = "0";
                            secondNumber = "0";
                        }
                    }
                    else
                    {
                        var tmpFirstNumber = firstNumber;
                        var tmpSecondNumber = secondNumber;

                        resultCalculation = ClassUtility.ComputeCalculation(secondNumber, randomMathOperator, firstNumber);
                        secondNumber = tmpFirstNumber;
                        firstNumber = tmpSecondNumber;
                        if (resultCalculation >= min && resultCalculation <= ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange)
                        {
                            if (resultCalculation - Math.Round(resultCalculation, 0) != 0)
                            {
                                firstNumber = "0";
                                secondNumber = "0";
                            }
                        }
                    }
                }
            
                

                calculation = firstNumber + " " + randomMathOperator + " " + secondNumber;
                string encryptedShare = ClassUtility.StringToHexString(calculation + ClassMiningPoolGlobalStats.CurrentBlockTimestampCreate);

                encryptedShare = ClassUtility.EncryptXorShare(encryptedShare, ClassMiningPoolGlobalStats.CurrentRoundXorKey.ToString());
                for (int i = 0; i < ClassMiningPoolGlobalStats.CurrentRoundAesRound; i++)
                {
                    encryptedShare = ClassUtility.EncryptAesShareAsync(encryptedShare, ClassMiningPoolGlobalStats.CurrentBlockKey, Encoding.UTF8.GetBytes(ClassMiningPoolGlobalStats.CurrentRoundAesKey), ClassMiningPoolGlobalStats.CurrentRoundAesSize);
                }
                encryptedShare = ClassUtility.EncryptXorShare(encryptedShare, ClassMiningPoolGlobalStats.CurrentRoundXorKey.ToString());
                encryptedShare = ClassUtility.EncryptAesShareAsync(encryptedShare, ClassMiningPoolGlobalStats.CurrentBlockKey, Encoding.UTF8.GetBytes(ClassMiningPoolGlobalStats.CurrentRoundAesKey), ClassMiningPoolGlobalStats.CurrentRoundAesSize);
                encryptedShare = ClassUtility.GenerateSHA512(encryptedShare);
                if (!ListOfShareToFound.ContainsKey(encryptedShare))
                {
                    string shareHashIndication = ClassUtility.GenerateSHA512(encryptedShare);
                    if (shareHashIndication == ClassMiningPoolGlobalStats.CurrentBlockIndication)
                    {
                        ClassLog.ConsoleWriteLog("Lol ! the pool seems to have found the block " + ClassMiningPoolGlobalStats.CurrentBlockId + " himself, waiting confirmation.", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleYellowLog, true);
                        await Task.Factory.StartNew(() => ClassNetworkBlockchain.SendPacketBlockFound(encryptedShare, resultCalculation, calculation, shareHashIndication), CancellationToken.None, TaskCreationOptions.RunContinuationsAsynchronously, PriorityScheduler.AboveNormal).ConfigureAwait(false);
                    }
                    else
                    {
                        if (ListOfShareToFound.Count >= totalRandomJobToFound)
                        {
                            jobGenerated = true;
                            bool blockHashIndicationPut = false;

                            foreach (var shareToFound in ListOfShareToFound)
                            {
                                if (ClassUtils.GetRandomBetween(1, 100) >= 70)
                                {
                                    if (!blockHashIndicationPut)
                                    {
                                        blockHashIndicationPut = true;
                                        currentMiningJobHashIndication += ClassMiningPoolGlobalStats.CurrentBlockIndication + shareToFound.Value.Item1;
                                    }
                                }
                                else
                                {
                                    currentMiningJobHashIndication += shareToFound.Value.Item1;
                                }
                            }
                            if (!blockHashIndicationPut)
                            {
                                currentMiningJobHashIndication += ClassMiningPoolGlobalStats.CurrentBlockIndication;
                            }
                            CurrentShareIndicationHashJob = currentMiningJobHashIndication;
                            break;
                        }
                        else
                        {
                            ListOfShareToFound.Add(encryptedShare, new Tuple<string, string>(shareHashIndication, calculation));
                        }
                    }
                }
            }
#if DEBUG
            Console.WriteLine("Job generated");
#endif
        }

        /// <summary>
        /// Check share received.
        /// </summary>
        /// <param name="result"></param>
        /// <param name="mathCalculation"></param>
        /// <param name="share"></param>
        /// <param name="hash"></param>
        /// <returns></returns>
        private Tuple<string, decimal> CheckMinerShare(decimal result, string mathCalculation, string share, string hash, bool trustedShare)
        {
            if (ListOfShareToFound.Count == 0)
            {
                return new Tuple<string, decimal>(ClassMiningPoolRequest.TypeResultShareDuplicate, CurrentMiningJobDifficulty);
            }
            TotalShareReceived++;
            if (hash != ClassMiningPoolGlobalStats.CurrentBlockIndication)
            {
                if (hash.Length != ClassMiningPoolGlobalStats.CurrentBlockIndication.Length)
                {
                    return new Tuple<string, decimal>(ClassMiningPoolRequest.TypeResultShareInvalid, CurrentMiningJobDifficulty);
                }
                if (ListOfShare.ContainsKey(share))
                {
                    return new Tuple<string, decimal>(ClassMiningPoolRequest.TypeResultShareDuplicate, CurrentMiningJobDifficulty);
                }
                if (ListOfShare.ContainsValue(hash))
                {
                    return new Tuple<string, decimal>(ClassMiningPoolRequest.TypeResultShareDuplicate, CurrentMiningJobDifficulty);
                }
                bool existShareHashIndication = false;
                foreach(var shareIndication in ListOfShareToFound)
                {
                    if (shareIndication.Value.Item1 == hash)
                    {
                        existShareHashIndication = true;
                    }
                }
                if (CurrentShareIndicationHashJob.Contains(hash))
                {
                    existShareHashIndication = true;
                }
                if (!existShareHashIndication)
                {
                    return new Tuple<string, decimal>(ClassMiningPoolRequest.TypeResultShareInvalid, CurrentMiningJobDifficulty);
                }
            }

            var splitMathCalculation = mathCalculation.Split(new[] { " " }, StringSplitOptions.None);
            if (decimal.Parse(splitMathCalculation[0]) >= ClassMiningPoolGlobalStats.CurrentBlockJobMinRange && decimal.Parse(splitMathCalculation[0]) <= ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange)
            {
                if (decimal.Parse(splitMathCalculation[2]) >= ClassMiningPoolGlobalStats.CurrentBlockJobMinRange && decimal.Parse(splitMathCalculation[2]) <= ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange)
                {
                    if (result > ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange)
                    {
                        return new Tuple<string, decimal>(ClassMiningPoolRequest.TypeResultShareInvalid, CurrentMiningJobDifficulty);
                    }
                    if (result < ClassMiningPoolGlobalStats.CurrentBlockJobMinRange)
                    {
                        return new Tuple<string, decimal>(ClassMiningPoolRequest.TypeResultShareInvalid, CurrentMiningJobDifficulty);
                    }

                    if (ClassUtility.ComputeCalculation(splitMathCalculation[0], splitMathCalculation[1], splitMathCalculation[2]) != result)
                    {
                        return new Tuple<string, decimal>(ClassMiningPoolRequest.TypeResultShareInvalid, CurrentMiningJobDifficulty);
                    }


                    byte[] reverseShare = Convert.FromBase64String(share);

                    if (reverseShare.Length != 96)
                    {
                        return new Tuple<string, decimal>(ClassMiningPoolRequest.TypeResultShareInvalid, CurrentMiningJobDifficulty);
                    }

                    byte[] reverseHash = Convert.FromBase64String(hash);

                    if (reverseShare.Length != 96)
                    {
                        return new Tuple<string, decimal>(ClassMiningPoolRequest.TypeResultShareInvalid, CurrentMiningJobDifficulty);
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
                            return new Tuple<string, decimal>(ClassMiningPoolRequest.TypeResultShareInvalid, CurrentMiningJobDifficulty);
                        }

                        string hashShare = ClassUtility.GenerateSHA512(encryptedShare);

                        if (hashShare != hash)
                        {
                            return new Tuple<string, decimal>(ClassMiningPoolRequest.TypeResultShareInvalid, CurrentMiningJobDifficulty);
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
                            ListOfShare.Add(share, hash);
                        }
                        else
                        {
                            return new Tuple<string, decimal>(ClassMiningPoolRequest.TypeResultShareDuplicate, CurrentMiningJobDifficulty);
                        }
                    }
                    catch
                    {
                        return new Tuple<string, decimal>(ClassMiningPoolRequest.TypeResultShareDuplicate, CurrentMiningJobDifficulty);
                    }
                    LastShareReceived = ClassUtility.GetCurrentDateInMilliSecond();
                    decimal difference = (LastShareReceived / 1000.0m) - LastJobDateReceive;
                    decimal timespendOnShare = ClassUtility.GetCurrentDateInSecond() - LastJobDateReceive;
                    LastShareDifficultyDone = CurrentMiningJobDifficulty;

                    if (difference >= 1)
                    {
                        decimal factorScore = (CurrentMiningJobDifficulty * difference) / 100;
                        decimal miningScore = Math.Round((CurrentMiningJobDifficulty - factorScore), 0);
                        decimal newDifficulty = Math.Round(CurrentMiningJobDifficulty + ((miningScore / CurrentMiningJobDifficulty) * 100), 0);
                        decimal totalPossibility = ((CurrentMiningJobDifficulty / CurrentMinRangeJob) * ClassUtility.RandomOperatorCalculation.Length);

                        if (miningScore <= CurrentMiningJobDifficulty && miningScore > 0)
                        {

                            TotalMiningScore += Math.Round(miningScore, 0);

                        }
                        if (newDifficulty >= CurrentMiningJobDifficulty * 4)
                        {
                            UseCustomDifficulty = false;
                        }
                        return new Tuple<string, decimal>(ClassMiningPoolRequest.TypeResultShareOk, newDifficulty);
                    }
                    else
                    {
                        decimal miningScore = Math.Round((CurrentMiningJobDifficulty * difference), 0);
                        decimal newDifficulty = Math.Round((CurrentMiningJobDifficulty / difference), 0);

                        if (miningScore <= CurrentMiningJobDifficulty && miningScore > 0)
                        {
                            TotalMiningScore += Math.Round(miningScore, 0);
                        }
                        if (newDifficulty >= CurrentMiningJobDifficulty * 4)
                        {
                            UseCustomDifficulty = false;
                        }
                        return new Tuple<string, decimal>(ClassMiningPoolRequest.TypeResultShareOk, newDifficulty);
                    }

                }
                else
                {
                    ClassLog.ConsoleWriteLog("Miner IP " + Ip + " with Wallet Address: " + MinerWalletAddress + " bad number used: " + splitMathCalculation[2], ClassLogEnumeration.IndexPoolMinerErrorLog);
                    return new Tuple<string, decimal>(ClassMiningPoolRequest.TypeResultShareLowDifficulty, CurrentMiningJobDifficulty);
                }
            }
            else
            {
                ClassLog.ConsoleWriteLog("Miner IP " + Ip + " with Wallet Address: " + MinerWalletAddress + " bad number used: " + splitMathCalculation[0], ClassLogEnumeration.IndexPoolMinerErrorLog);
                return new Tuple<string, decimal>(ClassMiningPoolRequest.TypeResultShareLowDifficulty, CurrentMiningJobDifficulty);
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
        private async void CheckShareHashWithBlockIndicationAsync(decimal result, string mathCalculation, string share, string hash)
        {
            ClassLog.ConsoleWriteLog("Miner IP " + Ip + " with Wallet Address: " + MinerWalletAddress + " seems to have found the block " + ClassMiningPoolGlobalStats.CurrentBlockId + ", waiting confirmation.", ClassLogEnumeration.IndexPoolGeneralLog, ClassLogConsoleEnumeration.IndexPoolConsoleYellowLog, true);
            await Task.Factory.StartNew(() => ClassNetworkBlockchain.SendPacketBlockFound(share, result, mathCalculation, hash), CancellationToken.None, TaskCreationOptions.RunContinuationsAsynchronously, PriorityScheduler.AboveNormal).ConfigureAwait(false);
        }


#endregion

        #region Network Functions

        /// <summary>
        /// Handle incoming miner, check if his ip is banned.
        /// </summary>
        /// <returns></returns>
        public async Task HandleIncomingMiner(int miningPoolPort, decimal difficultyStart)
        {
            MiningDifficultyStart = difficultyStart;
            Ip = ((IPEndPoint)(TcpMiner.Client.RemoteEndPoint)).Address.ToString();
            ClassLog.ConsoleWriteLog("Incoming miner connection IP " + Ip + " on mining pool port: " + miningPoolPort + " with difficulty start of: "+MiningDifficultyStart, ClassLogEnumeration.IndexPoolMinerLog);

            if (!ClassFilteringMiner.CheckMinerIsBannedByIP(Ip) && !string.IsNullOrEmpty(ClassMiningPoolGlobalStats.CurrentBlockId)) // The miner IP is not banned.
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
                                            LastShareReceived = ClassUtility.GetCurrentDateInMilliSecond();
                                            await Task.Factory.StartNew(() => CalculateHashrateAsync(), CancellationToken.None, TaskCreationOptions.DenyChildAttach, PriorityScheduler.Lowest).ConfigureAwait(false);
                                            TotalGoodShareDone++;
                                            if (ListOfShare.Count >= ListOfShareToFound.Count)
                                            {
                                                await Task.Factory.StartNew(() => MiningPoolSendJobAsync(MiningDifficultyStart), CancellationToken.None, TaskCreationOptions.DenyChildAttach, PriorityScheduler.Lowest).ConfigureAwait(false);
                                            }
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
                                            LastShareReceived = ClassUtility.GetCurrentDateInMilliSecond();
                                            await Task.Factory.StartNew(() => CalculateHashrateAsync(), CancellationToken.None, TaskCreationOptions.DenyChildAttach, PriorityScheduler.Lowest).ConfigureAwait(false);
                                            TotalGoodShareDone++;
                                            if (ListOfShare.Count >= ListOfShareToFound.Count)
                                            {
                                                await Task.Factory.StartNew(() => MiningPoolSendJobAsync(MiningDifficultyStart), CancellationToken.None, TaskCreationOptions.DenyChildAttach, PriorityScheduler.Lowest).ConfigureAwait(false);
                                            }
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
                                decimal result = decimal.Parse(packetJson[ClassMiningPoolRequest.SubmitResult].ToString());
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
                                        var checkMinerShareResult = CheckMinerShare(result, mathCalculation, share, hash, true);
                                        switch (checkMinerShareResult.Item1)
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
                                                TotalGoodShareDone++;

                                                if (ListOfShare.Count >= ListOfShareToFound.Count)
                                                {
                                                    await Task.Factory.StartNew(() => MiningPoolSendJobAsync(checkMinerShareResult.Item2), CancellationToken.None, TaskCreationOptions.DenyChildAttach, PriorityScheduler.Lowest).ConfigureAwait(false);
                                                }

                                                break;
                                        }
  
                                    }
                                    else
                                    {
                                        var checkMinerShareResult = CheckMinerShare(result, mathCalculation, share, hash, false);
                                        switch (checkMinerShareResult.Item1)
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
                                                TotalGoodShareDone++;
                                                if (ListOfShare.Count >= ListOfShareToFound.Count)
                                                {
                                                    await Task.Factory.StartNew(() => MiningPoolSendJobAsync(checkMinerShareResult.Item2), CancellationToken.None, TaskCreationOptions.DenyChildAttach, PriorityScheduler.Lowest).ConfigureAwait(false);
                                                }
                                                break;
                                        }
                                    }
                                }
                                else
                                {
                                    var checkMinerShareResult = CheckMinerShare(result, mathCalculation, share, hash, false);
                                    switch (checkMinerShareResult.Item1)
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

                                            ClassLog.ConsoleWriteLog("Miner IP " + Ip + " with Wallet Address: " + MinerWalletAddress + " good share accepted. Job: " + CurrentMiningJobDifficulty + "/" + ClassMiningPoolGlobalStats.CurrentBlockJobMaxRange + " share received:"+packetReceived, ClassLogEnumeration.IndexPoolMinerLog);
                                            ClassMinerStats.InsertGoodShareToMiner(MinerWalletAddress, CurrentMiningJobDifficulty);
                                            TotalGoodShareDone++;
                                            await Task.Factory.StartNew(() => MiningPoolSendJobAsync(checkMinerShareResult.Item2), CancellationToken.None, TaskCreationOptions.DenyChildAttach, PriorityScheduler.Lowest).ConfigureAwait(false);
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
                        if (LastShareReceived + (MiningPoolSetting.MiningPoolTimeout * 1000.0m) < ClassUtility.GetCurrentDateInMilliSecond())
                        {
                            IsConnected = false;
                            break;
                        }
                        if (LastShareReceived + (MiningPoolSetting.MiningPoolIntervalChangeJob * 1000.0m) < ClassUtility.GetCurrentDateInMilliSecond())
                        {
                            var timeSpendWithoutShare = ClassUtility.GetCurrentDateInSecond() - (LastShareReceived / 1000m);
                            if (timeSpendWithoutShare >= 1)
                            {
                                decimal tmpcurrentHashrate = CurrentHashrate - ((CurrentHashrate * timeSpendWithoutShare) / 100);
                                decimal tmpcurrentMiningJob = CurrentMiningJobDifficulty - ((CurrentMiningJobDifficulty * timeSpendWithoutShare) / 100);
                                if (tmpcurrentMiningJob < CurrentMiningJobDifficulty && tmpcurrentHashrate > 0)
                                {
                                    await Task.Factory.StartNew(() => MiningPoolSendJobAsync(tmpcurrentMiningJob), CancellationToken.None, TaskCreationOptions.DenyChildAttach, PriorityScheduler.Lowest).ConfigureAwait(false);
                                }
                            }
                        }

                        if (CurrentBlockHashOnMining != ClassMiningPoolGlobalStats.CurrentBlockHash || CurrentMiningJobDifficulty == 0)
                        {
                            await Task.Factory.StartNew(() => MiningPoolSendJobAsync(CurrentMiningJobDifficulty), CancellationToken.None, TaskCreationOptions.DenyChildAttach, PriorityScheduler.Lowest).ConfigureAwait(false);
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
