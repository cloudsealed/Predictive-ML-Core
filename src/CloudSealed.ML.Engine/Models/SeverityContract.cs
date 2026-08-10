namespace CloudSealed.ML.Engine.Models;

// Espelha 1:1 o cliente TypeScript que consumir /v1/predict-severity.
// Nomes e tipos de campo não mudam sem versionar o endpoint.
//
// Stateless por design (igual /v1/predict-architecture): cada request carrega
// os exemplos de treino reais junto com o item a classificar. Sem cache/estado
// em memória entre requests — o chamador decide quando re-treinar simplesmente
// enviando o histórico atualizado. Abaixo de MinimumTrainingSamples, o
// endpoint recusa e devolve trained=false explicitamente, em vez de arriscar
// overfit silencioso.

public class FindingReviewInput
{
    // 'SECURITY' | 'COST' | 'ARCHITECTURE' | 'COMPLIANCE'
    public string Dimension { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    // Obrigatório nos exemplos de treino; ignorado em Candidate.
    // 'LOW' | 'MEDIUM' | 'HIGH' | 'CRITICAL'
    public string? Severity { get; set; }
}

public class PredictSeverityRequest
{
    // Achados já revisados por analista (com Severity preenchido) — o
    // conjunto de treino real. O chamador é responsável por só incluir
    // revisões humanas de verdade, nunca dado sintético.
    public List<FindingReviewInput> TrainingReviews { get; set; } = new();

    // O achado ainda não revisado que se quer classificar.
    public FindingReviewInput Candidate { get; set; } = new();
}

public class PredictSeverityResponse
{
    public bool Trained { get; set; }

    public int TrainingSampleCount { get; set; }

    public int MinimumTrainingSamples { get; set; }

    // Null quando Trained=false.
    public string? PredictedSeverity { get; set; }

    // Probabilidade por classe; vazio quando Trained=false.
    public Dictionary<string, float> ClassProbabilities { get; set; } = new();

    // Preenchido só quando Trained=false, explicando por que (dado insuficiente).
    public string? Message { get; set; }
}
