using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using ShortWaveTrader.Core;

namespace ShortWaveTrader.Data
{
    [Serializable]
    internal class BybitKlineResponse
    {
        public int retCode;
        public string retMsg;
        public BybitKlineResult result;
        public long time;
    }

    [Serializable]
    internal class BybitKlineResult
    {
        public string symbol;
        public string category;
        public List<List<string>> list;
    }

    public sealed class BybitKlineClient
    {
        // Bybit v5 market kline
        // https://api.bybit.com/v5/market/kline?category=linear&symbol=ADAUSDT&interval=1&limit=...
        private const string BaseUrl = "https://api.bybit.com/v5/market/kline";
        private const string DefaultSymbol = "ADAUSDT";   // USDT Perp
        private const string DefaultCategory = "linear";  // Bybit futures (Unified)
        private const int DefaultIntervalMinutes = 1;
        private const int DefaultLimit = 200;             // Canonical limit per instructions
        private const int FallbackLimit = 1000;           // Max per Bybit paging when using start/end
        private const int LookbackHours = 24;             // fallback window if canonical empty

        /// <summary>
        /// Fetch latest 1m candles for ADAUSDT (USDT Perp) from Bybit v5 (newest first -> reversed to oldest first).
        /// Mirrors the provided reference approach: single call without start/end, retry code 10006 with backoff, reverse list.
        /// </summary>
        public IEnumerator FetchADAUSDT_1m_Latest(Action<List<Candle>> onOk, Action<string> onErr)
        {
            var canonicalUrl = BuildUrl(limit: DefaultLimit);
            var canonicalCandles = new List<Candle>();

            var canonicalResp = new BybitKlineResponse();
            bool canonicalOk = false;
            yield return SendRequest(canonicalUrl, r => canonicalResp = r, err =>
            {
                onErr?.Invoke(err);
            }, out canonicalOk);

            if (!canonicalOk) yield break;

            if (TryBuildCandles(canonicalResp, canonicalCandles, 0, canonicalUrl, out var parseErr, out var emptyList))
            {
                onOk?.Invoke(canonicalCandles);
                yield break;
            }

            if (!emptyList)
            {
                onErr?.Invoke(parseErr);
                yield break;
            }

            // Canonical call returned empty list; fall back to explicit 24h window paging (Python parity)
            long endMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long startMs = endMs - (LookbackHours * 60L * 60L * 1000L);
            if (startMs >= endMs)
            {
                onErr?.Invoke($"Invalid time window start>=end ({startMs}>={endMs}) while retrying Bybit fetch.");
                yield break;
            }

            var windowCandles = new List<Candle>();
            bool windowOk = true;
            long cursor = startMs;

            while (cursor < endMs)
            {
                var url = BuildUrl(cursor, endMs, FallbackLimit);
                var windowResp = new BybitKlineResponse();

                bool ok = false;
                yield return SendRequest(url, r => windowResp = r, err =>
                {
                    onErr?.Invoke(err);
                    windowOk = false;
                }, out ok);

                if (!ok || !windowOk) yield break;

                if (!TryBuildCandles(windowResp, windowCandles, windowCandles.Count, url, out var winErr, out var winEmpty))
                {
                    onErr?.Invoke(winErr);
                    yield break;
                }

                if (windowCandles.Count == 0 && winEmpty)
                {
                    onErr?.Invoke($"Bybit returned empty candles list for window (url={url})");
                    yield break;
                }

                long lastMs = windowCandles.Count > 0 ? windowCandles[^1].TimeMs : cursor;
                cursor = lastMs + (DefaultIntervalMinutes * 60 * 1000);
                yield return new WaitForSeconds(0.2f);
            }

            if (windowCandles.Count == 0)
            {
                onErr?.Invoke("Bybit fallback window paging returned no candles.");
                yield break;
            }

            onOk?.Invoke(windowCandles);
        }

