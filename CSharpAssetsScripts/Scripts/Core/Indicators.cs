using System.Collections.Generic;

namespace ShortWaveTrader.Core
{
    public static class Indicators
    {
        public static double SMA(IReadOnlyList<Candle> c, int endExclusive, int period)
        {
            if (endExclusive < period || period <= 0) return 0;
            double sum = 0;
            for (int i = endExclusive - period; i < endExclusive; i++)
                sum += c[i].Close;
            return sum / period;
        }
    }
}
