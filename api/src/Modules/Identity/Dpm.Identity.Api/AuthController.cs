using Dpm.Identity.Application.Contracts;
using Dpm.Identity.Application.Login;
using Dpm.Identity.Application.Logout;
using Dpm.Identity.Application.Refresh;
using Dpm.Identity.Application.Register;
using Dpm.Identity.Application.VerifyEmail;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Dpm.Identity.Api;

public sealed record RegisterRequest(string Email, string Password, string DisplayName);

public sealed record LoginRequest(string Email, string Password);

public sealed record VerifyEmailRequest(string Token);

public sealed record ResendVerificationRequest(string Email);

public sealed record AuthResponse(string AccessToken, int ExpiresInSeconds, UserDto User);

[ApiController]
[Route("api/v1/auth")]
[EnableRateLimiting("auth")]
public sealed class AuthController(ISender sender) : ControllerBase
{
    private const string RefreshCookieName = "dpm_refresh";

    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        var result = await sender.Send(
            new RegisterUserCommand(request.Email, request.Password, request.DisplayName), ct);
        return result.IsSuccess
            ? StatusCode(
                StatusCodes.Status201Created,
                new { message = "Check your email to verify your account." })
            : ApiResults.Problem(result.Error, HttpContext);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new LoginCommand(request.Email, request.Password), ct);
        if (result.IsFailure)
        {
            return ApiResults.Problem(result.Error, HttpContext);
        }

        SetRefreshCookie(result.Value.RefreshToken);
        return Ok(ToAuthResponse(result.Value));
    }

    [HttpPost("token/refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken ct)
    {
        if (!Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken)
            || string.IsNullOrWhiteSpace(refreshToken))
        {
            return ApiResults.Problem(
                Dpm.Identity.Application.IdentityErrors.InvalidToken, HttpContext);
        }

        var result = await sender.Send(new RefreshTokenCommand(refreshToken), ct);
        if (result.IsFailure)
        {
            ClearRefreshCookie();
            return ApiResults.Problem(result.Error, HttpContext);
        }

        SetRefreshCookie(result.Value.RefreshToken);
        return Ok(ToAuthResponse(result.Value));
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Logout(CancellationToken ct)
    {
        if (Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken)
            && !string.IsNullOrWhiteSpace(refreshToken))
        {
            await sender.Send(new LogoutCommand(refreshToken), ct);
        }

        ClearRefreshCookie();
        return NoContent();
    }

    [HttpPost("email/verify")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> VerifyEmail(VerifyEmailRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new VerifyEmailCommand(request.Token), ct);
        return result.IsSuccess
            ? Ok(new { message = "Email verified." })
            : ApiResults.Problem(result.Error, HttpContext);
    }

    [HttpPost("email/resend")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> ResendVerification(ResendVerificationRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new ResendVerificationCommand(request.Email), ct);
        return result.IsSuccess
            ? Accepted(new { message = "If the account exists, a verification email was sent." })
            : ApiResults.Problem(result.Error, HttpContext);
    }

    private static AuthResponse ToAuthResponse(AuthTokensDto tokens) =>
        new(tokens.AccessToken, tokens.ExpiresInSeconds, tokens.User);

    private void SetRefreshCookie(string refreshToken) =>
        Response.Cookies.Append(RefreshCookieName, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/api/v1/auth",
            MaxAge = TimeSpan.FromDays(14),
        });

    private void ClearRefreshCookie() =>
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions { Path = "/api/v1/auth" });
}
