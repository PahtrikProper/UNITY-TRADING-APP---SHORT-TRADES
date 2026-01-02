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
            ui.SetStatus("Fetching Bybit candles… ADAUSDT 3m last 3 days");
            ui.SetProgress(0f);

            var client = new BybitKlineClient();
            int minBars = RequiredBars(new StrategyParams()) + 10;
            List<Candle> candles = null;
            string err = null;

            yield return StartCoroutine(client.FetchADAUSDT_3m_Last3Days(
                minBars,
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

            int minBars = RequiredBars(baseParams) + 1;
            if (candles.Count < minBars)
            {
                ui.AddRow($"Not enough history to warm indicators (need ≥{minBars} bars, got {candles.Count}).");
                yield break;
            }

            StrategyParams bestP = null;
            BacktestState bestR = null;

            optimizer.OnIteration += (i, total, p, r) =>
            {
                float pct = (float)i / total;
                ui.SetStatus($"Optimization {i}/{total} ({pct:P0})… bal={r.Balance:F2}");
            };

            optimizer.OnBestUpdated += (p, r) =>
            {
                bestP = p;
                bestR = r;
                ui.AddRow($"Best so far: SMA={p.SmaPeriod} Stoch={p.StochPeriod} MACD={p.UseMacd} Signal={p.UseSignal} MomExit={p.UseMomentumExit} Bal={r.Balance:F2} Trades={r.Trades}");
            };

            yield return null; // allow UI to update before heavy loop
            (bestP, bestR) = optimizer.OptimizeRandom(candles, baseParams, strat, sampleCount: 500);

            if (bestP == null || bestR == null)
            {
                ui.AddRow("Optimization failed to produce parameters.");
                ui.SetStatus("Optimization failed — cannot start paper trading.");
                yield break;
            }

            ui.AddRow("----- BACKTEST (BEST) -----");
            ui.AddRow($"Balance={bestR.Balance:F2} Trades={bestR.Trades} Wins={bestR.Wins} Losses={bestR.Losses} MaxDD={bestR.MaxDrawdown:F2}");
            ui.SetSummary(
                $"REAL DATA CONFIRMED\nCandles={candles.Count}\n" +
                $"Best Params → SMA={bestP.SmaPeriod} | Stoch={bestP.StochPeriod} | MACD={bestP.UseMacd} | Signal={bestP.UseSignal} | MomentumExit={bestP.UseMomentumExit}\n" +
                $"Backtest → Balance={bestR.Balance:F2} Trades={bestR.Trades} MaxDD={bestR.MaxDrawdown:F2}"
            );

            ui.ClearRows();
            yield return StartCoroutine(RunLivePaperTrader(bestP, strat));
        }

        private IEnumerator RunLivePaperTrader(StrategyParams p, IStrategy strat)
        {
            var client = new BybitKlineClient();
            int minBars = RequiredBars(p) + 10;
            List<Candle> liveCandles = null;
            string err = null;

            ui.SetStatus("Fetching live candles for paper trading…");
            ui.SetProgress(0f);

            yield return StartCoroutine(client.FetchADAUSDT_3m_Last3Days(
                minBars,
                ok => liveCandles = ok,
                e => err = e
            ));

            if (!string.IsNullOrEmpty(err) || liveCandles == null || liveCandles.Count == 0)
            {
                ui.AddRow("Unable to fetch live candles for paper trading.");
                ui.SetStatus("Live fetch failed.");
                if (!string.IsNullOrEmpty(err)) ui.AddRow(err);
                yield break;
            }

            ui.SetStatus($"Paper trading on live candles ({liveCandles.Count}) with optimized params…");
            ui.SetProgress(1f);
            yield return StartCoroutine(RunPaperTraderLiveLoop(liveCandles[^1].TimeMs, p, strat, state: null, pos: null));
        }

        private IEnumerator RunPaperTraderLiveLoop(long lastProcessedMs, StrategyParams p, IStrategy strat, BacktestState state, PositionState pos)
        {
            ui.AddRow("----- PAPER TRADING (LIVE DATA STREAM) -----");
            ui.SetStatus("Starting live paper trading with optimized params…");

            pos ??= new PositionState();
            state ??= new BacktestState();
            state.Reset(p.StartingBalance);
            var rng = new System.Random(p.RandomSeed);

            int warmup = RequiredBars(p);
            int loggedTrades = 0;

            while (true)
            {
                var client = new BybitKlineClient();
                int minBars = warmup + 10;
                List<Candle> candles = null;
                string err = null;

                yield return StartCoroutine(client.FetchADAUSDT_3m_Last3Days(
                    minBars,
                    ok => candles = ok,
                    e => err = e
                ));

                if (!string.IsNullOrEmpty(err) || candles == null || candles.Count == 0)
                {
                    ui.SetStatus("Live fetch failed, retrying shortly…");
                    if (!string.IsNullOrEmpty(err)) ui.AddRow($"LIVE ERR: {err}");
                    yield return new WaitForSeconds(10f);
                    continue;
                }
                if (candles.Count < warmup + 1)
                {
                    ui.AddRow($"Not enough candles for indicators (have {candles.Count}, need {warmup + 1}). Refetching soon…");
                    yield return new WaitForSeconds(10f);
                    continue;
                }

                var indicators = Indicators.BuildCache(candles, p);
                int startIdx = 0;
                if (lastProcessedMs > 0)
                {
                    startIdx = candles.Count;
                    for (int i = 0; i < candles.Count; i++)
                    {
                        if (candles[i].TimeMs > lastProcessedMs)
                        {
                            startIdx = i;
                            break;
                        }
                    }
                }

                if (startIdx >= candles.Count)
                {
                    ui.SetStatus("No new bars yet… waiting.");
                    yield return new WaitForSeconds(10f);
                    continue;
                }

                for (int i = startIdx; i < candles.Count; i++)
                {
                    double price = candles[i].Close;
                    double sma = indicators.Sma[i];
                    double stoch = indicators.StochK[i];
                    double macd = indicators.Macd[i];
                    double signal = indicators.Signal[i];

                    if (!pos.IsOpen)
                    {
                        if (i >= warmup && strat.ShouldEnterShort(candles, i, p, indicators))
                        {
                            double entryPrice = TradeMath.SimulateFillPrice("short", price, p, rng);
                            double marginUsed = state.Balance * p.RiskFraction;
                            double leverage = TradeMath.ResolveLeverage(marginUsed, p);
                            double notional = marginUsed * leverage;
                            double qty = entryPrice > 0 ? notional / entryPrice : 0;
                            double entryFee = TradeMath.BybitFee(notional, p);
                            double liqPrice = TradeMath.CalcShortLiquidationPrice(entryPrice, leverage, p);

                            if (marginUsed > 0 && qty > 0 && state.Balance >= marginUsed + entryFee)
                            {
                                state.Balance -= marginUsed + entryFee;
                                double tpPrice = TradeMath.TakeProfitPrice(entryPrice);
                                pos.OpenShort(entryPrice, i, candles[i].Time, qty, marginUsed, tpPrice, entryFee, notional, leverage, liqPrice);
                                if (loggedTrades < 20)
                                {
                                    ui.AddRow($"ENTER SHORT @{i} price={price:F4} tp={tpPrice:F4} qty={qty:F4}");
                                    loggedTrades++;
                                }
                            }
                        }
                        if (loggedTrades < 20)
                        {
                            ui.AddRow($"BAR @{i} t={candles[i].Time:HH:mm} price={price:F4} sma={sma:F4} stoch={stoch:F2} macd={macd:F4} sig={signal:F4} pos=FLAT");
                            loggedTrades++;
                        }
                    }
                    else
                    {
                        pos.BarsHeld++;
                        if (candles[i].High >= pos.LiqPrice && pos.LiqPrice > 0)
                        {
                            double exitPriceLiq = pos.LiqPrice;
                            double exitFeeLiq = TradeMath.BybitFee(exitPriceLiq * pos.Qty, p);
                            double pnlGrossLiq = (pos.EntryPrice - exitPriceLiq) * pos.Qty;
                            double netPnlLiq = pnlGrossLiq - pos.EntryFee - exitFeeLiq;
                            double afterLiq = state.Balance + pos.MarginUsed + pnlGrossLiq - exitFeeLiq;

                            state.AddTrade(new TradeRecord
                            {
                                EntryBar = pos.EntryIndex,
                                ExitBar = i,
                                EntryTime = pos.EntryTime,
                                ExitTime = candles[i].Time,
                                Entry = pos.EntryPrice,
                                Exit = exitPriceLiq,
                                Pnl = netPnlLiq,
                                EntryFee = pos.EntryFee,
                                ExitFee = exitFeeLiq,
                                BalanceAfter = afterLiq,
                                Leverage = pos.Leverage,
                                LiqPrice = pos.LiqPrice,
                                Reason = "Liquidation"
                            });

                            if (loggedTrades < 20)
                            {
                                ui.AddRow($"LIQUIDATED @{i} mark={price:F4} liq={exitPriceLiq:F4} pnl={netPnlLiq:F2} bal={afterLiq:F2}");
                                loggedTrades++;
                            }

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
                            double after = state.Balance + pos.MarginUsed + pnlGross - exitFee;

                            state.AddTrade(new TradeRecord
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

                            if (loggedTrades < 20)
                            {
                                ui.AddRow($"EXIT @{i} price={exitPrice:F4} pnl={netPnl:F2} reason={reason} bal={after:F2}");
                                loggedTrades++;
                            }

                            pos.Reset();
                        }
                        if (loggedTrades < 20)
                        {
                            ui.AddRow($"BAR @{i} t={candles[i].Time:HH:mm} price={price:F4} sma={sma:F4} stoch={stoch:F2} macd={macd:F4} sig={signal:F4} pos=SHORT qty={pos.Qty:F4} liq={pos.LiqPrice:F4} tp={pos.TpPrice:F4}");
                            loggedTrades++;
                        }
                    }

                    double openEquity = state.Balance + (pos.IsOpen ? pos.MarginUsed + pos.UnrealizedPnl(price) : 0);
                    state.MarkEquity(openEquity);
                }

                lastProcessedMs = candles[^1].TimeMs;
                ui.SetStatus($"Paper trading live… last={DateTimeOffset.FromUnixTimeMilliseconds(lastProcessedMs):HH:mm:ss} bal={state.Balance:F2}");
                yield return new WaitForSeconds(15f);
            }
        }

        private static int RequiredBars(StrategyParams p)
        {
            return Mathf.Max(Mathf.Max(p.SmaPeriod, p.StochPeriod), Mathf.Max(p.MacdSlow, p.MacdSignal)) + 2;
        }
    }
}
