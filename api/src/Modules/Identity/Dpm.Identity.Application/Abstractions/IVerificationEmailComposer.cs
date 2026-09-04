namespace Dpm.Identity.Application.Abstractions;

/// <summary>Builds and sends identity emails (verification, password reset) with app links.</summary>
public interface IVerificationEmailComposer
{
    Task SendVerificationAsync(string email, string displayName, string token, CancellationToken ct);
}
