namespace CloudSealed.ML.Engine.Models;

// Mirrors 1:1 the TypeScript client that consumes /v1/predict-severity.
// Field names and types do not change without versioning the endpoint.
//
// Stateless by design (same as /v1/predict-architecture): each request carries
// the actual training examples together with the item to classify. No cache/state
// in memory between requests — the caller decides when to retrain simply by
// sending the updated history. Below MinimumTrainingSamples, the endpoint
// refuses and returns trained=false explicitly, instead of risking silent
// overfit.

public class FindingReviewInput
{
    // 'SECURITY' | 'COST' | 'ARCHITECTURE' | 'COMPLIANCE'
    public string Dimension { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    // Required in training examples; ignored in Candidate.
    // 'LOW' | 'MEDIUM' | 'HIGH' | 'CRITICAL'
    public string? Severity { get; set; }
}

public class PredictSeverityRequest
{
    // Findings already reviewed by an analyst (with Severity filled in) — the
    // actual training set. The caller is responsible for only including
    // genuine human reviews, never synthetic data.
    public List<FindingReviewInput> TrainingReviews { get; set; } = new();

    // The finding not yet reviewed that is to be classified.
    public FindingReviewInput Candidate { get; set; } = new();
}

public class PredictSeverityResponse
{
    public bool Trained { get; set; }

    public int TrainingSampleCount { get; set; }

    public int MinimumTrainingSamples { get; set; }

    // Null when Trained=false.
    public string? PredictedSeverity { get; set; }

    // Per-class probability; empty when Trained=false.
    public Dictionary<string, float> ClassProbabilities { get; set; } = new();

    // Populated only when Trained=false, explaining why (insufficient data).
    public string? Message { get; set; }
}
