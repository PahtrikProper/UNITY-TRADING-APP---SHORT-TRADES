using System;

namespace ShortWaveTrader.Core
{
    public struct Candle
    {
        public int Index;
        public long TimeMs;
        public DateTime Time;
        public double Open;
        public double High;
        public double Low;
        public double Close;
        public double Volume;
    }
}
