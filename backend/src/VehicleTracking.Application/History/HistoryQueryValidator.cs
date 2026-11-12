using FluentValidation;
using Microsoft.Extensions.Options;
using VehicleTracking.Application.Common;
using VehicleTracking.Application.Storage;

namespace VehicleTracking.Application.History;

/// <summary>Geçmiş sorgusunun aralık, sayfa boyutu ve imleç kuralları.</summary>
public sealed class HistoryQueryValidator : AbstractValidator<ResolvedHistoryQuery>
{
    public HistoryQueryValidator(IOptions<StorageOptions> storageOptions, IOptions<HistoryOptions> historyOptions)
    {
        HistoryOptions options = historyOptions.Value;
        TimeSpan retention = storageOptions.Value.Retention;

        RuleFor(query => query.Start)
            .Must((query, start) => start >= query.WindowStart - options.ClockSkewTolerance)
            .WithMessage(UserMessages.OutsideRetention(retention))
            .OverridePropertyName("from");
        RuleFor(query => query.End)
            .GreaterThan(query => query.Start)
            .WithMessage(UserMessages.EndMustBeAfterStart)
            .OverridePropertyName("to");
        RuleFor(query => query.PageSize)
            .InclusiveBetween(HistoryOptions.MinPageSize, options.MaxPageSize)
            .WithMessage(UserMessages.PageSizeOutOfRange(options.MaxPageSize))
            .OverridePropertyName("pageSize");
        RuleFor(query => query.After)
            .NotNull()
            .WithMessage(UserMessages.InvalidCursor)
            .OverridePropertyName("cursor")
            .When(query => query.Cursor is not null);
    }
}
