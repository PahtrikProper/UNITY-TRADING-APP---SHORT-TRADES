using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using ShortWaveTrader.UI;
using ShortWaveTrader.Data;
using ShortWaveTrader.Core;
using ShortWaveTrader.Engines;
using ShortWaveTrader.Strategies;

namespace ShortWaveTrader
{
    public class RuntimeApp : MonoBehaviour
    {
        private RuntimeUI ui;

        void Start()
        {
            ui = new RuntimeUI();
            ui.Build();
            StartCoroutine(FetchAndShow());
        }

        IEnumerator FetchAndShow()
        {
            ui.SetStatus("Fetching Bybit candles… ADAUSDT 1m latest");
            ui.SetProgress(0f);

            var client = new BybitKlineClient();
            List<Candle> candles = null;
            string err = null;

            yield return StartCoroutine(client.FetchADAUSDT_1m_Latest(
                ok => candles = ok,
                e => err = e
            ));

            if (!string.IsNullOrEmpty(err))
            {
                ui.SetStatus("DATA ERROR: " + err);
                ui.SetSummary(err);
                yield break;
            }

            ui.SetStatus($"Fetched {candles.Count} candles (oldest→newest). Showing sample…");
            ui.SetProgress(1f);

            // show first 5 and last 5 so you can visually verify it’s real
            int n = candles.Count;
            int show = Mathf.Min(5, n);

            ui.AddRow("FIRST candles:");
            for (int i = 0; i < show; i++)
            {
                var c = candles[i];
                ui.AddRow($"{i+1}/{n} t={c.TimeMs} O={c.Open} H={c.High} L={c.Low} C={c.Close} V={c.Volume}");
            }

            ui.AddRow("LAST candles:");
            for (int i = n - show; i < n; i++)
            {
                var c = candles[i];
                ui.AddRow($"{i+1}/{n} t={c.TimeMs} O={c.Open} H={c.High} L={c.Low} C={c.Close} V={c.Volume}");
            }

            var first = candles[0];
            var last = candles[n - 1];
            ui.SetSummary(
                $"REAL DATA CONFIRMED\n" +
                $"Candles={n}\n" +
                $"FirstClose={first.Close}\n" +
                $"LastClose={last.Close}\n" +
                $"Δ={last.Close - first.Close:F6}"
            );

            yield return StartCoroutine(RunOptimizationAndPaperTrade(candles));
        }

        private IEnumerator RunOptimizationAndPaperTrade(List<Candle> candles)
        {
            if (candles == null || candles.Count == 0)
            {
                ui.AddRow("No candles available for optimization.");
                yield break;
            }

            ui.AddRow("----- OPTIMIZATION -----");
            ui.SetStatus("Optimizing strategy grid…");

            var baseParams = new StrategyParams();
            var strat = new ShortOnlyTrendTPStrategy();
            var optimizer = new OptimizerEngine();

            StrategyParams bestP = null;
            BacktestState bestR = null;

            optimizer.OnIteration += (i, total, p, r) =>
            {
                if (i % 3 == 0)
                    ui.SetStatus($"Optimization {i}/{total}… balance={r.Balance:F2}");
            };

            optimizer.OnBestUpdated += (p, r) =>
            {
                bestP = p;
                bestR = r;
                ui.AddRow($"New best: SMA={p.SmaPeriod} Stoch={p.StochPeriod} MACD={p.UseMacd} Signal={p.UseSignal} MomExit={p.UseMomentumExit} Balance={r.Balance:F2} Trades={r.Trades}");
            };

            yield return null; // allow UI to update before heavy loop
            (bestP, bestR) = optimizer.Optimize(candles, baseParams, strat);

            ui.AddRow("----- BACKTEST (BEST) -----");
            ui.AddRow($"Balance={bestR.Balance:F2} Trades={bestR.Trades} Wins={bestR.Wins} Losses={bestR.Losses} MaxDD={bestR.MaxDrawdown:F2}");
            ui.SetSummary(
                $"{ui.GetSummaryText()}\n\nBEST PARAMS\nSMA={bestP.SmaPeriod} | Stoch={bestP.StochPeriod} | MACD={bestP.UseMacd} | Signal={bestP.UseSignal} | MomentumExit={bestP.UseMomentumExit}\nBalance={bestR.Balance:F2} Trades={bestR.Trades} MaxDD={bestR.MaxDrawdown:F2}"
            );

            yield return StartCoroutine(RunPaperTrader(candles, bestP, strat));
        }

        private IEnumerator RunPaperTrader(IReadOnlyList<Candle> candles, StrategyParams p, IStrategy strat)
        {
            ui.AddRow("----- PAPER TRADING (LIVE REPLAY) -----");
            ui.SetStatus("Starting paper trading replay with best params…");

            var pos = new PositionState();
            var indicators = Indicators.BuildCache(candles, p);
            var state = new BacktestState();
            state.Reset(p.StartingBalance);

            int warmup = Mathf.Max(Mathf.Max(p.SmaPeriod, p.StochPeriod), Mathf.Max(p.MacdSlow, p.MacdSignal)) + 2;
            int loggedTrades = 0;

            for (int i = 0; i < candles.Count; i++)
            {
                double price = candles[i].Close;

                if (!pos.IsOpen)
                {
                    if (i >= warmup && strat.ShouldEnterShort(candles, i, p, indicators))
                    {
                        double marginUsed = state.Balance * p.RiskFraction;
                        double notional = p.MarginRate > 0 ? marginUsed / p.MarginRate : 0;
                        double qty = price > 0 ? notional / price : 0;

                        if (marginUsed > 0 && qty > 0)
                        {
                            state.Balance -= marginUsed;
                            double tpPrice = price * (1 - p.TakeProfitPct);
                            pos.OpenShort(price, i, candles[i].Time, qty, marginUsed, tpPrice);
                            if (loggedTrades < 20)
                            {
                                ui.AddRow($"ENTER SHORT @{i} price={price:F4} tp={tpPrice:F4} qty={qty:F4}");
                                loggedTrades++;
                            }
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
                        double after = state.Balance + pos.MarginUsed + pnl;

                        state.AddTrade(new TradeRecord
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

                        if (loggedTrades < 20)
                        {
                            ui.AddRow($"EXIT @{i} price={exitPrice:F4} pnl={pnl:F2} reason={reason} bal={after:F2}");
                            loggedTrades++;
                        }

                        pos.Reset();
                    }
                }

                if (i % 25 == 0)
                {
                    ui.SetStatus($"Paper trading… {i + 1}/{candles.Count} bars");
                    yield return null;
                }
            }

            ui.AddRow($"Paper trading complete. Trades={state.Trades} Balance={state.Balance:F2} MaxDD={state.MaxDrawdown:F2}");
            ui.SetStatus("Paper trading finished with optimized params.");
        }
    }
}
