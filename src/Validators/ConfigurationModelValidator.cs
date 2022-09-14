using System;
using System.Globalization;
using System.Threading.Tasks;
using FluentValidation;
using Nop.Plugin.Payments.Xumm.Models;
using Nop.Plugin.Payments.Xumm.Services;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;
using XUMM.NET.SDK.Extensions;

namespace Nop.Plugin.Payments.Xumm.Validators;

/// <summary>
/// Represents a validator for <see cref="ConfigurationModel" />
/// </summary>
public class ConfigurationModelValidator : BaseNopValidator<ConfigurationModel>
{
    #region Fields

    private readonly IXummService _xummService;

    #endregion

    #region Ctor

    public ConfigurationModelValidator(ILocalizationService localizationService, IXummService xummService)
    {
        _xummService = xummService;

        // API Settings section
        RuleFor(model => model.ApiKey)
            .NotEmpty()
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.Xumm.Fields.ApiKey.Required"));

        RuleFor(x => x.ApiKey).Must((x, context) =>
            {
                if (string.IsNullOrEmpty(x.ApiKey))
                    return true;

                return Guid.TryParse(x.ApiKey, out _);
            })
           .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.Xumm.Fields.ApiKey.Invalid"));

        RuleFor(model => model.ApiSecret)
            .NotEmpty()
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.Xumm.Fields.ApiSecret.Required"));

        RuleFor(x => x.ApiSecret).Must((x, context) =>
            {
                if (string.IsNullOrEmpty(x.ApiSecret))
                    return true;

                return Guid.TryParse(x.ApiSecret, out _);
            })
           .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.Xumm.Fields.ApiSecret.Invalid"));

        // XRPL Settings section
        WhenAsync(async (x, ct) => await ValidApiCredentialsAsync(), () =>
        {
            RuleFor(x => x.XrplAddress).Must((x, context) =>
                {
                    if (string.IsNullOrEmpty(x.XrplAddress))
                        return true;

                    return x.XrplAddress.IsAccountAddress();
                })
                .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.Xumm.Fields.XrplAddress.Invalid"));

            RuleFor(x => x.XrplPaymentDestinationTag).Must((x, context) =>
                {
                    if (string.IsNullOrEmpty(x.XrplPaymentDestinationTag))
                        return true;

                    return uint.TryParse(x.XrplPaymentDestinationTag, NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out _);
                })
                .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.Xumm.Fields.XrplDestinationTag.Invalid"));

            RuleFor(x => x.XrplRefundDestinationTag).Must((x, context) =>
                {
                    if (string.IsNullOrEmpty(x.XrplRefundDestinationTag))
                        return true;

                    return uint.TryParse(x.XrplRefundDestinationTag, NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out _);
                })
                .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.Xumm.Fields.XrplDestinationTag.Invalid"));
        });

        // Additional Settings section
        RuleFor(model => model.AdditionalFee)
            .GreaterThanOrEqualTo(0)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.Xumm.Fields.AdditionalFee.ShouldBeGreaterThanOrEqualZero"));
    }


    private async Task<bool> ValidApiCredentialsAsync()
    {
        var pong = await _xummService.GetPongAsync();
        return pong?.Pong ?? false;
    }
    #endregion
}
