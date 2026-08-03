namespace Sinalo.Domain;

public readonly record struct Quarter(int Year, int Number)
{
    public static Quarter From(DateOnly date) => new(date.Year, ((date.Month - 1) / 3) + 1);

    public override string ToString() => $"{Year}-T{Number}";
}
