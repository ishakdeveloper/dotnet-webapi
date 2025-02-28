using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using MyApi.Models;
using MyApi.Models.DTOs;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Models;

namespace MyApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IIdentityServerInteractionService _interaction;
        private readonly IClientStore _clientStore;
        private readonly IConfiguration _configuration;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IIdentityServerInteractionService interaction,
            IClientStore clientStore,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _interaction = interaction;
            _clientStore = clientStore;
            _configuration = configuration;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                // Send email confirmation token
                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                // TODO: Send email with confirmation link
                
                return Ok(new { message = "Registration successful. Please check your email for confirmation." });
            }

            return BadRequest(result.Errors);
        }

        [HttpPost("token")]
        public async Task<IActionResult> Token([FromBody] LoginDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
                return BadRequest("Invalid credentials");

            var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, false);
            if (!result.Succeeded)
                return BadRequest("Invalid credentials");

            // Create claims for the user
            var claims = new List<Claim>
            {
                new Claim("sub", user.Id),
                new Claim("email", user.Email),
                new Claim("name", $"{user.FirstName} {user.LastName}"),
                new Claim("role", "user") // Add roles as needed
            };

            // Get roles and add them as claims
            var roles = await _userManager.GetRolesAsync(user);
            foreach (var role in roles)
            {
                claims.Add(new Claim("role", role));
            }

            // Create security key
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Secret"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // Create token
            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: creds
            );

            return Ok(new
            {
                access_token = new JwtSecurityTokenHandler().WriteToken(token),
                token_type = "Bearer",
                expires_in = 3600
            });
        }

        [HttpGet("external-login")]
        public async Task<IActionResult> ExternalLogin(string provider, string returnUrl = null)
        {
            // Verify that the provider is configured
            var client = await _clientStore.FindEnabledClientByIdAsync(provider);
            if (client == null)
                return BadRequest($"Provider {provider} is not configured.");

            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, returnUrl);
            return Challenge(properties, provider);
        }

        [HttpGet("external-callback")]
        public async Task<IActionResult> ExternalCallback(string returnUrl = null)
        {
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
                return BadRequest("Error loading external login information.");

            var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false);
            if (result.Succeeded)
            {
                var user = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
                var token = await GenerateTokenAsync(user);
                return Ok(new { access_token = token });
            }

            // Create new user from external login
            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            var user2 = await _userManager.FindByEmailAsync(email);
            if (user2 == null)
            {
                user2 = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FirstName = info.Principal.FindFirstValue(ClaimTypes.GivenName) ?? "",
                    LastName = info.Principal.FindFirstValue(ClaimTypes.Surname) ?? "",
                    EmailConfirmed = true
                };

                var createResult = await _userManager.CreateAsync(user2);
                if (!createResult.Succeeded)
                    return BadRequest(createResult.Errors);
            }

            var addLoginResult = await _userManager.AddLoginAsync(user2, info);
            if (!addLoginResult.Succeeded)
                return BadRequest(addLoginResult.Errors);

            var token2 = await GenerateTokenAsync(user2);
            return Ok(new { access_token = token2 });
        }

        private async Task<string> GenerateTokenAsync(ApplicationUser user)
        {
            var claims = new List<Claim>
            {
                new Claim("sub", user.Id),
                new Claim("email", user.Email),
                new Claim("name", $"{user.FirstName} {user.LastName}")
            };

            var roles = await _userManager.GetRolesAsync(user);
            foreach (var role in roles)
            {
                claims.Add(new Claim("role", role));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Secret"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            if (User?.Identity.IsAuthenticated == true)
            {
                await _signInManager.SignOutAsync();
                await _interaction.RevokeTokensForCurrentSessionAsync();
            }

            return Ok(new { message = "Logged out successfully" });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
                return Ok(new { message = "If your email is registered, you will receive a password reset link." });

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            // TODO: Send email with reset password link

            return Ok(new { message = "If your email is registered, you will receive a password reset link." });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
                return BadRequest("Invalid request");

            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.NewPassword);
            if (result.Succeeded)
                return Ok(new { message = "Password has been reset successfully." });

            return BadRequest(result.Errors);
        }
    }
} 