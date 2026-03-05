using Microsoft.AspNetCore.Mvc;
using SaaSPlatform.Application.Common;
using SaaSPlatform.Application.DTOs;
using SaaSPlatform.Application.Interfaces;

namespace SaaSPlatform.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    [HttpPost("users/create")]
    public async Task<IActionResult> CreateUser(CreateUserRequest req, CancellationToken ct)
    {
        var result = await _auth.CreateUserAsync(req, ct);
        return Ok(ApiResponse<AuthResponse>.Ok(result, "User created successfully."));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req, CancellationToken ct)
    {
        var result = await _auth.LoginAsync(req, ct);
        return Ok(ApiResponse<AuthResponse>.Ok(result, "Logged in successfully."));
    }
}