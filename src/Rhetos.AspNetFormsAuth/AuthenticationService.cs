﻿/*
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

using Microsoft.AspNetCore.Identity;
using Rhetos.Dom.DefaultConcepts;
using Rhetos.Host.AspNet;
using Rhetos.Logging;
using Rhetos.Security;
using Rhetos.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Rhetos.AspNetFormsAuth
{
    public class AuthenticationService
    {
        public const int DefaultTokenExpirationInMinutes = 1440;

        private readonly ILogger _logger;
        private readonly Lazy<IAuthorizationManager> _authorizationManager;
        private readonly Lazy<IEnumerable<IPasswordStrength>> _passwordStrengthRules;
        private readonly Lazy<ISqlExecuter> _sqlExecuter;
        private readonly Lazy<ISendPasswordResetToken> _sendPasswordResetTokenPlugin;
        private readonly ILocalizer _localizer;
        private readonly SignInManager<IdentityUser<Guid>> _signInManager;
        private readonly UserManager<IdentityUser<Guid>> _userManager;

        public AuthenticationService(
            IRhetosComponent<ILogProvider> logProvider,
            IRhetosComponent<Lazy<IAuthorizationManager>> authorizationManager,
            IRhetosComponent<GenericRepositories> repositories,
            IRhetosComponent<Lazy<ISqlExecuter>> sqlExecuter,
            IRhetosComponent<Lazy<IEnumerable<ISendPasswordResetToken>>> sendPasswordResetTokenPlugins,
            IRhetosComponent<ILocalizer> localizer,
            SignInManager<IdentityUser<Guid>> signInManager,
            UserManager<IdentityUser<Guid>> userManager,
            AuthenticationServiceOptions authenticationServiceOptions)
        {
            _logger = logProvider.Value.GetLogger("AspNetFormsAuth.AuthenticationService");
            _authorizationManager = authorizationManager.Value;
            _sqlExecuter = sqlExecuter.Value;
            _sendPasswordResetTokenPlugin = new Lazy<ISendPasswordResetToken>(() => SinglePlugin(sendPasswordResetTokenPlugins.Value));
            _signInManager = signInManager;
            _userManager = userManager;

            _passwordStrengthRules = new Lazy<IEnumerable<IPasswordStrength>>(() => {
                if (authenticationServiceOptions.UseRegexRulesForPasswordStrengthCheck)
                    return repositories.Value.Load<IPasswordStrength>();
                else
                    return new List<IPasswordStrength>();
            });

            _localizer = localizer.Value;
        }

        private ISendPasswordResetToken SinglePlugin(Lazy<IEnumerable<ISendPasswordResetToken>> plugins)
        {
            if (plugins.Value.Count() == 0)
                throw new UserException("Sending the password reset token is not enabled on this server (the required plugin is not registered).");

            if (plugins.Value.Count() > 1)
                throw new FrameworkException("There is more than one plugin registered for sending the password reset token: "
                    + string.Join(", ", plugins.Value.Select(plugin => plugin.GetType().FullName)) + ".");

            return plugins.Value.Single();
        }

        private void CheckPermissions(Claim claim, string userName)
        {
            bool allowed = _authorizationManager.Value.GetAuthorizations(new[] { claim }).Single();
            if (!allowed)
                throw new UserException(
                    "You are not authorized for action '{0}' on resource '{1}', user '{2}'. The required security claim is not set.",
                    new[] { claim.Right, claim.Resource, userName },
                    null, null);
        }

        public async Task<bool> LogIn(string userName, string password, bool rememberMe)
        {
            _logger.Trace(() => $"Login: {userName}");

            ValidateNonEmptyString(userName, nameof(userName));
            ValidateNonEmptyString(password, nameof(password));

            var logInSucceeded = false;
            await SafeExecute(
                async () =>
                {
                    var loginResult = await _signInManager.PasswordSignInAsync(userName, password, rememberMe, lockoutOnFailure: true);
                    logInSucceeded = loginResult.Succeeded;
                }, "Login", userName);
            return logInSucceeded;
        }

        public async Task LogOut()
        {
            _logger.Trace(() => $"LogOut");

            await SafeExecute(
                async () => await _signInManager.SignOutAsync(),
                "Logout", "");
            ;
        }

        public async Task SetPassword(string userName, string password, bool ignorePasswordStrengthPolicy)
        {
            _logger.Trace(() => "SetPassword: " + password);

            CheckPermissions(AuthenticationServiceClaims.SetPasswordClaim, userName);

            ValidateNonEmptyString(userName, nameof(userName));
            ValidateNonEmptyString(password, nameof(password));

            if (ignorePasswordStrengthPolicy)
                CheckPermissions(AuthenticationServiceClaims.IgnorePasswordStrengthPolicyClaim, userName);
            else
                CheckPasswordStrength(password);

            await SafeExecute(
                async () => {
                    var user = await _userManager.FindByNameAsync(userName);
                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                    return await _userManager.ResetPasswordAsync(user, token, password);
                }, "Set password", userName);
        }

        public async Task<bool> ChangeMyPassword(string userName, string oldPassword, string newPassword)
        {
            _logger.Trace(() => "ChangeMyPassword");

            ValidateNonEmptyString(userName, nameof(userName));
            ValidateNonEmptyString(oldPassword, nameof(oldPassword));
            ValidateNonEmptyString(newPassword, nameof(newPassword));

            CheckPasswordStrength(newPassword);

            return await SafeExecute(
                async () => {
                    var user = await _userManager.FindByNameAsync(userName);
                    return await _userManager.ChangePasswordAsync(user, oldPassword, newPassword);
                }, "ChangeMyPassword", userName);
        }

        private void CheckPasswordStrength(string password)
        {
            foreach (var rule in _passwordStrengthRules.Value)
            {
                var regex = new Regex(rule.RegularExpression);
                if (!regex.IsMatch(password))
                {
                    _logger.Trace("CheckPasswordStrength failed on regular expression '" + rule.RegularExpression + "'.");
                    throw new UserException(rule.RuleDescription);
                }
            }
        }

        public async Task UnlockUser(string userName)
        {
            _logger.Trace(() => "UnlockUser: " + userName);
            ValidateNonEmptyString(userName, nameof(userName));
            CheckPermissions(AuthenticationServiceClaims.UnlockUserClaim, userName);

            await SafeExecute(
                async () =>
                {
                    var user = await _userManager.FindByNameAsync(userName);
                    return await _userManager.SetLockoutEndDateAsync(user, new DateTimeOffset(DateTime.UtcNow));
                }, "Unlock user", userName);
        }

        public async Task<string> GeneratePasswordResetToken(string userName)
        {
            _logger.Trace(() => "GeneratePasswordResetToken: " + userName);
            CheckPermissions(AuthenticationServiceClaims.GeneratePasswordResetTokenClaim, userName);
            ValidateNonEmptyString(userName, nameof(userName));
            return await GeneratePasswordResetTokenInternal(userName);
        }

        private async Task<string> GeneratePasswordResetTokenInternal(string userName)
        {
            if (!DoesUserExists(userName)) // Providing this information is not a security issue, because this method requires admin credentials (GeneratePasswordResetTokenClaim).
                throw new UserException("User '{0}' is not registered.", new[] { userName }, null, null);

            var user = await _userManager.FindByNameAsync(userName);
            return await _userManager.GeneratePasswordResetTokenAsync(user);
        }

        public async Task SendPasswordResetToken(string userName, Dictionary<string, string> additionalClientInfo)
        {
            _logger.Trace("SendPasswordResetToken " + userName);
            ValidateNonEmptyString(userName, nameof(userName));
            const string logErrorFormat = "SendPasswordResetToken failed for {0}: {1}";

            try
            {
                var user = await _userManager.FindByNameAsync(userName);
                var passwordResetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
                // The plugin may choose it's own client error messages (UserException and ClientException will not be suppressed).
                _sendPasswordResetTokenPlugin.Value.SendPasswordResetToken(userName, additionalClientInfo, passwordResetToken);
            }
            catch (Exception ex)
            {
                if (ex is UserException || ex is ClientException)
                    ExceptionsUtility.Rethrow(ex);

                // Don't return an internal error to the client. Log it and return a generic error message:
                _logger.Error(logErrorFormat, userName, ex);
                throw new FrameworkException(FrameworkException.GetInternalServerErrorMessage(_localizer, ex));
            }
        }

        public async Task<bool> ResetPassword(string userName,string newPassword, string resetPasswordToken)
        {
            _logger.Trace("ResetPassword");

            ValidateNonEmptyString(userName, nameof(userName));
            ValidateNonEmptyString(newPassword, nameof(newPassword));
            ValidateNonEmptyString(resetPasswordToken, nameof(resetPasswordToken));

            CheckPasswordStrength(newPassword);

            IdentityUser<Guid> user = null;
            bool successfulReset = await SafeExecute(
                async () =>
                {
                    user = await _userManager.FindByNameAsync(userName);
                    return await _userManager.ResetPasswordAsync(user, resetPasswordToken, newPassword);
                }, "ResetPassword", userName);

            if (successfulReset && user != null)
                await SafeExecute( // Login does not need to be successful for this function to return true.
                    async () => { await _signInManager.SignInAsync(user, false); },
                    "Login after ResetPassword", userName);

            return successfulReset;
        }

        bool DoesUserExists(string userName)
        {
            string sql = string.Format(DoesUserExistsSql, SqlUtility.QuoteText(userName));
            bool exists = false;
            _sqlExecuter.Value.ExecuteReader(sql, reader => exists = true);
            return exists;
        }

        const string DoesUserExistsSql =
            @"SELECT TOP 1 1 FROM Common.Principal cp
            WHERE cp.Name = {0}";

        private void ValidateNonEmptyString(string value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new UserException($"Empty {fieldName} is not allowed.");
        }

        private string GetIdentityResultErrorSummary(IdentityResult identityResult)
        {
            var errorSummary = new StringBuilder();
            if (!identityResult.Succeeded)
            {
                foreach (var identityError in identityResult.Errors)
                {
                    errorSummary.AppendLine($"Code: {identityError.Code}, Description: {identityError.Description}");
                }
            }

            return errorSummary.ToString();
        }

        /// <summary>
        /// Runs the action in a try/catch block and returns true if the execution completes without any exception
        /// and the action returns true for <see cref="IdentityResult.Succeeded"/>.
        /// Otherwise it returns false.
        /// It logs the exception to the Info log.
        /// If the <see cref="IdentityResult.Succeeded"/> is fase it logs the <see cref="IdentityResult.Errors"/> to the Trace log.
        /// </summary>
        async Task<bool> SafeExecute(Func<Task<IdentityResult>> action, string actionName, string context)
        {
            IdentityResult identityResult = IdentityResult.Failed();
            var executionSuccesfullyCompleted = await SafeExecute(
                async () =>
                {
                    identityResult = await action();
                    if (!identityResult.Succeeded)
                        _logger.Trace(() => actionName + " failed: " + context + Environment.NewLine + GetIdentityResultErrorSummary(identityResult));
                }, actionName, context);

            return executionSuccesfullyCompleted && identityResult.Succeeded;
        }

        /// <summary>
        /// Runs the action in a try/catch block and returns true if the execution completes without any exception.
        /// Otherwise it returns false.
        /// It logs the exception to the Info log.
        /// </summary>
        async Task<bool> SafeExecute(Func<Task> action, string actionName, string context)
        {
            try
            {
                await action();
                return true;
            }
            catch (Exception ex)
            {
                _logger.Info(() => actionName + " failed: " + context + ", " + ex);
                return false;
            }
        }
    }
}