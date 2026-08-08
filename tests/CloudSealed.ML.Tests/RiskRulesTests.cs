using CloudSealed.ML.Engine.Scoring;

namespace CloudSealed.ML.Tests;

public class RiskRulesTests
{
    [Theory]
    [InlineData("LOW", RiskRules.SpofBaseLow)]
    [InlineData("MEDIUM", RiskRules.SpofBaseMedium)]
    [InlineData("HIGH", RiskRules.SpofBaseHigh)]
    [InlineData("CRITICAL", RiskRules.SpofBaseCritical)]
    [InlineData("UNKNOWN", RiskRules.SpofBaseLow)]
    public void SpofBaseByCriticality_MapsExpectedWeight(string criticality, int expected)
    {
        Assert.Equal(expected, RiskRules.SpofBaseByCriticality(criticality));
    }

    [Theory]
    [InlineData("DATABASE", RiskRules.SpofModifierDatabase)]
    [InlineData("THIRD_PARTY_SERVICE", RiskRules.SpofModifierThirdParty)]
    [InlineData("APPLICATION", RiskRules.SpofModifierAppOrApi)]
    [InlineData("API", RiskRules.SpofModifierAppOrApi)]
    public void SpofModifierByType_MapsExpectedWeight(string type, int expected)
    {
        Assert.Equal(expected, RiskRules.SpofModifierByType(type));
    }

    [Theory]
    [InlineData("LOW", 1)]
    [InlineData("MEDIUM", 2)]
    [InlineData("HIGH", 3)]
    [InlineData("CRITICAL", 4)]
    public void OverallWeightByCriticality_IncreasesWithCriticality(string criticality, int expected)
    {
        Assert.Equal(expected, RiskRules.OverallWeightByCriticality(criticality));
    }
}
