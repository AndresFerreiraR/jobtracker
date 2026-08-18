using JobTracker.SharedKernel.Results;

namespace Jobs.Domain.Jobs;

public static class AddressErrors
{
    public static readonly Error StreetRequired =
        Error.Validation("Address.StreetRequired", "Street is required.");

    public static readonly Error CityRequired =
        Error.Validation("Address.CityRequired", "City is required.");

    public static readonly Error StateRequired =
        Error.Validation("Address.StateRequired", "State is required.");

    public static readonly Error InvalidZipCode =
        Error.Validation("Address.InvalidZipCode", "Postal code must be 3–10 alphanumeric characters (spaces and hyphens allowed).");

    public static readonly Error InvalidLatitude =
        Error.Validation("Address.InvalidLatitude", "Latitude must be within [-90, 90].");

    public static readonly Error InvalidLongitude =
        Error.Validation("Address.InvalidLongitude", "Longitude must be within [-180, 180].");
}
