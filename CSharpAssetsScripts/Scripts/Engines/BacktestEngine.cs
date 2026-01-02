using System.Collections.Generic;
using System;
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

            if (candles == null || candles.Count == 0) return st;

            var indicators = Indicators.BuildCache(candles, p);

            var pos = new PositionState();
            int warmup = Math.Max(Math.Max(p.SmaPeriod, p.StochPeriod), Math.Max(p.MacdSlow, p.MacdSignal)) + 2;

            for (int i = 0; i < candles.Count; i++)
            {
                double price = candles[i].Close;

                if (!pos.IsOpen)
                {
                    if (i < warmup)
                    {
                        st.MarkEquity();
                        continue;
                    }

                    if (strat.ShouldEnterShort(candles, i, p, indicators))
                    {
                        double marginUsed = st.Balance * p.RiskFraction;
                        double notional = p.MarginRate > 0 ? marginUsed / p.MarginRate : 0;
                        double qty = price > 0 ? notional / price : 0;

                        if (marginUsed > 0 && qty > 0)
                        {
                            st.Balance -= marginUsed;
                            double tpPrice = price * (1 - p.TakeProfitPct);
                            pos.OpenShort(price, i, candles[i].Time, qty, marginUsed, tpPrice);
                            st.MarkEquity(st.Balance + marginUsed);
                            continue;
                        }
                    }
                }
                else
                {
                    pos.BarsHeld++;

                    bool exit = strat.ShouldExitShort(candles, i, pos, p, indicators, out var reason);
                    if (!exit && i == candles.Count - 1)
                    {
                        exit = true;
                        reason = "FinalClose";
                    }

                    if (exit)
                    {
                        double exitPrice = reason == "TP" && pos.TpPrice > 0
                            ? Math.Min(pos.TpPrice, price)
                            : price;

                        double pnl = (pos.EntryPrice - exitPrice) * pos.Qty;
                        double after = st.Balance + pos.MarginUsed + pnl;

                        st.AddTrade(new TradeRecord
                        {
                            EntryBar = pos.EntryIndex,
                            ExitBar = i,
                            EntryTime = pos.EntryTime,
                            ExitTime = candles[i].Time,
                            Entry = pos.EntryPrice,
                            Exit = exitPrice,
                            Pnl = pnl,
                            BalanceAfter = after,
                            Reason = reason
                        });

                        pos.Reset();
                    }
                }

                double openEquity = st.Balance + (pos.IsOpen ? pos.MarginUsed : 0);
                st.MarkEquity(openEquity);
            }

            return st;
        }
    }
}
