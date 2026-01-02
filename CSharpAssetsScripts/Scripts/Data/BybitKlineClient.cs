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
        // https://api.bybit.com/v5/market/kline?category=linear&symbol=ADAUSDT&interval=3&start=...&end=...&limit=...
        private const string BaseUrl = "https://api.bybit.com/v5/market/kline";

        /// <summary>
        /// Fetch last 24 hours of 3m candles for ADAUSDT (USDT Perp) from Bybit v5.
        /// Returns candles sorted oldest->newest (Unity-friendly).
        /// </summary>
        public IEnumerator FetchADAUSDT_3m_Last24h(Action<List<Candle>> onOk, Action<string> onErr)
        {
            var nowUtc = DateTime.UtcNow;
            var startUtc = nowUtc.AddHours(-24);

            // Bybit expects ms epoch
            long startMs = ToUnixMs(startUtc);
            long endMs = ToUnixMs(nowUtc);

            // 24h / 3m = 480 candles. Bybit limit often 1000 max; use 480.
            int limit = 480;

            string url =
                $"{BaseUrl}?category=linear&symbol=ADAUSDT&interval=3&start={startMs}&end={endMs}&limit={limit}";

            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = 20;
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    onErr?.Invoke($"HTTP error: {req.error}");
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
                    onErr?.Invoke($"JSON parse error: {e.Message}\nRaw: {json}");
                    yield break;
                }

                if (resp == null)
                {
                    onErr?.Invoke($"Null response parse.\nRaw: {json}");
                    yield break;
                }

                if (resp.retCode != 0)
                {
                    onErr?.Invoke($"Bybit error retCode={resp.retCode} msg={resp.retMsg}");
                    yield break;
                }

                if (resp.result == null || resp.result.list == null || resp.result.list.Count == 0)
                {
                    onErr?.Invoke("Bybit returned no candles.");
                    yield break;
                }

                // Bybit returns latest->oldest. Convert and reverse to oldest->newest.
                var candles = new List<Candle>(resp.result.list.Count);

                // Each item: [startTime, open, high, low, close, volume, turnover]
                // All as strings.
                foreach (var row in resp.result.list)
                {
                    if (row == null || row.Count < 6) continue;

                    // Parse using invariant culture
                    long tMs = ParseLong(row[0]);
                    double o = ParseDouble(row[1]);
                    double h = ParseDouble(row[2]);
                    double l = ParseDouble(row[3]);
                    double c = ParseDouble(row[4]);
                    double v = ParseDouble(row[5]);

                    candles.Add(new Candle
                    {
                        TimeMs = tMs,
                        Open = o,
                        High = h,
                        Low = l,
                        Close = c,
                        Volume = v
                    });
                }

                candles.Reverse(); // oldest->newest

                // sanity: ensure increasing times
                candles.Sort((a, b) => a.TimeMs.CompareTo(b.TimeMs));

                onOk?.Invoke(candles);
            }
        }

        private static long ToUnixMs(DateTime utc)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return (long)(utc - epoch).TotalMilliseconds;
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
