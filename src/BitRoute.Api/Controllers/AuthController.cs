using BitRoute.Application;
using BitRoute.Application.Auth;
using BitRoute.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace BitRoute.Api.Controllers;

/// <summary>
/// Transport only: bind the request, call the auth service, wrap the result in the
/// response envelope. All rules and failures live below; the ExceptionMiddleware
/// turns thrown domain exceptions into error envelopes.
/// </summary>
[ApiController]
[Route("auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth)
    {
        _auth = auth;
    }

    /// <summary>Creates a passenger account and starts a session (auto-login).</summary>
    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<AuthResult>>> Register(
        RegisterRequest request, CancellationToken cancellationToken)
        // 201 without a Location header: there is no GET /users/{id} endpoint yet,
        // and CreatedAtAction must never point at an action that does not exist.
        => StatusCode(
            StatusCodes.Status201Created,
            await _auth.RegisterAsync(request, cancellationToken));

    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<AuthResult>>> Login(
        LoginRequest request, CancellationToken cancellationToken)
        => Ok(await _auth.LoginAsync(request, cancellationToken));

    /// <summary>Rotates the refresh token; a replayed token revokes the whole session.</summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<ApiResponse<AuthResult>>> Refresh(
        RefreshRequest request, CancellationToken cancellationToken)
        => Ok(await _auth.RefreshAsync(request, cancellationToken));

    [HttpPost("logout")]
    public async Task<ActionResult<ApiResponse<object>>> Logout(
        LogoutRequest request, CancellationToken cancellationToken)
        => Ok(await _auth.LogoutAsync(request, cancellationToken));

    /// <summary>Provisions a new operator user account (admin-only).</summary>
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    [HttpPost("operators")]
    public async Task<ActionResult<ApiResponse<AuthUser>>> ProvisionOperator(
        ProvisionOperatorRequest request, CancellationToken cancellationToken)
        => Ok(await _auth.ProvisionOperatorAsync(request, cancellationToken));

    /// <summary>
    /// The caller's identity as the bearer token proves it. Exists to verify the JWT
    /// pipeline end to end; claims come straight from the validated token, never a body.
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    public ActionResult<ApiResponse<object>> Me() => Ok(ApiResponse.Success<object>(
        new
        {
            Id = User.Identity!.Name,
            Email = User.FindFirst(JwtRegisteredClaimNames.Email)?.Value,
            Roles = User.FindAll("role").Select(c => c.Value),
        },
        "Authenticated."));
}
