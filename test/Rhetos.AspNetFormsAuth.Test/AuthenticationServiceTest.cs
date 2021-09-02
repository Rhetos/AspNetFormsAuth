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

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.DependencyInjection;
using Rhetos.AspNetFormsAuth.TestApp;
using System;
using System.Threading.Tasks;
using Rhetos.Host.AspNet;
using Rhetos.Dom.DefaultConcepts;
using Rhetos.Persistence;
using Rhetos.Utilities;
using Rhetos.Security;
using System.Linq;
using System.Collections.Generic;

namespace Rhetos.AspNetFormsAuth.Test
{
    [TestClass]
    public class AuthenticationServiceTest
    {
        private static WebApplicationFactory<Startup> _factory;

        [ClassInitialize]
        public static void ClassInit(TestContext testContext)
        {
            _factory = new CustomWebApplicationFactory<Startup>();
        }

        [TestMethod]
        public async Task LoginWithoutPrincipalTest()
        {
            var client = new HttpClientAuthenticationHelper(_factory.CreateClient());
            Assert.IsFalse((await client.Login(GetRandomUserName(), "test")).Value, "User with not Principal should not be able to log in.");
        }

        [TestMethod]
        public async Task LoginWithoutMembershiplTest()
        {
            var user = await CreateRandomUser(false);
            var client = new HttpClientAuthenticationHelper(_factory.CreateClient());
            Assert.IsFalse((await client.Login(user.UserName, "test")).Value, "User without membership should not be able to log in.");
        }

        [TestMethod]
        public async Task LoginWithUserTest()
        {
            var user = await CreateRandomUser(true);
            var client = new HttpClientAuthenticationHelper(_factory.CreateClient());
            Assert.IsTrue((await client.Login(user.UserName, user.Password)).Value);
        }

        [TestMethod]
        public async Task UnlockUserTest()
        {
            var user = await CreateRandomUser(true);
            var client = new HttpClientAuthenticationHelper(_factory.CreateClient());
            for (var i = 0; i < 5; i++)
                await client.Login(user.UserName, "NotMyPAssword");

            Assert.IsFalse((await client.Login(user.UserName, user.Password)).Value, "The user should be locked out");

            var adminUser = await CreateRandomUser(true);
            await client.Login(adminUser.UserName, adminUser.Password);
            var unlockResponse = await client.UnlockUser(adminUser.UserName);
            Assert.IsTrue(unlockResponse.Error.ContainsAll("You are not authorized for action", "claim is not set"));

            AddClaimToUser(adminUser.UserName, AuthenticationServiceClaims.UnlockUserClaim);
            var unlockResponseAfterSetClaim = await client.UnlockUser(user.UserName);
            Assert.IsFalse(unlockResponseAfterSetClaim.IsError);

            Assert.IsTrue((await client.Login(user.UserName, user.Password)).Value, "The user should be able to log in after the account has been unlocked.");
        }

        [TestMethod]
        public async Task MethodsThatNeedAuthorizationTest()
        {
            var client = new HttpClientAuthenticationHelper(_factory.CreateClient());
            ShouldReturnUnathorized(await client.ChangeMyPassword("test", "test"));
            ShouldReturnUnathorized(await client.SetPassword("test", "test", false));
            ShouldReturnUnathorized(await client.UnlockUser("test"));
        }

        private void ShouldReturnUnathorized<TResponse>(ValueOrError<TResponse> response)
        {
            if (!response.IsError)
                Assert.Fail("The response should return an error.");
            Assert.IsTrue(response.Error.StartsWith("(401)"), "The rsponse from the server should be Unathorized.");
        }

