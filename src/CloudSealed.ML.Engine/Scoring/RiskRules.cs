namespace CloudSealed.ML.Engine.Scoring;

// Pesos e limiares do scoring determinístico. Nenhum modelo treinado: o request
// só traz inventário declarado (nome/tipo/criticidade/exposição), não telemetria
// real nem grafo de dependências, então cada peso aqui é uma regra de arquitetura
// explícita, não um coeficiente aprendido. Ver README para o porquê.
public static class RiskRules
{
    // ---- Single Point of Failure ----
    // Base pelo impacto declarado da criticidade. O schema não expõe campo de
    // redundância, então assume-se instância única (pior caso) — essa premissa
    // é declarada no texto do finding, não escondida no número.
    public const int SpofBaseLow = 0;
    public const int SpofBaseMedium = 15;
    public const int SpofBaseHigh = 35;
    public const int SpofBaseCritical = 55;

    public const int SpofModifierDatabase = 15;    // estado é mais caro de replicar que stateless
    public const int SpofModifierThirdParty = 20;   // fora do controle, sem fallback declarado
    public const int SpofModifierAppOrApi = 5;

    public const int SpofFindingThreshold = 50;      // acima disso, gera finding

    // ---- Excessive Coupling ----
    // Sem grafo de dependências no request, o proxy de acoplamento é exposição +
    // presença de autenticação + dependência de terceiros (por sistema e agregado).
    public const int CouplingPublicNoAuth = 40;
    public const int CouplingPublicWithAuth = 15;
    public const int CouplingInternalBase = 5;
    public const int CouplingThirdPartyType = 25;

    public const int CouplingFanOutFreeCount = 2;    // até 2 THIRD_PARTY_SERVICE no request não penaliza
    public const int CouplingFanOutStep = 5;         // +5 por dependência além da 2ª
    public const int CouplingFanOutCap = 20;         // teto do bônus agregado de fan-out

    public const int CouplingFindingThreshold = 50;

    // ---- Scalability Gap ----
    // Preferência por métrica real (historicalMetrics) sobre sinal declarativo.
    public const double P99LatencyThresholdMs = 1000;
    public const int ScalabilityP99Weight = 30;

    public const double TailRatioThreshold = 3.0;    // p99/avg acima disso = cauda pesada sob carga
    public const int ScalabilityTailRatioWeight = 20;

    // Fallback condicional quando não há historicalMetrics — rotulado como
    // "unknown risk"/premissa no finding, não tratado como medição.
    public const int ScalabilityDataSensitivityDbWeight = 20;
    public const int ScalabilityCriticalNoMetricsWeight = 15;

    // 30 e não 40: o par DATABASE+dataSensitivity (20) + CRITICAL sem métricas (15)
    // soma 35 e precisa cruzar o limiar sozinho — nenhum dos dois sinais isolados
    // deve gerar finding, só a combinação.
    public const int ScalabilityFindingThreshold = 30;

    public const int MaxRiskScore = 100;

    public static int SpofBaseByCriticality(string criticality) => criticality switch
    {
        "CRITICAL" => SpofBaseCritical,
        "HIGH" => SpofBaseHigh,
        "MEDIUM" => SpofBaseMedium,
        _ => SpofBaseLow,
    };

    public static int SpofModifierByType(string type) => type switch
    {
        "DATABASE" => SpofModifierDatabase,
        "THIRD_PARTY_SERVICE" => SpofModifierThirdParty,
        "APPLICATION" or "API" => SpofModifierAppOrApi,
        _ => 0,
    };

    // Peso de criticidade usado para ponderar o overallArchitectureScore: um
    // CRITICAL de alto risco não pode se diluir entre vários LOW no cálculo geral.
    public static int OverallWeightByCriticality(string criticality) => criticality switch
    {
        "CRITICAL" => 4,
        "HIGH" => 3,
        "MEDIUM" => 2,
        _ => 1,
    };
}
