using System;
using ShortWaveTrader.Core;
using ShortWaveTrader.Strategies;

namespace ShortWaveTrader.Engines
{
    public sealed class OptimizerEngine
    {
        public event Action<int,int,StrategyParams,BacktestState> OnIteration;
        public event Action<StrategyParams,BacktestState> OnBestUpdated;

        public (StrategyParams, BacktestState) OptimizeRandom(System.Collections.Generic.IReadOnlyList<Candle> candles, StrategyParams baseParams, IStrategy strat, int sampleCount = 500, int? seed = null)
        {
            var rng = seed.HasValue ? new Random(seed.Value) : new Random();
            StrategyParams bestP = null;
            BacktestState bestR = null;
            double bestBal = double.MinValue;

            for (int i = 1; i <= sampleCount; i++)
            {
                var p = RandomParams(baseParams, rng);
                var res = new BacktestEngine().Run(candles, p, strat);

                OnIteration?.Invoke(i, sampleCount, p, res);

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

        public (StrategyParams, BacktestState) Optimize(System.Collections.Generic.IReadOnlyList<Candle> candles, StrategyParams baseParams, IStrategy strat)
        {
            StrategyParams bestP = null;
            BacktestState bestR = null;
            double bestBal = double.MinValue;

            int total = baseParams.GridSmaPeriod.Length
                      * baseParams.GridStochPeriod.Length
                      * baseParams.GridUseMacd.Length
                      * baseParams.GridUseSignal.Length
                      * baseParams.GridUseMomentumExit.Length;
            int iter = 0;

            foreach (var sma in baseParams.GridSmaPeriod)
            foreach (var stoch in baseParams.GridStochPeriod)
            foreach (var useMacd in baseParams.GridUseMacd)
            foreach (var useSignal in baseParams.GridUseSignal)
            foreach (var useMom in baseParams.GridUseMomentumExit)
            {
                iter++;

                var p = new StrategyParams
                {
                    StartingBalance = baseParams.StartingBalance,
                    RiskFraction = baseParams.RiskFraction,
                    MarginRate = baseParams.MarginRate,
                    TakeProfitPct = baseParams.TakeProfitPct,
                    StartMonth = baseParams.StartMonth,
                    StartYear = baseParams.StartYear,
                    SmoothK = baseParams.SmoothK,
                    MacdFast = baseParams.MacdFast,
                    MacdSlow = baseParams.MacdSlow,
                    MacdSignal = baseParams.MacdSignal,
                    SmaPeriod = sma,
                    StochPeriod = stoch,
                    UseMacd = useMacd,
                    UseSignal = useSignal,
                    UseMomentumExit = useMom
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

        private static StrategyParams RandomParams(StrategyParams src, Random rng)
        {
            int RandInt(int min, int max) => rng.Next(min, max + 1);
            bool RandBool() => rng.NextDouble() > 0.5;

            return new StrategyParams
            {
                StartingBalance = src.StartingBalance,
                RiskFraction = src.RiskFraction,
                MarginRate = src.MarginRate,
                TakeProfitPct = src.TakeProfitPct,
                StartMonth = src.StartMonth,
                StartYear = src.StartYear,
                SmoothK = src.SmoothK,
                MacdFast = src.MacdFast,
                MacdSlow = src.MacdSlow,
                MacdSignal = src.MacdSignal,
                SmaPeriod = RandInt(20, 120),
                StochPeriod = RandInt(8, 30),
                UseMacd = RandBool(),
                UseSignal = RandBool(),
                UseMomentumExit = RandBool()
            };
        }
    }
}