        [TestMethod]
        public async Task CheckForClaimsTest()
        {
            var client = new HttpClientAuthenticationHelper(_factory.CreateClient());
            var user = await CreateRandomUser(true);

            await client.Login(user.UserName, user.Password);

            ShouldReturnRequiresClaim(await client.SetPassword(user.UserName, "test", false), AuthenticationServiceClaims.SetPasswordClaim);
            AddClaimToUser(user.UserName, AuthenticationServiceClaims.SetPasswordClaim);
            ShouldReturnRequiresClaim(await client.SetPassword(user.UserName, "test", true), AuthenticationServiceClaims.IgnorePasswordStrengthPolicyClaim);
            ShouldReturnRequiresClaim(await client.UnlockUser(user.UserName), AuthenticationServiceClaims.UnlockUserClaim);
            ShouldReturnRequiresClaim(await client.GeneratePasswordResetToken(user.UserName), AuthenticationServiceClaims.GeneratePasswordResetTokenClaim);
        }

        private void ShouldReturnRequiresClaim<TResponse>(ValueOrError<TResponse> response, Claim claim)
        {
            if (!response.IsError)
                Assert.Fail("The response should return an error.");
            Assert.IsTrue(response.Error.ContainsAll("(400)", $"not authorized for action '{claim.Right}' on resource '{claim.Resource}'"), 
                $"The {claim.Resource}.{claim.Right} should be required for this action.");
        }

        [TestMethod]
        public async Task SendPasswordResetTokenTest()
        {
            var user = await CreateRandomUser(true);
            var client = new HttpClientAuthenticationHelper(_factory.CreateClient());

            var additionalInfo = new Dictionary<string, string> { { "test", "test" } };
            await client.SendPasswordResetToken(user.UserName, additionalInfo);

            var lastSentToken = SendPasswordResetTokenMock.SentTokens.Last();
            Assert.AreEqual("test", lastSentToken.additionalClientInfo["test"]);

            var newPassword = GetRandomPassword();
            Assert.IsTrue((await client.ResetPassword(user.UserName, lastSentToken.passwordResetToken, newPassword)).Value);
            //Assert.IsTrue((await client.Login(user.UserName, newPassword)).Value);
        }

        [TestMethod]
        public async Task GeneratePasswordResetTokenTest()
        {
            var adminUser = await CreateRandomUser(true);
            var user = await CreateRandomUser(true);
            var newPassword = GetRandomPassword();
            string token;

            {
                var client = new HttpClientAuthenticationHelper(_factory.CreateClient());

                AddClaimToUser(adminUser.UserName, AuthenticationServiceClaims.GeneratePasswordResetTokenClaim);
                await client.Login(adminUser.UserName, adminUser.Password);
                token = (await client.GeneratePasswordResetToken(user.UserName)).Value;
            }

            {
                var client = new HttpClientAuthenticationHelper(_factory.CreateClient());
                await client.ResetPassword(user.UserName, token, newPassword);
                Assert.IsTrue((await client.Login(user.UserName, newPassword)).Value);
            }
        }

        [TestMethod]
        public async Task PasswordStrengthTest()
        {
            var passwordRuleDescription = "Starts with a and has at least three characters";
            using (var serviceScope = _factory.Server.Services.CreateScope())
            {
                var repository = serviceScope.ServiceProvider.GetService<IRhetosComponent<Common.DomRepository>>().Value;
                repository.Common.AspNetFormsAuthPasswordStrength.Insert(new Common.AspNetFormsAuthPasswordStrength
                {
                    RegularExpression = "a...",
                    RuleDescription = passwordRuleDescription
                });

                var authenticationService = serviceScope.ServiceProvider.GetService<AuthenticationService>();
                var user = await CreateRandomUser(true);
                Exception exception = null;
                try
                {
                    await authenticationService.ChangeMyPasswordAsync(user.UserName, user.Password, "test");
                }
                catch (Exception e)
                {
                    exception = e;
                }

                Assert.IsTrue(exception is UserException && (exception as UserException).Message.Contains(passwordRuleDescription));
            }
        }

