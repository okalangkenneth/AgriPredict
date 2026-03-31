using FluentValidation;

namespace AgriPredict.Api.Validators;

/// <summary>Represents the query parameters for the rainfall prediction endpoint.</summary>
public sealed record RainfallRequest(double Lat, double Lon, int Days);

public sealed class RainfallRequestValidator : AbstractValidator<RainfallRequest>
{
    public RainfallRequestValidator()
    {
        RuleFor(x => x.Lat)
            .InclusiveBetween(-90, 90)
            .WithMessage("Latitude must be between -90 and 90.");

        RuleFor(x => x.Lon)
            .InclusiveBetween(-180, 180)
            .WithMessage("Longitude must be between -180 and 180.");

        RuleFor(x => x.Days)
            .InclusiveBetween(1, 7)
            .WithMessage("Days must be between 1 and 7.");
    }
}
