using FluentValidation;

namespace AgriPredict.Api.Validators;

/// <summary>Represents the lat/lon query parameters shared by all spatial endpoints.</summary>
public sealed record CoordinateRequest(double Lat, double Lon);

public sealed class CoordinateValidator : AbstractValidator<CoordinateRequest>
{
    public CoordinateValidator()
    {
        RuleFor(x => x.Lat)
            .InclusiveBetween(-90, 90)
            .WithMessage("Latitude must be between -90 and 90.");

        RuleFor(x => x.Lon)
            .InclusiveBetween(-180, 180)
            .WithMessage("Longitude must be between -180 and 180.");
    }
}
