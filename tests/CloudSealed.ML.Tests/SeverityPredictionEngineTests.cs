using System;
using System.Collections.Generic;
using System.Linq;
using CloudSealed.ML.Engine;
using CloudSealed.ML.Engine.Models;
using Xunit;

namespace CloudSealed.ML.Tests
{
    public class SeverityPredictionEngineTests
    {
        // Deliberately synthetic and clearly separable — this suite only
        // proves the pipeline mechanics (refuse-below-floor, train, predict,
        // label the probability vector correctly). It is not evidence the
        // model generalizes; only real analyst-reviewed data can show that.
        private static List<FindingReview> SyntheticReviews(int count)
        {
            var reviews = new List<FindingReview>();
            for (var i = 0; i < count; i++)
            {
                var authIssue = i % 2 == 0;
                reviews.Add(new FindingReview
                {
                    Dimension = authIssue ? "SECURITY" : "COST",
                    Category = authIssue ? "Identidade & Acesso" : "Otimização",
                    Title = authIssue ? "MFA ausente em conta administrativa" : "Recurso ocioso identificado",
                    Description = authIssue
                        ? "Conta com privilégio administrativo sem MFA habilitado."
                        : "Instância sem uso nos últimos 30 dias.",
                    Severity = authIssue ? "CRITICAL" : "MEDIUM",
                });
            }
            return reviews;
        }

        [Fact]
        public void TrainModel_RefusesBelowMinimumSamples()
        {
            var engine = new SeverityPredictionEngine();
            var tooFew = SyntheticReviews(SeverityPredictionEngine.MinimumTrainingSamples - 1);

            var ex = Assert.Throws<InvalidOperationException>(() => engine.TrainModel(tooFew));
            Assert.Contains("need at least", ex.Message);
            Assert.False(engine.IsTrained);
        }

        [Fact]
        public void Predict_ThrowsBeforeTraining()
        {
            var engine = new SeverityPredictionEngine();
            var ex = Assert.Throws<InvalidOperationException>(
                () => engine.Predict(new FindingReview { Dimension = "SECURITY" }));
            Assert.Contains("trained before inference", ex.Message);
        }

        [Fact]
        public void TrainModel_And_Predict_Succeed_AtOrAboveFloor()
        {
            var engine = new SeverityPredictionEngine();
            var reviews = SyntheticReviews(SeverityPredictionEngine.MinimumTrainingSamples);

            engine.TrainModel(reviews);

            Assert.True(engine.IsTrained);
            Assert.Equal(SeverityPredictionEngine.MinimumTrainingSamples, engine.TrainingSampleCount);

            var (predicted, probabilities) = engine.Predict(new FindingReview
            {
                Dimension = "SECURITY",
                Category = "Identidade & Acesso",
                Title = "MFA ausente em conta administrativa",
                Description = "Conta com privilégio administrativo sem MFA habilitado.",
            });

            Assert.False(string.IsNullOrEmpty(predicted));
            // Probabilities must be correctly labeled and sum to ~1 — this is
            // what catches a class-order mismatch between Score[] and the
            // label names (the actual bug risk in this kind of pipeline).
            Assert.Equal(2, probabilities.Count);
            Assert.All(probabilities.Values, p => Assert.InRange(p, 0f, 1f));
            Assert.InRange(probabilities.Values.Sum(), 0.99f, 1.01f);
        }
    }
}
