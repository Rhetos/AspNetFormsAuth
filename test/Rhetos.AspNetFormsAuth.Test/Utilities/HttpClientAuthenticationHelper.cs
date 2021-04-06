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

using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net;
using System.Collections.Generic;

namespace Rhetos.AspNetFormsAuth.Test
{
    public class HttpClientAuthenticationHelper
    {
        public HttpClient HttpClient { get; private set; }

        public HttpClientAuthenticationHelper(HttpClient httpClient)
        {
            this.HttpClient = httpClient;
        }

        public async Task<ValueOrError<bool>> Login(string userName, string password, bool persistCookie = false)
        {
            var parameters = new LoginParameters
            {
                UserName = userName,
                Password = password,
                PersistCookie = persistCookie
            };

            return await SendRequest<LoginParameters, bool>(parameters, "Login");
        }

        public async Task<ValueOrError<None>> Logout()
        {
            return await SendRequestWithoutResponse(new None(), "Logout");
        }

        public async Task<ValueOrError<None>> SetPassword(string userName, string password, bool ignorePasswordStrengthPolicy)
        {
            return await SendRequestWithoutResponse(new SetPasswordParameters { UserName = userName, Password = password, IgnorePasswordStrengthPolicy = ignorePasswordStrengthPolicy }, "SetPassword");
        }

        public async Task<ValueOrError<bool>> ChangeMyPassword(string oldPassword, string newPassword)
        {
            return await SendRequest<ChangeMyPasswordParameters, bool>(new ChangeMyPasswordParameters { OldPassword = oldPassword, NewPassword = newPassword }, "ChangeMyPassword");
        }

        public async Task<ValueOrError<None>> UnlockUser(string userName)
        {
            return await SendRequestWithoutResponse(new UnlockUserParameters { UserName = userName }, "UnlockUser");
        }

        public async Task<ValueOrError<string>> GeneratePasswordResetToken(string userName)
        {
            return await SendRequestWithStringAsResponse(new GeneratePasswordResetTokenParameters { UserName = userName }, "GeneratePasswordResetToken");
        }

        public async Task<ValueOrError<None>> SendPasswordResetToken(string userName, Dictionary<string, string> additionalClientInfo = null)
        {
            return await SendRequestWithoutResponse(new SendPasswordResetTokenParameters {
                UserName = userName,
                AdditionalClientInfo = additionalClientInfo != null ? additionalClientInfo : new Dictionary<string, string>()
            }, "SendPasswordResetToken");
        }

        public async Task<ValueOrError<bool>> ResetPassword(string userName, string passwordResetToken, string newPassword)
        {
            return await SendRequest<ResetPasswordParameters, bool>(new ResetPasswordParameters { UserName = userName, PasswordResetToken = passwordResetToken, NewPassword = newPassword }, "ResetPassword");
        }

        private async Task<ValueOrError<TResponse>> SendRequestWithCustomParser<TParameters, TResponse>(TParameters parameters, string method, Func<string, TResponse> parseResponse)
        {
            var content = new StringContent(JsonSerializer.Serialize(parameters), Encoding.UTF8, "application/json");
            var results = await HttpClient.PostAsync($"Resources/AspNetFormsAuth/Authentication/{method}", content);
            var responseContent = await results.Content.ReadAsStringAsync();

            if (results.StatusCode == HttpStatusCode.OK)
            {
                return ValueOrError<TResponse>.CreateValue(parseResponse(responseContent));
            }
            else if (results.StatusCode == HttpStatusCode.BadRequest || results.StatusCode == HttpStatusCode.Unauthorized)
            {
                string errorMessage = "";
                if (results.StatusCode == HttpStatusCode.BadRequest)
                    errorMessage = JsonSerializer.Deserialize<RhetosErrorResponse>(responseContent).UserMessage;
                return ValueOrError<TResponse>.CreateError($"({(int)results.StatusCode}) {errorMessage}");
            }
            else
                throw new Exception("Response from server: " + responseContent);
        }

        private async Task<ValueOrError<TResponse>> SendRequest<TParameters, TResponse>(TParameters parameters, string method)
        {
            return await SendRequestWithCustomParser(parameters, method, response => JsonSerializer.Deserialize<TResponse>(response));
        }

        private async Task<ValueOrError<string>> SendRequestWithStringAsResponse<TParameters>(TParameters parameters, string method)
        {
            return await SendRequestWithCustomParser(parameters, method, response => response);
        }

        private async Task<ValueOrError<None>> SendRequestWithoutResponse<TParameters>(TParameters parameters, string method)
        {
            return await SendRequestWithCustomParser(parameters, method, response => new None());
        }
    }
}
