using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using ShortWaveTrader.UI;
using ShortWaveTrader.Data;
using ShortWaveTrader.Core;

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
            ui.SetStatus("Fetching Bybit candles… ADAUSDT 1m latest");
            ui.SetProgress(0f);

            var client = new BybitKlineClient();
            List<Candle> candles = null;
            string err = null;

            yield return StartCoroutine(client.FetchADAUSDT_1m_Latest(
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
        }
    }
}
