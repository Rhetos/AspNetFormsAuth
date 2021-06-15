/*
    Copyright (C) 2014 Omega software d.o.o.

    This file is part of Rhetos.

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as
    published by the Free Software Foundation, either version 3 of the
    License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rhetos.Host.AspNet.RestApi.Filters;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Rhetos.AspNetFormsAuth
{
    public class LoginParameters
    {
        public string UserName { get; set; }
        public string Password { get; set; }
        /// <summary>
        /// "PersistCookie" parameter may be presented to users as the "Remember me" checkbox.
        /// </summary>
        public bool PersistCookie { get; set; }
    }

    public class SetPasswordParameters
    {
        public string UserName { get; set; }
        public string Password { get; set; }
        public bool IgnorePasswordStrengthPolicy { get; set; }
    }

    public class ChangeMyPasswordParameters
    {
        public string OldPassword { get; set; }
        public string NewPassword { get; set; }
    }

    public class UnlockUserParameters
    {
        public string UserName { get; set; }
    }

    public class GeneratePasswordResetTokenParameters
    {
        public string UserName { get; set; }
    }

    public class SendPasswordResetTokenParameters
    {
        public string UserName { get; set; }

        /// <summary>
        /// Used for future ISendPasswordResetToken extensibility.
        /// For example, AdditionalClientInfo may contain answers to security questions, preferred method of communication or similar user provided information.
        /// </summary>
        public Dictionary<string, string> AdditionalClientInfo { get; set; }
    }

    public class ResetPasswordParameters
    {
        public string UserName { get; set; }
        public string PasswordResetToken { get; set; }
        public string NewPassword { get; set; }
    }

    /// <summary>
    /// Provides standard web methods for user authentication (login, logout, etc.).
    /// </summary>
    [ServiceFilter(typeof(ApiExceptionFilter))]
    [ServiceFilter(typeof(ApiCommitOnSuccessFilter))]
    public class AuthenticationController : ControllerBase
    {
        private readonly AuthenticationService _authenticationService;

        public AuthenticationController(AuthenticationService authenticationService)
        {
            _authenticationService = authenticationService;
        }

        [HttpPost]
        public async Task<bool> Login([FromBody] LoginParameters parameters)
        {
            ValidateForEmptyParameters(parameters);
            return await _authenticationService.LogInAsync(parameters.UserName, parameters.Password, parameters.PersistCookie);
        }

        [HttpPost]
        public async Task Logout()
        {
            await _authenticationService.LogOutAsync();
        }

        [HttpPost]
        [Authorize]
        public async Task SetPassword([FromBody] SetPasswordParameters parameters)
        {
            ValidateForEmptyParameters(parameters);
            await _authenticationService.SetPasswordAsync(parameters.UserName, parameters.Password, parameters.IgnorePasswordStrengthPolicy);
        }

        [HttpPost]
        [Authorize]
        public async Task<bool> ChangeMyPassword([FromBody] ChangeMyPasswordParameters parameters)
        {
            ValidateForEmptyParameters(parameters);
            return await _authenticationService.ChangeMyPasswordAsync(HttpContext?.User?.Identity?.Name, parameters.OldPassword, parameters.NewPassword);
        }

        [HttpPost]
        [Authorize]
        public async Task UnlockUser([FromBody] UnlockUserParameters parameters)
        {
            ValidateForEmptyParameters(parameters);
            await _authenticationService.UnlockUserAsync(parameters.UserName);
        }

        [HttpPost]
        public async Task<string> GeneratePasswordResetToken([FromBody] GeneratePasswordResetTokenParameters parameters)
        {
            ValidateForEmptyParameters(parameters);
            return await _authenticationService.GeneratePasswordResetTokenAsync(parameters.UserName);
        }

        [HttpPost]
        public async Task SendPasswordResetToken([FromBody] SendPasswordResetTokenParameters parameters)
        {
            ValidateForEmptyParameters(parameters);
            await _authenticationService.SendPasswordResetTokenAsync(parameters.UserName, parameters.AdditionalClientInfo);
        }

        /// <summary>
        /// This method is similar to SetPassword, but there is a difference in access permissions.
        /// ResetPassword allows anonymous access, while SetPassword needs a specific authorization.
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<bool> ResetPassword([FromBody] ResetPasswordParameters parameters)
        {
            ValidateForEmptyParameters(parameters);
            return await _authenticationService.ResetPasswordAsync(parameters.UserName, parameters.NewPassword, parameters.PasswordResetToken);
        }

        private void ValidateForEmptyParameters(object parameters)
        {
            if (parameters == null)
                throw new ClientException("It is not allowed to call this authentication service method with no parameters provided.");
        }
    }
}
