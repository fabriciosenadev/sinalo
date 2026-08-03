using Sinalo.Domain;

namespace Sinalo.Application.Configuration;

public sealed record SourceConfiguration(ContentSource Source, string DisplayName, string PageUrl, AvailabilityPolicy Policy);
