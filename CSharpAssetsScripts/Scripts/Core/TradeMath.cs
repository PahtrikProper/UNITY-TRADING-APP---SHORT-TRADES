using System;

namespace ShortWaveTrader.Core
{
    public static class TradeMath
    {
        private const double BpsDivisor = 10000.0;
        public const double FixedTakeProfitPct = 0.0044; // 0.44%

        public static double BybitFee(double tradeValue, StrategyParams p)
        {
            return tradeValue * Math.Max(0, p.BybitFeeRate);
        }

        public static double CalcShortLiquidationPrice(double entryPrice, double leverage, StrategyParams p)
        {
            double mmRate = Math.Max(0, p.MaintenanceMarginRate);
            double takerFee = Math.Max(0, p.BybitFeeRate);
            double lev = Math.Max(leverage, 1e-9);
            return Math.Max(entryPrice * (1 + (1 / lev) - mmRate + takerFee), 0);
        }

        public static double ResolveLeverage(double marginUsed, StrategyParams p)
        {
            if (marginUsed <= 0) return 1.0;

            double desired = p.DesiredLeverage > 0
                ? p.DesiredLeverage
                : (p.MarginRate > 0 ? 1.0 / p.MarginRate : 1.0);

            double maxByConfig = p.MaxLeverage > 0 ? p.MaxLeverage : desired;
            double impliedByMargin = p.MarginRate > 0 ? (1.0 / p.MarginRate) : maxByConfig;

            double leverage = Math.Min(desired, impliedByMargin);
            leverage = Math.Min(leverage, maxByConfig);
            return Math.Max(1.0, leverage);
        }

        public static double SimulateFillPrice(string direction, double midPrice, StrategyParams p, Random rng)
        {
            double spread = midPrice * (p.SpreadBps / BpsDivisor);
            double slippageAmt = 0;

            if (p.SlippageBps > 0)
            {
                // Box-Muller transform to sample a normal distribution similar to the Python version
                double u1 = 1.0 - rng.NextDouble();
                double u2 = 1.0 - rng.NextDouble();
                double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
                double mean = p.SlippageBps;
                double stdDev = p.SlippageBps / 2.0;
                double sampledBps = Math.Abs(mean + randStdNormal * stdDev);
                slippageAmt = midPrice * (sampledBps / BpsDivisor);
            }

            if (direction == "long")
            {
                return midPrice + spread + slippageAmt;
            }

            return midPrice - spread - slippageAmt;
        }

        public static double TakeProfitPrice(double entryPrice)
        {
            return entryPrice * (1 - FixedTakeProfitPct);
        }
    }
}
