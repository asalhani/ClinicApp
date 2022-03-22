﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using ClinicApp.Api.JwtFeatures;
using EmailService;
using Entities.Appsettings;
using Entities.DTO;
using Entities.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace ClinicApp.Api.Controllers
{
	[Route("api/accounts")]
	[ApiController]
	public class AccountsController : ControllerBase
	{
		private readonly UserManager<User> _userManager;
		private readonly IMapper _mapper;
		private readonly JwtHandler _jwtHandler;
		private readonly IEmailSender _emailSender;
		private readonly IdentityConfiguration _identityConfiguration;

		public AccountsController(UserManager<User> userManager, IMapper mapper, JwtHandler jwtHandler, IEmailSender emailSender, IdentityConfiguration identityConfiguration)
		{
			_userManager = userManager;
			_mapper = mapper;
			_jwtHandler = jwtHandler;
			_emailSender = emailSender;
			_identityConfiguration = identityConfiguration;
		}

		[HttpPost("Registration")]
		public async Task<IActionResult> RegisterUser([FromBody] UserForRegistrationDto userForRegistration)
		{
			if (userForRegistration == null || !ModelState.IsValid)
				return BadRequest();

			var user = _mapper.Map<User>(userForRegistration);

			user.TwoFactorEnabled = _identityConfiguration.EnableOtp;
			user.EmailConfirmed = !_identityConfiguration.EnableEmailConfirmation;
			
			var result = await _userManager.CreateAsync(user, userForRegistration.Password);
			if (!result.Succeeded)
			{
				var errors = result.Errors.Select(e => e.Description);

				return BadRequest(new RegistrationResponseDto { Errors = errors });
			}

			if (_identityConfiguration.EnableEmailConfirmation)
			{
				var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
				var param = new Dictionary<string, string>
				{
					{"token", token },
					{"email", user.Email }
				};
				var callback = QueryHelpers.AddQueryString(userForRegistration.ClientURI, param);
				var message = new Message(new string[] { "codemazetest@gmail.com" }, "Email Confirmation token", callback, null);
				await _emailSender.SendEmailAsync(message);	
			}

			await _userManager.AddToRoleAsync(user, "Viewer");
			

			return Ok();
		}

		[HttpPost("Login")]
		public async Task<IActionResult> Login([FromBody] UserForAuthenticationDto userForAuthentication)
		{
			var user = await _userManager.FindByNameAsync(userForAuthentication.Email);
			if (user == null)
				return BadRequest("Invalid Request");

			if (!await _userManager.IsEmailConfirmedAsync(user))
				return Unauthorized(new AuthResponseDto { ErrorMessage = "Email is not confirmed" });

			//you can check here if the account is locked out in case the user enters valid credentials after locking the account.

			if (!await _userManager.CheckPasswordAsync(user, userForAuthentication.Password))
			{
				await _userManager.AccessFailedAsync(user);

				if (await _userManager.IsLockedOutAsync(user))
				{
					var content = $"Your account is locked out. To reset the password click this link: {userForAuthentication.clientURI}";
					var message = new Message(new string[] { userForAuthentication.Email }, "Locked out account information", content, null);
					await _emailSender.SendEmailAsync(message);

					return Unauthorized(new AuthResponseDto { ErrorMessage = "The account is locked out" });
				}

				return Unauthorized(new AuthResponseDto { ErrorMessage = "Invalid Authentication" });
			}

			if(_identityConfiguration.EnableOtp && await _userManager.GetTwoFactorEnabledAsync(user))
				return await GenerateOTPFor2StepVerification(user);

			var token = await _jwtHandler.GenerateToken(user);

			await _userManager.ResetAccessFailedCountAsync(user);

			return Ok(new AuthResponseDto { IsAuthSuccessful = true, Token = token });
		}

		private async Task<IActionResult> GenerateOTPFor2StepVerification(User user)
		{
			var providers = await _userManager.GetValidTwoFactorProvidersAsync(user);
			if (!providers.Contains("Email"))
			{
				return Unauthorized(new AuthResponseDto { ErrorMessage = "Invalid 2-Step Verification Provider." });
			}

			var token = await _userManager.GenerateTwoFactorTokenAsync(user, "Email");
			var message = new Message(new string[] { user.Email }, "Authentication token", token, null);
			// await _emailSender.SendEmailAsync(message);

			return Ok(new AuthResponseDto { Is2StepVerificationRequired = true, Provider = "Email" });
		}

		[HttpPost("TwoStepVerification")]
		public async Task<IActionResult> TwoStepVerification([FromBody] TwoFactorDto twoFactorDto)
		{
			if (!ModelState.IsValid)
				return BadRequest();

			var user = await _userManager.FindByEmailAsync(twoFactorDto.Email);
			if (user == null)
				return BadRequest("Invalid Request");

			var validVerification = await _userManager.VerifyTwoFactorTokenAsync(user, twoFactorDto.Provider, twoFactorDto.Token);
			if (!validVerification)
				return BadRequest("Invalid Token Verification");

			var token = await _jwtHandler.GenerateToken(user);
			return Ok(new AuthResponseDto { IsAuthSuccessful = true, Token = token });
		}

		[HttpPost("ForgotPassword")]
		public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto forgotPasswordDto)
		{
			if (!ModelState.IsValid)
				return BadRequest();

			var user = await _userManager.FindByEmailAsync(forgotPasswordDto.Email);
			if (user == null)
				return BadRequest("Invalid Request");

			var token = await _userManager.GeneratePasswordResetTokenAsync(user);
			var param = new Dictionary<string, string>
			{
				{"token", token },
				{"email", forgotPasswordDto.Email }
			};

			var callback = QueryHelpers.AddQueryString(forgotPasswordDto.ClientURI, param);

			var message = new Message(new string[] { "codemazetest@gmail.com" }, "Reset password token", callback, null);
			await _emailSender.SendEmailAsync(message);

			return Ok();
		}

		[HttpPost("ResetPassword")]
		public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto resetPasswordDto)
		{
			if (!ModelState.IsValid)
				return BadRequest();

			var user = await _userManager.FindByEmailAsync(resetPasswordDto.Email);
			if (user == null)
				return BadRequest("Invalid Request");

			var resetPassResult = await _userManager.ResetPasswordAsync(user, resetPasswordDto.Token, resetPasswordDto.Password);
			if (!resetPassResult.Succeeded)
			{
				var errors = resetPassResult.Errors.Select(e => e.Description);

				return BadRequest(new { Errors = errors });
			}

			await _userManager.SetLockoutEndDateAsync(user, new DateTime(2000, 1, 1));

			return Ok();
		}

		[HttpGet("EmailConfirmation")]
		public async Task<IActionResult> EmailConfirmation([FromQuery] string email, [FromQuery] string token)
		{
			var user = await _userManager.FindByEmailAsync(email);
			if (user == null)
				return BadRequest("Invalid Email Confirmation Request");

			var confirmResult = await _userManager.ConfirmEmailAsync(user, token);
			if (!confirmResult.Succeeded)
				return BadRequest("Invalid Email Confirmation Request");

			return Ok();
		}
	}
}
