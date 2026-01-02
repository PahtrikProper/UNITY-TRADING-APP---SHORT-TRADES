using System;
using System.Collections.Generic;

namespace ShortWaveTrader.Core
{
    public sealed class StrategyParams
    {
        public double StartingBalance = 1000.0;
        public double RiskFraction = 0.95;   // percent of equity to deploy per trade
        public double MarginRate = 0.10;     // 10x notional when risking 100%
        public double DesiredLeverage = 3.0; // target leverage, clamped by MarginRate/MaxLeverage
        public double MaxLeverage = 50.0;    // safety cap to mirror exchange tier rules
        public double BybitFeeRate = 0.0006; // taker fee % (6 bps default)
        public double SpreadBps = 0.0;       // bid/ask spread in basis points
        public double SlippageBps = 0.0;     // random slippage in basis points
        public double MaintenanceMarginRate = 0.004; // 0.4% default maintenance margin
        public int RandomSeed = 1337;
        public double TakeProfitPct = TradeMath.FixedTakeProfitPct; // hard-set TP
        public int StartYear = 2020;
        public int StartMonth = 1;

        public int SmaPeriod = 50;
        public int StochPeriod = 14;
        public int SmoothK = 2;
        public int MacdFast = 12;
        public int MacdSlow = 26;
        public int MacdSignal = 9;
        public bool UseMacd = true;
        public bool UseSignal = true;
        public bool UseMomentumExit = true;

        public int[] GridSmaPeriod = new[] { 30, 50, 80 };
        public int[] GridStochPeriod = new[] { 10, 14, 20 };
        public bool[] GridUseMacd = new[] { true, false };
        public bool[] GridUseSignal = new[] { true, false };
        public bool[] GridUseMomentumExit = new[] { true, false };
    }

    public sealed class PositionState
    {
        public bool IsOpen;
        public double EntryPrice;
        public int EntryIndex;
        public DateTime EntryTime;
        public int BarsHeld;
        public double Qty;
        public double MarginUsed;
        public double TpPrice;
        public double EntryFee;
        public double ExitFee;
        public double TradeValue;
        public double Leverage;
        public double LiqPrice;

        public void OpenShort(double price, int index, DateTime time, double qty, double marginUsed, double tpPrice, double entryFee, double tradeValue, double leverage, double liqPrice)
        {
            IsOpen = true;
            EntryPrice = price;
            EntryIndex = index;
            BarsHeld = 0;
            EntryTime = time;
            Qty = qty;
            MarginUsed = marginUsed;
            TpPrice = tpPrice;
            EntryFee = entryFee;
            TradeValue = tradeValue;
            Leverage = leverage;
            LiqPrice = liqPrice;
        }

        public void Reset()
        {
            IsOpen = false;
            EntryPrice = 0;
            EntryIndex = 0;
            BarsHeld = 0;
            EntryTime = default;
            Qty = 0;
            MarginUsed = 0;
            TpPrice = 0;
            EntryFee = 0;
            ExitFee = 0;
            TradeValue = 0;
            Leverage = 0;
            LiqPrice = 0;
        }

        public double UnrealizedPnl(double markPrice)
        {
            if (!IsOpen || Qty <= 0) return 0;
            return (EntryPrice - markPrice) * Qty;
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
        public double EntryFee;
        public double ExitFee;
        public double BalanceAfter;
        public double Leverage;
        public double LiqPrice;
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

        public void MarkEquity(double? equityOverride = null)
        {
            double equity = equityOverride ?? Balance;
            EquityCurve.Add(equity);
            if (equity > Peak) Peak = equity;
            var dd = Peak - equity;
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

    public sealed class IndicatorCache
    {
        public List<double> Sma;
        public List<double> StochK;
        public List<double> Macd;
        public List<double> Signal;
    }
}
