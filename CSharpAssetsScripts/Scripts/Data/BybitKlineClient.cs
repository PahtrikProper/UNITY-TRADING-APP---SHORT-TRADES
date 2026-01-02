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
    internal class BybitMetaResponse
    {
        public int retCode;
        public string retMsg;
    }

    public sealed class BybitKlineClient
    {
        private const string BaseUrl = "https://api.bybit.com/v5/market/kline";
        private const string Symbol = "ADAUSDT";
        private const int IntervalMinutes = 3;
        private const int Limit = 1500; // 3 days of 3m bars ≈ 1440, pad a bit

        private static string BuildUrl()
        {
            long startMs = DateTimeOffset.UtcNow.AddDays(-3).ToUnixTimeMilliseconds();
            var sb = new StringBuilder();
            sb.Append(BaseUrl);
            sb.Append("?category=linear");
            sb.Append("&symbol=").Append(Symbol);
            sb.Append("&interval=").Append(IntervalMinutes);
            sb.Append("&limit=").Append(Limit);
            sb.Append("&start=").Append(startMs);
            return sb.ToString();
        }

        public IEnumerator FetchADAUSDT_3m_Last3Days(
            Action<List<Candle>> onOk,
            Action<string> onErr)
        {
            string url = BuildUrl();
            using var req = UnityWebRequest.Get(url);
            req.timeout = 20;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke(req.error);
                yield break;
            }

            string json = req.downloadHandler.text;

            // STEP 1 — parse metadata only
            BybitMetaResponse meta;
            try
            {
                meta = JsonUtility.FromJson<BybitMetaResponse>(json);
            }
            catch (Exception e)
            {
                onErr?.Invoke("Meta parse failed: " + e.Message);
                yield break;
            }

            if (meta.retCode != 0)
            {
                onErr?.Invoke($"Bybit error {meta.retCode}: {meta.retMsg}");
                yield break;
            }

            // STEP 2 — extract kline array manually
            int listStart = json.IndexOf("\"list\":", StringComparison.Ordinal);
            if (listStart < 0)
            {
                onErr?.Invoke("Missing kline list in response.");
                yield break;
            }

            int arrayStart = json.IndexOf('[', listStart);
            int arrayEnd = json.IndexOf("]]", arrayStart, StringComparison.Ordinal);
            if (arrayStart < 0 || arrayEnd < 0)
            {
                onErr?.Invoke("Malformed kline array.");
                yield break;
            }

            string arrayBody = json.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);
            string[] rows = arrayBody.Split(new[] { "],[" }, StringSplitOptions.RemoveEmptyEntries);

            if (rows.Length == 0)
            {
                onErr?.Invoke("Empty candle data.");
                yield break;
            }

            var candles = new List<Candle>(rows.Length);

            // Bybit returns newest → oldest
            for (int i = rows.Length - 1; i >= 0; i--)
            {
                string row = rows[i].Replace("[", "").Replace("]", "").Replace("\"", "");
                string[] v = row.Split(',');

                if (v.Length < 6)
                    continue;

                long t = long.Parse(v[0], CultureInfo.InvariantCulture);
                double o = double.Parse(v[1], CultureInfo.InvariantCulture);
                double h = double.Parse(v[2], CultureInfo.InvariantCulture);
                double l = double.Parse(v[3], CultureInfo.InvariantCulture);
                double c = double.Parse(v[4], CultureInfo.InvariantCulture);
                double vol = double.Parse(v[5], CultureInfo.InvariantCulture);

                candles.Add(new Candle
                {
                    Index = candles.Count,
                    TimeMs = t,
                    Time = DateTimeOffset.FromUnixTimeMilliseconds(t).UtcDateTime,
                    Open = o,
                    High = h,
                    Low = l,
                    Close = c,
                    Volume = vol
                });
            }

            onOk?.Invoke(candles);
        }
    }
}
