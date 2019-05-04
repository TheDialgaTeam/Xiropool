using System;
using System.Threading;
using System.Threading.Tasks;
using Xiropht_Connector_All.RPC;
using Xiropht_Connector_All.Setting;
using Xiropht_Mining_Pool.Log;
using Xiropht_Mining_Pool.Miner;
using Xiropht_Mining_Pool.Mining;
using Xiropht_Mining_Pool.RpcWallet;
using Xiropht_Mining_Pool.Setting;
using Xiropht_Mining_Pool.Utility;

namespace Xiropht_Mining_Pool.Payment
{
    public class ClassPayment
    {
        public static bool PoolOnSendingTransaction;
        private static bool PoolOnProceedBlockReward;
        private static Thread ThreadAutoPaymentSystem;

        /// <summary>
        /// Enable auto payment system.
        /// </summary>
        public static void EnableAutoPaymentSystem()
        {
            if (ThreadAutoPaymentSystem != null && (ThreadAutoPaymentSystem.IsAlive || ThreadAutoPaymentSystem != null))
            {
                ThreadAutoPaymentSystem.Abort();
                GC.SuppressFinalize(ThreadAutoPaymentSystem);
            }

            if (MiningPoolSetting.MiningPoolIntervalPayment > 0)
            {
                if (MiningPoolSetting.MiningPoolMinimumBalancePayment > 0)
                {
                    if (MiningPoolSetting.MiningPoolFeeTransactionPayment > 0)
                    {
                        if (MiningPoolSetting.MiningPoolFeeTransactionPayment >= ClassConnectorSetting.MinimumWalletTransactionFee)
                        {
                            ThreadAutoPaymentSystem = new Thread(async delegate ()
                            {
                                while (!Program.Exit)
                                {
                                    try
                                    {
                                        if (!PoolOnProceedBlockReward)
                                        {
                                            PoolOnSendingTransaction = true;
                                            if (await ClassRpcWallet.GetCurrentBalance())
                                            {
                                                ClassLog.ConsoleWriteLog("Start to proceed payments.", ClassLogEnumeration.IndexPoolPaymentLog);


                                                if (ClassMinerStats.DictionaryMinerStats.Count > 0)
                                                {
                                                    foreach (var minerStats in ClassMinerStats.DictionaryMinerStats)
                                                    {

                                                        if (minerStats.Value.TotalBalance >= MiningPoolSetting.MiningPoolMinimumBalancePayment)
                                                        {
                                                            if (await ClassRpcWallet.GetCurrentBalance())
                                                            {
                                                                if (minerStats.Value.TotalBalance <= ClassMiningPoolGlobalStats.PoolCurrentBalance)
                                                                {
                                                                    decimal minersBalanceBase = minerStats.Value.TotalBalance;
                                                                    decimal minersBalance = minerStats.Value.TotalBalance;
                                                                    minersBalance = minersBalance - MiningPoolSetting.MiningPoolFeeTransactionPayment;
                                                                    ClassLog.ConsoleWriteLog("Attempt to send transaction of " + minersBalance + " " + ClassConnectorSetting.CoinNameMin + " to miner " + minerStats.Key, ClassLogEnumeration.IndexPoolPaymentLog);
                                                                    long dateSent = ClassUtility.GetCurrentDateInSecond();
                                                                    string resultPayment = await ClassRpcWallet.SendTransaction(minerStats.Key, minersBalance.ToString("F" + ClassConnectorSetting.MaxDecimalPlace).Replace(",", "."));
                                                                    if (resultPayment != null)
                                                                    {
                                                                        var resultPaymentSplit = resultPayment.Split(new[] { "|" }, StringSplitOptions.None);
                                                                        switch (resultPaymentSplit[0])
                                                                        {
                                                                            case ClassRpcWalletCommand.SendTokenTransactionConfirmed:
                                                                                minerStats.Value.TotalBalance -= minersBalanceBase;
                                                                                minerStats.Value.TotalPaid += minersBalanceBase;
                                                                                ClassMiningPoolGlobalStats.PoolTotalPaid += minersBalanceBase;
                                                                                ClassMinerStats.InsertTransactionPayment(minerStats.Key, resultPaymentSplit[1], minersBalance, dateSent);
                                                                                ClassLog.ConsoleWriteLog("Transaction sent to miner " + minerStats.Key + " is confirmed, transaction hash: " + resultPaymentSplit[1], ClassLogEnumeration.IndexPoolPaymentLog, ClassLogConsoleEnumeration.IndexPoolConsoleGreenLog, true);
                                                                                break;
                                                                            default:
                                                                                ClassLog.ConsoleWriteLog("Transaction sent to miner " + minerStats.Key + " is not confirmed, response received: " + resultPaymentSplit[0], ClassLogEnumeration.IndexPoolPaymentLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
                                                                                break;
                                                                        }
                                                                    }
                                                                    else
                                                                    {
                                                                        ClassLog.ConsoleWriteLog("No response from RPC Wallet to proceed payment to miner " + minerStats.Key, ClassLogEnumeration.IndexPoolPaymentErrorLog);
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    ClassLog.ConsoleWriteLog("Can't proceed payment to miner: " + minerStats.Key + ",  not enought coin on the pool wallet. | " + minerStats.Value.TotalBalance + "/" + ClassMiningPoolGlobalStats.PoolCurrentBalance, ClassLogEnumeration.IndexPoolPaymentErrorLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
                                                                }
                                                            }
                                                            else
                                                            {
                                                                ClassLog.ConsoleWriteLog("Can't proceed payment to miner: " + minerStats.Key + ", cannot get current pool balance.", ClassLogEnumeration.IndexPoolPaymentErrorLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
                                                            }
                                                        }
                                                    }
                                                }

                                                PoolOnSendingTransaction = false;

                                                ClassLog.ConsoleWriteLog("End to proceed payments.", ClassLogEnumeration.IndexPoolPaymentLog);
                                            }
                                            else
                                            {
                                                ClassLog.ConsoleWriteLog("Can't proceed payment, cannot get current pool balance.", ClassLogEnumeration.IndexPoolPaymentErrorLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
                                            }
                                        }
                                        else
                                        {
                                            ClassLog.ConsoleWriteLog("Pool currently on proceed block reward, payments will are launch after.", ClassLogEnumeration.IndexPoolPaymentLog, ClassLogConsoleEnumeration.IndexPoolConsoleYellowLog, true);
                                        }
                                    }
                                    catch(Exception error)
                                    {
                                        PoolOnSendingTransaction = false;
                                        ClassLog.ConsoleWriteLog("Proceed payments exception error: "+error.Message, ClassLogEnumeration.IndexPoolPaymentErrorLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);

                                    }
                                    Thread.Sleep(MiningPoolSetting.MiningPoolIntervalPayment * 1000);
                                }
                            });
                            ThreadAutoPaymentSystem.Start();
                        }
                        else
                        {
                            ClassLog.ConsoleWriteLog("Warning, the transaction " + ClassConnectorSetting.CoinNameMin + " fee is less than the minimum accepted of: " + ClassConnectorSetting.MinimumWalletTransactionFee + ", the payment system will not be enabled.", ClassLogEnumeration.IndexPoolPaymentErrorLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
                        }
                    }
                    else
                    {
                        ClassLog.ConsoleWriteLog("Warning, the transaction " + ClassConnectorSetting.CoinNameMin + " fee is less or equals of 0, the payment system will not be enabled.", ClassLogEnumeration.IndexPoolPaymentErrorLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
                    }
                }
                else
                {
                    ClassLog.ConsoleWriteLog("Warning, the minimum of "+ClassConnectorSetting.CoinNameMin+" to reach for proceed payment, is less or equals of 0, the payment system will not be enabled.", ClassLogEnumeration.IndexPoolPaymentErrorLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
                }
            }
            else
            {
                ClassLog.ConsoleWriteLog("Warning, the interval of payment is less or equals of 0 second, the payment system will not be enabled.", ClassLogEnumeration.IndexPoolPaymentErrorLog, ClassLogConsoleEnumeration.IndexPoolConsoleRedLog, true);
            }
        }

        /// <summary>
        /// Stop auto payment system.
        /// </summary>
        /// <returns></returns>
        public static void StopAutoPaymentSystem()
        {
            if (ThreadAutoPaymentSystem != null && (ThreadAutoPaymentSystem.IsAlive || ThreadAutoPaymentSystem != null))
            {
                ThreadAutoPaymentSystem.Abort();
                GC.SuppressFinalize(ThreadAutoPaymentSystem);
            }
        }

        /// <summary>
        /// Proceed mining score done by miners
        /// </summary>
        public static async void ProceedMiningScoreRewardAsync(string blockId)
        {
            
            while(PoolOnProceedBlockReward)
            {
                await Task.Delay(100);
                ClassLog.ConsoleWriteLog("Waiting proceed previous block reward before to proceed reward block id: " + blockId + "..", ClassLogEnumeration.IndexPoolPaymentLog, ClassLogConsoleEnumeration.IndexPoolConsoleYellowLog, true);
                if (Program.Exit)
                {
                    break;
                }
            }

            PoolOnProceedBlockReward = true;
            while (PoolOnSendingTransaction)
            {
                await Task.Delay(100);
                ClassLog.ConsoleWriteLog("Waiting end of proceed payments before to proceed reward block id: "+blockId+"..", ClassLogEnumeration.IndexPoolPaymentLog, ClassLogConsoleEnumeration.IndexPoolConsoleYellowLog, true);
                if (Program.Exit)
                {
                    break;
                }
            }
            ClassLog.ConsoleWriteLog("Proceed block reward from block found: " + blockId, ClassLogEnumeration.IndexPoolPaymentLog, ClassLogConsoleEnumeration.IndexPoolConsoleYellowLog, true);
            decimal blockReward = ClassConnectorSetting.ConstantBlockReward;
            if (MiningPoolSetting.MiningPoolFee > 0)
            {
                decimal poolFeeAmount = (blockReward * MiningPoolSetting.MiningPoolFee) / 100;
                blockReward -= poolFeeAmount;
                ClassLog.ConsoleWriteLog("Pool fee of "+MiningPoolSetting.MiningPoolFee+"% take "+poolFeeAmount+" " +ClassConnectorSetting.CoinNameMin+" from total block reward of " + ClassConnectorSetting.ConstantBlockReward + " " + ClassConnectorSetting.CoinName, ClassLogEnumeration.IndexPoolPaymentLog);
            }
            ClassLog.ConsoleWriteLog("Proceed block reward from Block ID: " + blockId + " with a block reward calculated with pool of "+MiningPoolSetting.MiningPoolFee+"% -> result: " +blockReward + " " + ClassConnectorSetting.CoinName, ClassLogEnumeration.IndexPoolPaymentLog);
            await Task.Delay(1000);
            decimal totalMiningScore = 0;
            foreach (var minerStats in ClassMinerStats.DictionaryMinerStats)
            {
                if (minerStats.Value.TotalMiningScore > 0)
                {
                    totalMiningScore += minerStats.Value.TotalMiningScore;
                }
            }

            ClassLog.ConsoleWriteLog("Total mining score done by miners on Block ID: " + blockId+ " " +totalMiningScore, ClassLogEnumeration.IndexPoolPaymentLog);

            foreach (var minerStats in ClassMinerStats.DictionaryMinerStats)
            {
                if (minerStats.Value.TotalMiningScore > 0)
                {
                    decimal pourcentageOfReward = (minerStats.Value.TotalMiningScore / totalMiningScore) * 100;
                    decimal minerReward = ((blockReward * pourcentageOfReward) / 100);
                    ClassLog.ConsoleWriteLog("Miner "+minerStats.Key+" receive "+pourcentageOfReward+"%  of block ID: " + blockId + " amount: " + minerReward + " "+ClassConnectorSetting.CoinNameMin, ClassLogEnumeration.IndexPoolPaymentLog);
                    minerStats.Value.TotalBalance += minerReward;
                    ClassMinerStats.DictionaryMinerStats[minerStats.Key].TotalMiningScore = 0;
                }
            }
            PoolOnProceedBlockReward = false;
        }
    }
}
