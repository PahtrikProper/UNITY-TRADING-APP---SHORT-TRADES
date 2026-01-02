using System;
using System.Collections.Generic;

namespace ShortWaveTrader.Core
{
    public struct Candle
    {
        public int Index;
        public DateTime Time;
        public double Open, High, Low, Close, Volume;
    }

    public sealed class StrategyParams
    {
        public double StartingBalance = 500.0;
        public double Leverage = 10.0;
        public double MaintenanceMarginPct = 0.5;
        public double TakeProfitPct = 0.0038;

        public int TrendSmaPeriod = 50;
        public int MaxBarsInTrade = 12;

        public int[] GridTrendSma = new[] { 20, 30, 50, 80, 120 };
        public int[] GridMaxBars  = new[] { 8, 12, 18, 24, 36 };
    }

    public sealed class PositionState
    {
        public bool IsOpen;
        public double EntryPrice;
        public int EntryIndex;
        public int BarsHeld;

        public void OpenShort(double price, int index)
        {
            IsOpen = true;
            EntryPrice = price;
            EntryIndex = index;
            BarsHeld = 0;
        }

        public void Reset()
        {
            IsOpen = false;
            EntryPrice = 0;
            EntryIndex = 0;
            BarsHeld = 0;
        }
    }

    public sealed class TradeRecord
    {
        public int EntryBar;
        public int ExitBar;
        public DateTime EntryTime;
        public DateTime ExitTime;
        public double Entry;
        public double Exit;
        public double Pnl;
        public double BalanceAfter;
        public string Reason;
    }

    public sealed class BacktestState
    {
        public double StartingBalance;
        public double Balance;
        public double Peak;
        public double MaxDrawdown;
        public int Trades;
        public int Wins;
        public int Losses;

        public readonly List<double> EquityCurve = new();
        public readonly List<TradeRecord> TradesLog = new();

        public void Reset(double startingBalance)
        {
            StartingBalance = startingBalance;
            Balance = startingBalance;
            Peak = startingBalance;
            MaxDrawdown = 0;
            Trades = Wins = Losses = 0;
            EquityCurve.Clear();
            TradesLog.Clear();
            EquityCurve.Add(Balance);
        }

        public void MarkEquity()
        {
            EquityCurve.Add(Balance);
            if (Balance > Peak) Peak = Balance;
            var dd = Peak - Balance;
            if (dd > MaxDrawdown) MaxDrawdown = dd;
        }

        public void AddTrade(TradeRecord t)
        {
            Trades++;
            Balance = t.BalanceAfter;
            if (t.Pnl >= 0) Wins++; else Losses++;
            TradesLog.Add(t);
            MarkEquity();
        }
    }
}
