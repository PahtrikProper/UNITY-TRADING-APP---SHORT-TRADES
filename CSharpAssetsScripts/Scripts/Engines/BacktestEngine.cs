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
            var rng = new Random(p.RandomSeed);
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
                        double entryPrice = TradeMath.SimulateFillPrice("short", price, p, rng);
                        double marginUsed = st.Balance * p.RiskFraction;
                        double leverage = TradeMath.ResolveLeverage(marginUsed, p);
                        double notional = marginUsed * leverage;
                        double qty = entryPrice > 0 ? notional / entryPrice : 0;
                        double entryFee = TradeMath.BybitFee(notional, p);
                        double liqPrice = TradeMath.CalcShortLiquidationPrice(entryPrice, leverage, p);

                        if (marginUsed > 0 && qty > 0 && st.Balance >= marginUsed + entryFee)
                        {
                            st.Balance -= marginUsed + entryFee;
                            double tpPrice = entryPrice * (1 - p.TakeProfitPct);
                            pos.OpenShort(entryPrice, i, candles[i].Time, qty, marginUsed, tpPrice, entryFee, notional, leverage, liqPrice);
                            st.MarkEquity(st.Balance + marginUsed);
                            continue;
                        }
                    }
                }
                else
                {
                    pos.BarsHeld++;

                    if (candles[i].High >= pos.LiqPrice && pos.LiqPrice > 0)
                    {
                        double exitPrice = pos.LiqPrice;
                        double exitFee = TradeMath.BybitFee(exitPrice * pos.Qty, p);
                        double pnlGross = (pos.EntryPrice - exitPrice) * pos.Qty;
                        double netPnl = pnlGross - pos.EntryFee - exitFee;
                        double after = st.Balance + pos.MarginUsed + pnlGross - exitFee;

                        st.AddTrade(new TradeRecord
                        {
                            EntryBar = pos.EntryIndex,
                            ExitBar = i,
                            EntryTime = pos.EntryTime,
                            ExitTime = candles[i].Time,
                            Entry = pos.EntryPrice,
                            Exit = exitPrice,
                            Pnl = netPnl,
                            EntryFee = pos.EntryFee,
                            ExitFee = exitFee,
                            BalanceAfter = after,
                            Leverage = pos.Leverage,
                            LiqPrice = pos.LiqPrice,
                            Reason = "Liquidation"
                        });

                        pos.Reset();
                        continue;
                    }

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
                        exitPrice = TradeMath.SimulateFillPrice("long", exitPrice, p, rng);
                        double exitFee = TradeMath.BybitFee(exitPrice * pos.Qty, p);

                        double pnlGross = (pos.EntryPrice - exitPrice) * pos.Qty;
                        double netPnl = pnlGross - pos.EntryFee - exitFee;
                        double after = st.Balance + pos.MarginUsed + pnlGross - exitFee;

                        st.AddTrade(new TradeRecord
                        {
                            EntryBar = pos.EntryIndex,
                            ExitBar = i,
                            EntryTime = pos.EntryTime,
                            ExitTime = candles[i].Time,
                            Entry = pos.EntryPrice,
                            Exit = exitPrice,
                            Pnl = netPnl,
                            EntryFee = pos.EntryFee,
                            ExitFee = exitFee,
                            BalanceAfter = after,
                            Leverage = pos.Leverage,
                            LiqPrice = pos.LiqPrice,
                            Reason = reason
                        });

                        pos.Reset();
                    }
                }

                double openEquity = st.Balance + (pos.IsOpen ? pos.MarginUsed + pos.UnrealizedPnl(price) : 0);
                st.MarkEquity(openEquity);
            }

            return st;
        }
    }
}
