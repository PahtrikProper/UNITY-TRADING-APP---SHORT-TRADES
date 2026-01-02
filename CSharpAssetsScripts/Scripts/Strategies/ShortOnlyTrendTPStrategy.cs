using System.Collections.Generic;
using ShortWaveTrader.Core;

namespace ShortWaveTrader.Strategies
{
    public sealed class ShortOnlyTrendTPStrategy : IStrategy
    {
        public bool ShouldEnterShort(IReadOnlyList<Candle> c, int i, StrategyParams p)
        {
            if (i < p.TrendSmaPeriod) return false;
            double sma = Indicators.SMA(c, i, p.TrendSmaPeriod);
            return c[i].Close < sma;
        }

        public bool ShouldExitShort(IReadOnlyList<Candle> c, int i, PositionState pos, StrategyParams p, out string reason)
        {
            reason = null;

            double move = (pos.EntryPrice - c[i].Close) / pos.EntryPrice;
            if (move >= p.TakeProfitPct)
            {
                reason = "TP";
                return true;
            }

            if (pos.BarsHeld >= p.MaxBarsInTrade)
            {
                reason = "Time";
                return true;
            }

            return false;
        }
    }
}
