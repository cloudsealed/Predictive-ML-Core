using Xunit;

// ApiEndpointTests muta a env var PREDICTIVE_ML_CORE_API_KEY (estado de processo);
// desabilita paralelismo entre classes para evitar corrida com outros testes.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
