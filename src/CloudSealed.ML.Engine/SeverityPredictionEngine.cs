using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ML;
using Microsoft.ML.Data;
using CloudSealed.ML.Engine.Models;

namespace CloudSealed.ML.Engine
{
    /// <summary>
    /// Learns finding severity (LOW/MEDIUM/HIGH/CRITICAL) from real analyst
    /// review history, instead of a fixed per-category default. Deliberately
    /// takes training examples as an in-memory collection (not a file path):
    /// callers pull real <see cref="FindingReview"/> rows from their own
    /// review history (e.g. rows with a completed human review), so the model
    /// is only ever fit on real, application-collected labels — never
    /// synthetic data standing in for them.
    ///
    /// <see cref="MinimumTrainingSamples"/> is a hard floor, not a suggestion:
    /// <see cref="TrainModel"/> refuses to fit a model below it, and callers
    /// should keep using their existing static/default severity until enough
    /// real reviews accumulate. A model "trained" on a handful of examples
    /// does not generalize — it memorizes noise and reports it with false
    /// confidence, which is worse than admitting there isn't enough data yet.
    /// </summary>
    public class SeverityPredictionEngine
    {
        /// <summary>
        /// Floor for a 4-class problem: below this, a single class can appear
        /// zero or one times in the training split, which produces a
        /// degenerate/overfit model rather than a generalizing one.
        /// </summary>
        public const int MinimumTrainingSamples = 30;

        private readonly MLContext _mlContext;
        private ITransformer? _trainedModel;
        private string[]? _labelNames;
        private int _trainingSampleCount;

        public bool IsTrained => _trainedModel is not null;
        public int TrainingSampleCount => _trainingSampleCount;

        public SeverityPredictionEngine()
        {
            // Seed 42: deterministic training, so results are reproducible for audit.
            _mlContext = new MLContext(seed: 42);
        }

        /// <summary>
        /// Fits the model on real analyst-reviewed findings.
        /// Throws <see cref="InvalidOperationException"/> if fewer than
        /// <see cref="MinimumTrainingSamples"/> examples are provided — by
        /// design, not a bug: see the class-level remarks.
        /// </summary>
        public void TrainModel(IEnumerable<FindingReview> reviews)
        {
            var reviewList = reviews.ToList();
            if (reviewList.Count < MinimumTrainingSamples)
            {
                throw new InvalidOperationException(
                    $"Only {reviewList.Count} reviewed finding(s) available; " +
                    $"need at least {MinimumTrainingSamples} to train without overfitting. " +
                    "Keep using the static/default severity until more real reviews accumulate.");
            }

            var dataView = _mlContext.Data.LoadFromEnumerable(reviewList);

            var pipeline = _mlContext.Transforms.Conversion
                .MapValueToKey(outputColumnName: "Label", inputColumnName: nameof(FindingReview.Severity))
                .Append(_mlContext.Transforms.Categorical.OneHotEncoding("DimensionEncoded", nameof(FindingReview.Dimension)))
                .Append(_mlContext.Transforms.Categorical.OneHotEncoding("CategoryEncoded", nameof(FindingReview.Category)))
                .Append(_mlContext.Transforms.Text.FeaturizeText("TitleFeaturized", nameof(FindingReview.Title)))
                .Append(_mlContext.Transforms.Text.FeaturizeText("DescriptionFeaturized", nameof(FindingReview.Description)))
                .Append(_mlContext.Transforms.Concatenate("Features",
                    "DimensionEncoded", "CategoryEncoded", "TitleFeaturized", "DescriptionFeaturized"))
                .Append(_mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy())
                .Append(_mlContext.Transforms.Conversion.MapKeyToValue(outputColumnName: "PredictedLabel", inputColumnName: "PredictedLabel"));

            _trainedModel = pipeline.Fit(dataView);
            _trainingSampleCount = reviewList.Count;
            _labelNames = ExtractLabelNames(dataView, pipeline, _trainedModel);
        }

        /// <summary>
        /// Predicts severity for a new (unreviewed) finding. The returned
        /// <see cref="ClassProbabilities"/> lets the caller decide its own
        /// confidence threshold rather than trusting PredictedSeverity blindly.
        /// </summary>
        public (string PredictedSeverity, Dictionary<string, float> ClassProbabilities) Predict(FindingReview input)
        {
            if (_trainedModel is null || _labelNames is null)
                throw new InvalidOperationException("Model must be trained before inference — call TrainModel first.");

            var engine = _mlContext.Model.CreatePredictionEngine<FindingReview, SeverityPrediction>(_trainedModel);
            var result = engine.Predict(input);

            var probabilities = new Dictionary<string, float>();
            for (var i = 0; i < _labelNames.Length && i < result.Score.Length; i++)
            {
                probabilities[_labelNames[i]] = result.Score[i];
            }

            return (result.PredictedSeverity, probabilities);
        }

        // The Score column comes back in the trainer's internal key order, not
        // alphabetical or insertion order — this reads that order back from
        // the label column's key-value metadata so ClassProbabilities can be
        // labeled correctly instead of guessing.
        private string[] ExtractLabelNames(IDataView trainData, IEstimator<ITransformer> pipeline, ITransformer model)
        {
            var transformed = model.Transform(trainData);
            var labelColumn = transformed.Schema["Label"];
            VBuffer<ReadOnlyMemory<char>> keyValues = default;
            labelColumn.GetKeyValues(ref keyValues);
            return keyValues.DenseValues().Select(v => v.ToString()).ToArray();
        }
    }
}
