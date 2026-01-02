using System;
using ShortWaveTrader.Core;
using ShortWaveTrader.Strategies;

namespace ShortWaveTrader.Engines
{
    public sealed class OptimizerEngine
    {
        public event Action<int,int,StrategyParams,BacktestState> OnIteration;
        public event Action<StrategyParams,BacktestState> OnBestUpdated;

        public (StrategyParams, BacktestState) Optimize(System.Collections.Generic.IReadOnlyList<Candle> candles, StrategyParams baseParams, IStrategy strat)
        {
            StrategyParams bestP = null;
            BacktestState bestR = null;
            double bestBal = double.MinValue;

            int total = baseParams.GridTrendSma.Length * baseParams.GridMaxBars.Length;
            int iter = 0;

            foreach (var sma in baseParams.GridTrendSma)
            foreach (var maxBars in baseParams.GridMaxBars)
            {
                iter++;

                var p = new StrategyParams
                {
                    StartingBalance = baseParams.StartingBalance,
                    Leverage = baseParams.Leverage,
                    MaintenanceMarginPct = baseParams.MaintenanceMarginPct,
                    TakeProfitPct = baseParams.TakeProfitPct,
                    TrendSmaPeriod = sma,
                    MaxBarsInTrade = maxBars,
                };

                var res = new BacktestEngine().Run(candles, p, strat);

                OnIteration?.Invoke(iter, total, p, res);

                if (res.Balance > bestBal)
                {
                    bestBal = res.Balance;
                    bestP = p;
                    bestR = res;
                    OnBestUpdated?.Invoke(bestP, bestR);
                }
            }

            return (bestP, bestR);
        }
    }
}
