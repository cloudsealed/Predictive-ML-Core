using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ML;
using Microsoft.ML.Transforms.TimeSeries;
using CloudSealed.ML.Engine.Models;

namespace CloudSealed.ML.Engine.Scoring
{
    public class TimeSeriesForecaster
    {
        private readonly MLContext _mlContext;

        public TimeSeriesForecaster()
        {
            _mlContext = new MLContext(seed: 42);
        }

        public class LatencyData
        {
            public float Value { get; set; }
        }

        public class LatencyForecast
        {
            public float[] Forecast { get; set; }
            public float[] LowerBound { get; set; }
            public float[] UpperBound { get; set; }
        }

        /// <summary>
        /// Demonstrates Multivariate Time-series Forecasting (SSA Forecasting)
        /// Predicts future latency degradation so the risk score becomes predictive.
        /// </summary>
        public LatencyForecast ForecastDegradation(IEnumerable<float> historicalLatencies)
        {
            var dataList = historicalLatencies.Select(l => new LatencyData { Value = l }).ToList();
            
            if (dataList.Count < 10)
            {
                throw new InvalidOperationException("Need at least 10 historical data points for SSA Forecasting.");
            }

            var dataView = _mlContext.Data.LoadFromEnumerable(dataList);

            // SSA (Singular Spectrum Analysis) is the ML.NET native way to do Time-Series Forecasting
            var forecastingPipeline = _mlContext.Forecasting.ForecastBySsa(
                outputColumnName: nameof(LatencyForecast.Forecast),
                inputColumnName: nameof(LatencyData.Value),
                windowSize: 5,
                seriesLength: dataList.Count,
                trainSize: dataList.Count,
                horizon: 3, // Forecast next 3 intervals
                confidenceLevel: 0.95f,
                confidenceLowerBoundColumn: nameof(LatencyForecast.LowerBound),
                confidenceUpperBoundColumn: nameof(LatencyForecast.UpperBound));

            var model = forecastingPipeline.Fit(dataView);
            var forecastEngine = model.CreateTimeSeriesEngine<LatencyData, LatencyForecast>(_mlContext);

            return forecastEngine.Predict();
        }
    }
}
