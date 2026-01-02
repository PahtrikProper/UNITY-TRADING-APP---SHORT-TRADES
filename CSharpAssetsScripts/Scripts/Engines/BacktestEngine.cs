using System.Collections.Generic;
using ShortWaveTrader.Core;
using ShortWaveTrader.Strategies;

namespace ShortWaveTrader.Engines
{
    public sealed class BacktestEngine
    {
        public BacktestState Run(IReadOnlyList<Candle> candles, StrategyParams p, IStrategy strat)
        {
            var st = new BacktestState();
            st.Reset(p.StartingBalance);

            var pos = new PositionState();
            double fixedNotional = p.StartingBalance * p.Leverage;

            for (int i = 0; i < candles.Count; i++)
            {
                double price = candles[i].Close;

                if (!pos.IsOpen)
                {
                    if (strat.ShouldEnterShort(candles, i, p))
                        pos.OpenShort(price, i);
                }
                else
                {
                    pos.BarsHeld++;

                    double adverse = (price - pos.EntryPrice) / pos.EntryPrice;
                    if (adverse * p.Leverage >= p.MaintenanceMarginPct)
                    {
                        st.AddTrade(new TradeRecord
                        {
                            EntryBar = pos.EntryIndex,
                            ExitBar = i,
                            EntryTime = candles[pos.EntryIndex].Time,
                            ExitTime = candles[i].Time,
                            Entry = pos.EntryPrice,
                            Exit = price,
                            Pnl = -st.Balance,
                            BalanceAfter = 0,
                            Reason = "Liquidation"
                        });
                        break;
                    }

                    if (strat.ShouldExitShort(candles, i, pos, p, out var reason))
                    {
                        double ret = (pos.EntryPrice - price) / pos.EntryPrice;
                        double pnl = ret * fixedNotional;
                        double after = st.Balance + pnl;

                        st.AddTrade(new TradeRecord
                        {
                            EntryBar = pos.EntryIndex,
                            ExitBar = i,
                            EntryTime = candles[pos.EntryIndex].Time,
                            ExitTime = candles[i].Time,
                            Entry = pos.EntryPrice,
                            Exit = price,
                            Pnl = pnl,
                            BalanceAfter = after,
                            Reason = reason
                        });

                        pos.Reset();
                    }
                }

                st.MarkEquity();
            }

            return st;
        }
    }
}
