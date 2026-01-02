using System.Collections.Generic;
using ShortWaveTrader.Core;

namespace ShortWaveTrader.Strategies
{
    public sealed class ShortOnlyTrendTPStrategy : IStrategy
    {
        public bool ShouldEnterShort(IReadOnlyList<Candle> c, int i, StrategyParams p, IndicatorCache indicators)
        {
            if (i < 2) return false;

            var t = c[i].Time;
            bool inDate = (t.Year > p.StartYear) || (t.Year == p.StartYear && t.Month >= p.StartMonth);
            if (!inDate) return false;

            double sma = indicators.Sma[i];
            double smaPrev = i > 0 ? indicators.Sma[i - 1] : double.NaN;
            if (double.IsNaN(sma) || double.IsNaN(smaPrev)) return false;

            bool lowsOk = c[i - 2].Low <= c[i - 1].Low && c[i].Low < c[i - 1].Low;
            bool smaOk = sma < smaPrev;
            bool macdOk = !p.UseMacd || (indicators.Macd[i] < indicators.Macd[i - 1]);
            bool signalOk = !p.UseSignal || (indicators.Signal[i] < indicators.Signal[i - 1]);

            return lowsOk && smaOk && macdOk && signalOk;
        }

        public bool ShouldExitShort(IReadOnlyList<Candle> c, int i, PositionState pos, StrategyParams p, IndicatorCache indicators, out string reason)
        {
            reason = null;

            if (pos.TpPrice > 0 && c[i].Low <= pos.TpPrice)
            {
                reason = "TP";
                return true;
            }

            bool stochReady = i > 0 && !double.IsNaN(indicators.StochK[i]) && !double.IsNaN(indicators.StochK[i - 1]);
            if (p.UseMomentumExit && stochReady && indicators.StochK[i] > indicators.StochK[i - 1])
            {
                reason = "Momentum";
                return true;
            }

            return false;
        }
    }
}