        private IEnumerator SendRequest(string url, Action<BybitKlineResponse> onResp, Action<string> onErr, out bool ok)
        {
            ok = false;
            int attempt = 0;
            while (true)
            {
                using (var req = UnityWebRequest.Get(url))
                {
                    req.timeout = 20;
                    yield return req.SendWebRequest();

                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        onErr?.Invoke($"HTTP error: {req.error} (url={url})");
                        yield break;
                    }

                    string json = req.downloadHandler.text;
                    BybitKlineResponse resp;
                    try
                    {
                        resp = JsonUtility.FromJson<BybitKlineResponse>(json);
                    }
                    catch (Exception e)
                    {
                        onErr?.Invoke($"JSON parse error: {e.Message}\nRaw: {json}\nUrl={url}");
                        yield break;
                    }

                    if (resp == null)
                    {
                        onErr?.Invoke($"Null response parse.\nRaw: {json}\nUrl={url}");
                        yield break;
                    }

                    var code = resp.retCode.ToString();
                    if (code == "0")
                    {
                        onResp?.Invoke(resp);
                        ok = true;
                        yield break;
                    }

                    if (code == "10006" && attempt < 5)
                    {
                        attempt++;
                        float backoffSeconds = 1.5f * attempt;
                        yield return new WaitForSeconds(backoffSeconds);
                        continue;
                    }

                    onErr?.Invoke($"Bybit error retCode={resp.retCode} msg={resp.retMsg}\nUrl={url}");
                    yield break;
                }
            }
        }

        private static string BuildUrl(long? startMs = null, long? endMs = null, int? limit = null)
        {
            var sb = new StringBuilder();
            sb.Append(BaseUrl)
              .Append("?category=").Append(DefaultCategory)
              .Append("&symbol=").Append(DefaultSymbol)
              .Append("&interval=").Append(DefaultIntervalMinutes);
            sb.Append("&limit=").Append(limit ?? DefaultLimit);
            if (startMs.HasValue) sb.Append("&start=").Append(startMs.Value);
            if (endMs.HasValue) sb.Append("&end=").Append(endMs.Value);
            return sb.ToString();
        }

        private static bool TryBuildCandles(BybitKlineResponse resp, List<Candle> output, int startIndex, string url, out string error, out bool emptyList)
        {
            error = null;
            emptyList = false;

            if (resp.result == null)
            {
                error = $"Bybit returned no result object (url={url})";
                return false;
            }

            if (!string.Equals(resp.result.category, DefaultCategory, StringComparison.OrdinalIgnoreCase))
            {
                error = $"Unexpected category '{resp.result.category}' (url={url})";
                return false;
            }

            if (resp.result.list == null || resp.result.list.Count == 0)
            {
                error = $"Bybit returned empty candles list (url={url})";
                emptyList = true;
                return false;
            }

            // Bybit returns latest->oldest. Convert and reverse to oldest->newest.
            for (int idx = resp.result.list.Count - 1; idx >= 0; idx--)
            {
                var row = resp.result.list[idx];
                if (row == null || row.Count < 6) continue;

                long tMs = ParseLong(row[0]);
                double o = ParseDouble(row[1]);
                double h = ParseDouble(row[2]);
                double l = ParseDouble(row[3]);
                double c = ParseDouble(row[4]);
                double v = ParseDouble(row[5]);
                var time = DateTimeOffset.FromUnixTimeMilliseconds(tMs).UtcDateTime;

                output.Add(new Candle
                {
                    Index = startIndex + output.Count,
                    TimeMs = tMs,
                    Time = time,
                    Open = o,
                    High = h,
                    Low = l,
                    Close = c,
                    Volume = v
                });
            }

            return true;
        }

        private static double ParseDouble(string s)
        {
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;
            return 0;
        }

        private static long ParseLong(string s)
        {
            if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) return l;
            // sometimes time might be float-like string; fallback
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return (long)d;
            return 0;
        }
    }
}
