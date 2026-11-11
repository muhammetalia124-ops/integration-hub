using System.ComponentModel.DataAnnotations;

namespace IntegrationHub.Application.DTOs;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class EmailAddressWithDomainAttribute : ValidationAttribute
{
    public EmailAddressWithDomainAttribute()
    {
        ErrorMessage = "Lütfen geçerli bir e-posta adresi girin.";
    }

    public override bool IsValid(object? value)
    {
        if (value is not string email || string.IsNullOrWhiteSpace(email))
        {
            return true;
        }

        if (!new EmailAddressAttribute().IsValid(email))
        {
            return false;
        }

        var domain = email[(email.LastIndexOf('@') + 1)..];
        var labels = domain.Split('.');
        return labels.Length >= 2 &&
               labels[^1].Length >= 2 &&
               labels[^1].All(char.IsLetter);
    }
}
