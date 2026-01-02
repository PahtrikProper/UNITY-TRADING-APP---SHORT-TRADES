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
        private const string DefaultSymbol = "ADAUSDT";
        private const string DefaultCategory = "linear";
        private const string DefaultInterval = "3";
        private const int DefaultLimit = 480;

        /// <summary>
        /// Fetch last 24 hours of 3m candles for ADAUSDT (USDT Perp) from Bybit v5.
        /// Returns candles sorted oldest->newest (Unity-friendly).
        /// </summary>
        public IEnumerator FetchADAUSDT_3m_Last24h(Action<List<Candle>> onOk, Action<string> onErr)
        {
            var nowUtc = DateTime.UtcNow;
            var startUtc = nowUtc.AddHours(-24);

            string BuildUrl(DateTime? start, DateTime? end, bool includeEnd)
            {
                var qs = new System.Text.StringBuilder($"{BaseUrl}?category={DefaultCategory}&symbol={DefaultSymbol}&interval={DefaultInterval}&limit={DefaultLimit}");
                if (start.HasValue) qs.Append($"&start={ToUnixMs(start.Value)}");
                if (includeEnd && end.HasValue) qs.Append($"&end={ToUnixMs(end.Value)}");
                return qs.ToString();
            }

            string primaryUrl = BuildUrl(startUtc, nowUtc, includeEnd: true);
            string fallbackUrl = BuildUrl(startUtc, null, includeEnd: false); // ask Bybit for latest window if primary is empty

            IEnumerator DoRequest(string url, Action<List<Candle>> ok, Action<string> err, bool allowFallback, string fallback)
            {
                using (var req = UnityWebRequest.Get(url))
                {
                    req.timeout = 20;
                    yield return req.SendWebRequest();

                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        if (allowFallback)
                        {
                            yield return DoRequest(fallback, ok, err, false, null);
                            yield break;
                        }

                        err?.Invoke($"HTTP error: {req.error} (url={url})");
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
                        err?.Invoke($"JSON parse error: {e.Message}\nRaw: {json}\nUrl={url}");
                        yield break;
                    }

                    if (resp == null)
                    {
                        err?.Invoke($"Null response parse.\nRaw: {json}\nUrl={url}");
                        yield break;
                    }

                    if (resp.retCode != 0)
                    {
                        if (allowFallback)
                        {
                            yield return DoRequest(fallback, ok, err, false, null);
                            yield break;
                        }

                        err?.Invoke($"Bybit error retCode={resp.retCode} msg={resp.retMsg}\nUrl={url}");
                        yield break;
                    }

                    if (resp.result == null || resp.result.list == null || resp.result.list.Count == 0)
                    {
                        if (allowFallback)
                        {
                            yield return DoRequest(fallback, ok, err, false, null);
                            yield break;
                        }

                        err?.Invoke("Bybit returned no candles for ADAUSDT 3m (both primary and fallback).");
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
                        var time = DateTimeOffset.FromUnixTimeMilliseconds(tMs).UtcDateTime;

                        candles.Add(new Candle
                        {
                            Index = candles.Count,
                            TimeMs = tMs,
                            Time = time,
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
                    for (int i = 0; i < candles.Count; i++)
                    {
                        var c = candles[i];
                        c.Index = i;
                        candles[i] = c;
                    }

                    ok?.Invoke(candles);
                }
            }

            yield return DoRequest(primaryUrl, onOk, onErr, allowFallback: true, fallback: fallbackUrl);
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
