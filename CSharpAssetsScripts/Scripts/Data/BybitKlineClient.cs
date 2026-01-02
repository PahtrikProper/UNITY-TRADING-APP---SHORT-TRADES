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
        private const string DefaultSymbol = "ADAUSDT";   // USDT Perp
        private const string DefaultCategory = "linear";  // Bybit futures (Unified)
        private const int DefaultIntervalMinutes = 3;
        private const int DefaultLimit = 1000;            // Bybit max per page

        /// <summary>
        /// Fetch last 24 hours of 3m candles for ADAUSDT (USDT Perp) from Bybit v5, paging until no data.
        /// Mirrors the Python data_client: retries retCode 10006 with backoff, returns oldest->newest.
        /// </summary>
        public IEnumerator FetchADAUSDT_3m_Last24h(Action<List<Candle>> onOk, Action<string> onErr)
        {
            var nowUtc = DateTime.UtcNow;
            var startUtc = nowUtc.AddHours(-24);

            long endSec = ToUnixSeconds(nowUtc);
            long startSec = ToUnixSeconds(startUtc);
            var all = new List<Candle>();

            while (startSec < endSec)
            {
                long startMs = startSec * 1000;
                string url =
                    $"{BaseUrl}?category={DefaultCategory}&symbol={DefaultSymbol}&interval={DefaultIntervalMinutes}&start={startMs}&limit={DefaultLimit}";

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

                if (resp.result == null || resp.result.list == null || resp.result.list.Count == 0)
                    break;

                // Bybit returns latest->oldest. Convert and reverse to oldest->newest.
                var page = new List<Candle>(resp.result.list.Count);

                // Each item: [startTime, open, high, low, close, volume, turnover]
                foreach (var row in resp.result.list)
                {
                    if (row == null || row.Count < 6) continue;

                    long tMs = ParseLong(row[0]);
                    double o = ParseDouble(row[1]);
                    double h = ParseDouble(row[2]);
                    double l = ParseDouble(row[3]);
                    double c = ParseDouble(row[4]);
                    double v = ParseDouble(row[5]);
                    var time = DateTimeOffset.FromUnixTimeMilliseconds(tMs).UtcDateTime;

                    page.Add(new Candle
                    {
                        Index = all.Count + page.Count,
                        TimeMs = tMs,
                        Time = time,
                        Open = o,
                        High = h,
                        Low = l,
                        Close = c,
                        Volume = v
                    });
                }

                page.Reverse(); // oldest->newest
                all.AddRange(page);

                // advance start to next bar to avoid duplicates
                long lastMs = page.Count > 0 ? page[^1].TimeMs : startSec * 1000;
                startSec = (lastMs / 1000) + (DefaultIntervalMinutes * 60);

                // small pause to avoid hammering
                yield return new WaitForSeconds(0.2f);
            }

            if (all.Count == 0)
            {
                onErr?.Invoke("Bybit returned no candles for ADAUSDT 3m futures.");
                yield break;
            }

            all.Sort((a, b) => a.TimeMs.CompareTo(b.TimeMs));
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                c.Index = i;
                all[i] = c;
            }

            onOk?.Invoke(all);
        }

        private static long ToUnixMs(DateTime utc)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return (long)(utc - epoch).TotalMilliseconds;
        }

        private static long ToUnixSeconds(DateTime utc)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return (long)(utc - epoch).TotalSeconds;
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
