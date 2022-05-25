using FluentValidation;
using Nop.Plugin.Payments.Xumm.Models;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.Payments.Xumm.Validators;

/// <summary>
/// Represents a validator for <see cref="ConfigurationModel" />
/// </summary>
public class ConfigurationModelValidator : BaseNopValidator<ConfigurationModel>
{
    #region Ctor

    public ConfigurationModelValidator(
        ILocalizationService localizationService)
    {
        RuleFor(model => model.ApiKey)
            .NotEmpty()
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.Xumm.Fields.ApiKey.Required"));

        RuleFor(model => model.ApiSecret)
            .NotEmpty()
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.Xumm.Fields.ApiSecret.Required"));

        RuleFor(model => model.AdditionalFee)
            .GreaterThanOrEqualTo(0)
            .WithMessageAwait(
                localizationService.GetResourceAsync(
                    "Plugins.Payments.Xumm.Fields.AdditionalFee.ShouldBeGreaterThanOrEqualZero"));
    }

    #endregion
}
