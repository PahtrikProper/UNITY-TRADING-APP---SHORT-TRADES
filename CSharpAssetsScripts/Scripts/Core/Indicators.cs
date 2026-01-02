using System;
using System.Collections.Generic;

namespace ShortWaveTrader.Core
{
    public static class Indicators
    {
        public static List<double> SmaSeries(IReadOnlyList<Candle> c, int period)
        {
            var res = new List<double>(new double[c.Count]);
            if (period <= 0) return res;

            double sum = 0;
            for (int i = 0; i < c.Count; i++)
            {
                sum += c[i].Close;
                if (i >= period) sum -= c[i - period].Close;
                res[i] = (i + 1 >= period) ? sum / period : double.NaN;
            }
            return res;
        }

        public static List<double> EmaSeries(IReadOnlyList<double> source, int period)
        {
            var res = new List<double>(new double[source.Count]);
            if (period <= 0 || source.Count == 0) return res;

            double k = 2.0 / (period + 1);
            double ema = source[0];
            res[0] = ema;

            for (int i = 1; i < source.Count; i++)
            {
                ema = (source[i] - ema) * k + ema;
                res[i] = ema;
            }
            return res;
        }

        public static List<double> CenteredStochK(IReadOnlyList<Candle> c, int period, int smoothK)
        {
            var res = new List<double>(new double[c.Count]);
            if (period <= 0) return res;

            var rawWindow = new Queue<double>();

            for (int i = 0; i < c.Count; i++)
            {
                double raw = double.NaN;
                if (i + 1 >= period)
                {
                    double lowest = double.MaxValue;
                    double highest = double.MinValue;
                    for (int j = i - period + 1; j <= i; j++)
                    {
                        lowest = Math.Min(lowest, c[j].Low);
                        highest = Math.Max(highest, c[j].High);
                    }

                    double range = highest - lowest;
                    if (range > 0)
                        raw = 100 * (c[i].Close - lowest) / range;
                    else
                        raw = 0;
                }

                rawWindow.Enqueue(raw);
                if (rawWindow.Count > Math.Max(1, smoothK)) rawWindow.Dequeue();

                double avg = 0;
                int count = 0;
                foreach (var v in rawWindow)
                {
                    if (double.IsNaN(v)) continue;
                    avg += v;
                    count++;
                }

                res[i] = count > 0 ? (avg / count) - 50 : double.NaN;
            }

            return res;
        }

        public static List<double> MacdSeries(IReadOnlyList<Candle> c, int fast, int slow)
        {
            var closes = new List<double>(c.Count);
            for (int i = 0; i < c.Count; i++) closes.Add(c[i].Close);

            var emaFast = EmaSeries(closes, fast);
            var emaSlow = EmaSeries(closes, slow);

            var macd = new List<double>(new double[c.Count]);
            for (int i = 0; i < c.Count; i++)
                macd[i] = emaFast[i] - emaSlow[i];

            return macd;
        }

        public static IndicatorCache BuildCache(IReadOnlyList<Candle> c, StrategyParams p)
        {
            var sma = SmaSeries(c, p.SmaPeriod);
            var stoch = CenteredStochK(c, p.StochPeriod, p.SmoothK);
            var macd = MacdSeries(c, p.MacdFast, p.MacdSlow);
            var signal = EmaSeries(macd, p.MacdSignal);

            return new IndicatorCache
            {
                Sma = sma,
                StochK = stoch,
                Macd = macd,
                Signal = signal
            };
        }
    }
}
