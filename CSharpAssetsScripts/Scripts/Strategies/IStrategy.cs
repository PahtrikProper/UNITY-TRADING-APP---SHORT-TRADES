using System.Collections.Generic;
using ShortWaveTrader.Core;

namespace ShortWaveTrader.Strategies
{
    public interface IStrategy
    {
        bool ShouldEnterShort(IReadOnlyList<Candle> candles, int i, StrategyParams p);
        bool ShouldExitShort(IReadOnlyList<Candle> candles, int i, PositionState pos, StrategyParams p, out string reason);
    }
}
