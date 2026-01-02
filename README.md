# UNITY-TRADING-APP — Short Trades (Bybit-isolated model)

This repository contains a Unity-based short-only trading demonstrator that mirrors Bybit isolated-margin behavior. The Unity side drives backtests, parameter sweeps, and live paper trading while modeling Bybit taker fees, spread/slippage, and liquidation for ADAUSDT linear perpetuals.

## Repository layout

- `CSharpAssetsScripts/` — Unity C# scripts used in the project.
  - `Scripts/Core/` — Strategy parameters, position/trade state, indicator cache, and shared trade math (fees, slippage, liquidation, TP). 【F:CSharpAssetsScripts/Scripts/Core/Models.cs†L6-L109】【F:CSharpAssetsScripts/Scripts/Core/TradeMath.cs†L5-L62】
  - `Scripts/Data/` — Bybit kline client for fetching ADAUSDT 3m candles with enough history for indicator warmup. 【F:CSharpAssetsScripts/Scripts/Data/BybitKlineClient.cs†L21-L132】
  - `Scripts/Engines/` — Backtest engine and optimizer (random 500-sample sweep by default). 【F:CSharpAssetsScripts/Scripts/Engines/BacktestEngine.cs†L10-L129】【F:CSharpAssetsScripts/Scripts/Engines/OptimizerEngine.cs†L12-L36】
  - `Scripts/Strategies/` — Short-only strategy (trend + centered Stoch, optional MACD/signal + momentum exit).
  - `Scripts/Bootstrap/RuntimeApp.cs` — Entry point wiring UI, data fetch, optimization, backtest, and live paper trading loops. 【F:CSharpAssetsScripts/Scripts/Bootstrap/RuntimeApp.cs†L24-L340】

## Key behaviors and exchange modeling

- **Market data**: Fetches ~3 days of ADAUSDT 3-minute candles (limit is raised dynamically to satisfy indicator warmup). Guards against insufficient history before running. 【F:CSharpAssetsScripts/Scripts/Data/BybitKlineClient.cs†L21-L47】
- **Fees & slippage**: Uses Bybit-style taker fee rate (`BybitFeeRate`), plus configurable spread/slippage (basis points) applied to simulated fills. 【F:CSharpAssetsScripts/Scripts/Core/TradeMath.cs†L5-L62】
- **Leverage & liquidation**: Per-position leverage is clamped by available margin, max leverage, and margin rate; liquidation for shorts factors maintenance margin and taker fee. 【F:CSharpAssetsScripts/Scripts/Core/TradeMath.cs†L14-L35】
- **TP**: Hard-set take-profit at 0.44% below entry via shared helper. 【F:CSharpAssetsScripts/Scripts/Core/TradeMath.cs†L5-L36】
- **Optimization**: Randomized sweep of 500 parameter combinations to pick best-performing set before paper trading. 【F:CSharpAssetsScripts/Scripts/Engines/OptimizerEngine.cs†L12-L36】
- **Live paper trading**: Streams new candles, rebuilds indicators, checks entries/exits, simulates fills with slippage/fees, and logs per-bar indicator snapshot plus position state (qty, TP, liq). 【F:CSharpAssetsScripts/Scripts/Bootstrap/RuntimeApp.cs†L213-L336】

## Running in Unity

1. Import `CSharpAssetsScripts` into your Unity project (these are ready-to-use `Assets/Scripts/*` equivalents).
2. Attach `RuntimeApp` to a scene object and enter Play mode.
3. The UI will:
   - Fetch ADAUSDT 3m data,
   - Run the 500-sample optimizer,
   - Backtest the best params,
   - Start live paper trading with the optimized config.

## Strategy at a glance

- **Entry (short-only)**: Lower-low pattern, SMA downtrend, optional MACD/signal filters.
- **Exit**: Fixed 0.44% TP, optional momentum/Stoch reversal, or liquidation trigger; final-bar exit on stream end.
- **Sizing**: Uses `RiskFraction` of equity as margin; notional = margin × resolved leverage; fees debited at entry/exit.

## Configuration highlights (C#)

- `BybitFeeRate`, `SpreadBps`, `SlippageBps`, `MaintenanceMarginRate`, `MarginRate`, `DesiredLeverage`, `MaxLeverage`.
- `RiskFraction`, `StartingBalance`, `RandomSeed` (deterministic fills).
- Indicator lengths: `SmaPeriod`, `StochPeriod`, `MacdFast/Slow/Signal`, plus booleans for MACD/Signal/Momentum exits.

## Known limitations

- Single-market focus (ADAUSDT linear perp).
- Short-only logic; longs would need parallel plumbing.
- No real order submission; live mode is paper/simulated only.

## Extending

- To change markets/intervals, update `Symbol`/`IntervalMinutes` in `BybitKlineClient` and retune warmup/window logic.
- To adjust TP/SL logic, edit `TradeMath.TakeProfitPrice` and the strategy exit checks.
- To widen optimization, raise `sampleCount` in `OptimizerEngine.OptimizeRandom` or add grid search variants.
