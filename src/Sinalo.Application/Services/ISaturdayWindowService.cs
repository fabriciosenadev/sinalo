using Sinalo.Domain;

namespace Sinalo.Application.Services;

public interface ISaturdayWindowService
{
    SaturdayWindow GetWindow(DateOnly referenceDate);
}