        [TestMethod]
        public async Task OverrideDeafultConfigurationTest()
        {
            var factoryWithCustomConfiguration = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddIdentityCore<IdentityUser<Guid>>(options =>
                    {
                        options.Password.RequiredLength = 10;
                    });
                });
            });

            using (var serviceScope = factoryWithCustomConfiguration.Server.Services.CreateScope())
            {
                var repository = serviceScope.ServiceProvider.GetService<IRhetosComponent<Common.DomRepository>>().Value;
                var authenticationService = serviceScope.ServiceProvider.GetService<AuthenticationService>();
                var userManager = serviceScope.ServiceProvider.GetService<UserManager<IdentityUser<Guid>>>();
                var userName = "Test";
                repository.Common.Principal.Insert(new Common.Principal { Name = userName });

                var password = "12345";
                var user = await userManager.FindByNameAsync(userName);
                var addPasswordResult = await userManager.AddPasswordAsync(user, password);
                Assert.IsTrue(addPasswordResult.Errors.Any(x => x.Code == "PasswordTooShort"), "Adding password should fail because it is configured to be at least 10 characters long.");
            }
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            using (var serviceScope = _factory.Server.Services.CreateScope())
            {
                var sqlExecuter = serviceScope.ServiceProvider.GetService<IRhetosComponent<ISqlExecuter>>().Value;

                sqlExecuter.ExecuteSql($@"
                    DELETE pp
                    FROM 
                        Common.PrincipalPermission pp
                        INNER JOIN Common.Principal p ON pp.PrincipalID = p.ID
                    WHERE p.Name like '{RandomNamePrefix}%';

                    DELETE m
                    FROM 
                        dbo.webpages_Membership m
                        INNER JOIN Common.Principal p ON p.AspNetUserId = m.UserId
                    WHERE p.Name like '{RandomNamePrefix}%';

                    DELETE p
                    FROM 
                        Common.Principal p
                    WHERE p.Name like '{RandomNamePrefix}%';");

                serviceScope.ServiceProvider.GetService<IRhetosComponent<IUnitOfWork>>().Value.CommitAndClose();
            }

            _factory.Dispose();
        }

        private async Task<(string UserName, string Password)> CreateRandomUser(bool withMembership)
        {
            var randomUserName = GetRandomUserName();
            string password = null;

            using (var serviceScope = _factory.Server.Services.CreateScope())
            {
                var authenticationService = serviceScope.ServiceProvider.GetService<AuthenticationService>();
                var userManager = serviceScope.ServiceProvider.GetService<UserManager<IdentityUser<Guid>>>();
                var repository = serviceScope.ServiceProvider.GetService<IRhetosComponent<Common.DomRepository>>().Value;
                repository.Common.Principal.Insert(new Common.Principal { Name = randomUserName });

                if (withMembership)
                {
                    password = GetRandomPassword();
                    var user = await userManager.FindByNameAsync(randomUserName);
                    var addPasswordResult = await userManager.AddPasswordAsync(user, password);
                    if (!addPasswordResult.Succeeded)
                        throw new Exception("Failed to add password to user. Errors: " + string.Join(Environment.NewLine, addPasswordResult.Errors.Select(x => x.Description)));
                }

                serviceScope.ServiceProvider.GetService<IRhetosComponent<IUnitOfWork>>().Value.CommitAndClose();
            }

            return (randomUserName, password);
        }

        private void AddClaimToUser(string userName, Claim claim)
        {
            using (var serviceScope = _factory.Server.Services.CreateScope())
            {
                var repository = serviceScope.ServiceProvider.GetService<IRhetosComponent<Common.DomRepository>>().Value;

                var claimID = repository.Common.Claim.Query(x => x.ClaimResource == claim.Resource && x.ClaimRight == claim.Right).First().ID;
                var userID = repository.Common.Principal.Query(x => x.Name == userName).First().ID;

                repository.Common.PrincipalPermission.Insert(new Common.PrincipalPermission
                {
                    ClaimID = claimID,
                    PrincipalID = userID,
                    IsAuthorized = true
                }) ;

                serviceScope.ServiceProvider.GetService<IRhetosComponent<IUnitOfWork>>().Value.CommitAndClose();
            }
            AuthorizationDataCache.ClearCache();
        }

        const string RandomNamePrefix = "Test__";

        static string GetRandomUserName() => RandomNamePrefix + Guid.NewGuid().ToString();

        static string GetRandomPassword() => Guid.NewGuid().ToString();
    }
}
