namespace Sinalo.Domain;

public sealed record SaturdayWindow(DateOnly Previous, DateOnly Current, DateOnly Next)
{
    public IReadOnlyList<DateOnly> InPriorityOrder => [Previous, Current, Next];

    public static SaturdayWindow From(DateOnly date)
    {
        var daysUntilSaturday = ((int)DayOfWeek.Saturday - (int)date.DayOfWeek + 7) % 7;
        var current = date.AddDays(daysUntilSaturday);

        return new SaturdayWindow(current.AddDays(-7), current, current.AddDays(7));
    }
}
