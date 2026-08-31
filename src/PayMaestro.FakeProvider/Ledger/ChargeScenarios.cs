using PayMaestro.FakeProvider.Contracts;

namespace PayMaestro.FakeProvider.Ledger;

/// <summary>
/// The behaviour each fake acquirer plays. The last four digits of the card select the
/// scenario, so one card number reproduces one documented outcome on one gateway.
/// </summary>
public static class ChargeScenarios
{
    /// <summary>The card whose charge settles at the acquirer while the answer is lost.</summary>
    public const string UnansweredCard = "9999";

    public static ChargeResponse Decide(string gatewayName, string cardLast4) => gatewayName switch
    {
        "AlphaPay" => cardLast4 switch
        {
            "0000" => Result("HardDecline", "43", "Stolen card"),
            "1111" => Result("SoftDecline", "51", "Insufficient funds"),
            "2222" => Result("SoftDecline", "51", "Route to BetaPay scenario"),
            "3333" => Result("SoftDecline", "51", "Route to GammaPay scenario"),
            _ => Approved
        },
        "BetaPay" => cardLast4 switch
        {
            "0000" => Result("HardDecline", "43", "Stolen card"),
            "2222" => Result("SoftDecline", "51", "Insufficient funds"),
            "3333" => Result("SoftDecline", "51", "Route to GammaPay scenario"),
            _ => Approved
        },
        "GammaPay" => cardLast4 switch
        {
            "0000" => Result("HardDecline", "43", "Stolen card"),
            "3333" => Result("Error", "96", "Gateway unavailable"),
            _ => Approved
        },
        _ => Approved
    };

    private static ChargeResponse Approved => Result("Approved", "00", "Approved");

    private static ChargeResponse Result(string resultType, string responseCode, string message)
        => new() { ResultType = resultType, ResponseCode = responseCode, Message = message };
}
