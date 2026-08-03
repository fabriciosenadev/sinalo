using Sinalo.Application.Services;
using Sinalo.Domain;

namespace Sinalo.Infrastructure;

public sealed class SaturdayWindowService : ISaturdayWindowService
{
    public SaturdayWindow GetWindow(DateOnly referenceDate) => SaturdayWindow.From(referenceDate);
}
