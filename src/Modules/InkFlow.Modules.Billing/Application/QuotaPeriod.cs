namespace InkFlow.Modules.Billing.Application;

internal static class QuotaPeriod
{
    public static DateTimeOffset StartOfMonth(DateTimeOffset value) =>
        new(value.Year, value.Month, 1, 0, 0, 0, TimeSpan.Zero);
}
