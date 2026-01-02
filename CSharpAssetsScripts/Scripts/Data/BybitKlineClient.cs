using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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

        /// <summary>
        /// Fetch latest 1m candles for ADAUSDT (USDT Perp) from Bybit v5 (newest first -> reversed to oldest first).
        /// Mirrors the provided reference approach: single call without start/end, retry code 10006 with backoff, reverse list.
        /// </summary>
        public IEnumerator FetchADAUSDT_1m_Latest(Action<List<Candle>> onOk, Action<string> onErr)
        {
            string url =
                $"{BaseUrl}?category={DefaultCategory}&symbol={DefaultSymbol}&interval={DefaultIntervalMinutes}&limit={DefaultLimit}";

            int attempt = 0;
            BybitKlineResponse resp = null;

            // retry retCode 10006 with backoff like Python client
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
                        break;

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

            // Validate required invariants per instructions
            if (resp.result == null)
            {
                onErr?.Invoke($"Bybit returned no result object (url={url})");
                yield break;
            }

            if (!string.Equals(resp.result.category, DefaultCategory, StringComparison.OrdinalIgnoreCase))
            {
                onErr?.Invoke($"Unexpected category '{resp.result.category}' (url={url})");
                yield break;
            }

            if (resp.result.list == null || resp.result.list.Count == 0)
            {
                onErr?.Invoke($"Bybit returned empty candles list (url={url})");
                yield break;
            }

            // Bybit returns latest->oldest. Convert and reverse to oldest->newest.
            var all = new List<Candle>(resp.result.list.Count);

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

                all.Add(new Candle
                {
                    Index = all.Count,
                    TimeMs = tMs,
                    Time = time,
                    Open = o,
                    High = h,
                    Low = l,
                    Close = c,
                    Volume = v
                });
            }

            onOk?.Invoke(all);
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
