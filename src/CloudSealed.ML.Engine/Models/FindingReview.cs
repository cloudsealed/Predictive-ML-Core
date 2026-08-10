using Microsoft.ML.Data;

namespace CloudSealed.ML.Engine.Models
{
    /// <summary>
    /// One analyst-reviewed finding: the attributes known at triage time, plus
    /// the severity the analyst actually assigned. This is the training example
    /// shape — <see cref="Severity"/> is only populated for records used to fit
    /// the model, never for inference input.
    /// </summary>
    public class FindingReview
    {
        public string Dimension { get; set; } = "";   // SECURITY | COST | ARCHITECTURE | COMPLIANCE
        public string Category { get; set; } = "";     // e.g. "Identidade & Acesso"
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Severity { get; set; } = "";      // Label. LOW | MEDIUM | HIGH | CRITICAL
    }

    public class SeverityPrediction
    {
        [ColumnName("PredictedLabel")]
        public string PredictedSeverity { get; set; } = "";

        // Per-class probabilities, in the order ML.NET assigned key values.
        // Paired with label names by SeverityPredictionEngine.Predict for the caller.
        public float[] Score { get; set; } = System.Array.Empty<float>();
    }
}
