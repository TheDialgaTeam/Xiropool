﻿using System;
using System.Threading;
using Xiropht_Connector_All.RPC;
using Xiropht_Connector_All.Setting;
using Xiropht_Mining_Pool.Log;
using Xiropht_Mining_Pool.Miner;
using Xiropht_Mining_Pool.RpcWallet;
using Xiropht_Mining_Pool.Setting;
using Xiropht_Mining_Pool.Utility;

namespace Xiropht_Mining_Pool.Payment
{
    public class ClassPayment
    {
        public static bool PoolOnSendingTransaction;
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
                                        PoolOnSendingTransaction = true;
                                        await ClassRpcWallet.GetCurrentBalance();
                                        ClassLog.ConsoleWriteLog("Start to proceed payments.", ClassLogEnumeration.IndexPoolPaymentLog);


                                        if (ClassMinerStats.DictionaryMinerStats.Count > 0)
                                        {
                                            foreach (var minerStats in ClassMinerStats.DictionaryMinerStats)
                                            {

                                                if (minerStats.Value.TotalBalance >= MiningPoolSetting.MiningPoolMinimumBalancePayment)
                                                {
                                                    decimal minersBalanceBase = minerStats.Value.TotalBalance;
                                                    decimal minersBalance = minerStats.Value.TotalBalance;
                                                    minersBalance = minersBalance - MiningPoolSetting.MiningPoolFeeTransactionPayment;
                                                    ClassLog.ConsoleWriteLog("Attempt to send transaction of " + minersBalance + " " + ClassConnectorSetting.CoinNameMin + " to miner " + minerStats.Key, ClassLogEnumeration.IndexPoolPaymentLog);
                                                    long dateSent = ClassUtility.GetCurrentDateInSecond();
                                                    string resultPayment = await ClassRpcWallet.SendTransaction(minerStats.Key, minersBalance.ToString("F"+ClassConnectorSetting.MaxDecimalPlace).Replace(",", "."));
                                                    if (resultPayment != null)
                                                    {
                                                        var resultPaymentSplit = resultPayment.Split(new[] { "|" }, StringSplitOptions.None);
                                                        switch (resultPaymentSplit[0])
                                                        {
                                                            case ClassRpcWalletCommand.SendTokenTransactionConfirmed:
                                                                minerStats.Value.TotalBalance -= minersBalanceBase;
                                                                minerStats.Value.TotalPaid += minersBalanceBase;
                                                                ClassMinerStats.InsertTransactionPayment(minerStats.Key, resultPaymentSplit[1], minersBalance, dateSent);
                                                                ClassLog.ConsoleWriteLog("Transaction sent to miner " + minerStats.Key + " is confirmed, transaction hash: " + resultPaymentSplit[1], ClassLogEnumeration.IndexPoolPaymentLog);
                                                                break;
                                                            default:
                                                                ClassLog.ConsoleWriteLog("Transaction sent to miner " + minerStats.Key + " is not confirmed, response received: " + resultPaymentSplit[0], ClassLogEnumeration.IndexPoolPaymentLog);
                                                                break;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        ClassLog.ConsoleWriteLog("No response from RPC Wallet to proceed payment to miner " + minerStats.Key, ClassLogEnumeration.IndexPoolPaymentErrorLog);
                                                    }
                                                }
                                            }
                                        }
                                        
                                        PoolOnSendingTransaction = false;

                                        ClassLog.ConsoleWriteLog("End to proceed payments.", ClassLogEnumeration.IndexPoolPaymentLog);
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
        public static void ProceedMiningScoreReward(string blockId)
        {
            decimal blockReward = ClassConnectorSetting.ConstantBlockReward;
            if (MiningPoolSetting.MiningPoolFee > 0)
            {
                decimal poolFeeAmount = (blockReward * MiningPoolSetting.MiningPoolFee) / 100;
                blockReward -= poolFeeAmount;
                ClassLog.ConsoleWriteLog("Pool fee of "+MiningPoolSetting.MiningPoolFee+"% take "+poolFeeAmount+" " +ClassConnectorSetting.CoinNameMin+" from total block reward of " + ClassConnectorSetting.ConstantBlockReward + " " + ClassConnectorSetting.CoinName, ClassLogEnumeration.IndexPoolPaymentLog);
            }
            ClassLog.ConsoleWriteLog("Proceed block reward from Block ID: " + blockId + " with a reward of: " + ClassConnectorSetting.ConstantBlockReward + " " + ClassConnectorSetting.CoinName, ClassLogEnumeration.IndexPoolPaymentLog);
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
                    decimal pourcentageOfReward = minerStats.Value.TotalMiningScore / totalMiningScore;
                    decimal minerReward = ((blockReward * pourcentageOfReward) / 100);
                    ClassLog.ConsoleWriteLog("Miner "+minerStats.Key+" receive "+pourcentageOfReward+"%  of block ID: " + blockId + " amount: " + minerReward+ " "+ClassConnectorSetting.CoinNameMin, ClassLogEnumeration.IndexPoolPaymentLog);
                    minerStats.Value.TotalBalance += minerReward;
                }
            }
        }
    }
}
