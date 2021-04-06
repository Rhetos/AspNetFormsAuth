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
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Rhetos.Host.AspNet;
using System.Linq;
using Rhetos.Dom.DefaultConcepts;
using Rhetos.Persistence;
using System.Net;

namespace Rhetos.AspNetFormsAuth.Test
{
    [TestClass]
    public class AuthenticationProcessTest
    {
        private static WebApplicationFactory<Startup> _factory;

        [ClassInitialize]
        public static void ClassInit(TestContext testContext)
        {
            _factory = new CustomWebApplicationFactory<Startup>();
        }

        [TestMethod]
        public async Task SimpleAuthenticationProcessTest()
        {
            var client = _factory.CreateClient();

            var userName = "u1";
            var initialPassword = "u1p";

            using (var serviceScope = _factory.Services.CreateScope())
            {
                var userManager = serviceScope.ServiceProvider.GetService<UserManager<IdentityUser<Guid>>>();
                var repository = serviceScope.ServiceProvider.GetService<IRhetosComponent<Common.DomRepository>>().Value;

                //Add the test user if does not exist
                if (!repository.Common.Principal.Query().Any(x => x.Name == userName))
                    repository.Common.Principal.Insert(new Common.Principal { Name = userName });

                //Set password to initial value
                var user = await userManager.FindByNameAsync(userName);
                var token = await userManager.GeneratePasswordResetTokenAsync(user);
                var resetPassowerdResults = await userManager.ResetPasswordAsync(user, token, initialPassword);

                Assert.IsTrue(resetPassowerdResults.Succeeded, "Initalize password failed with errors: " + string.Join(", ", resetPassowerdResults.Errors.Select(x => x.Description)));

                serviceScope.ServiceProvider.GetService<IRhetosComponent<IPersistenceTransaction>>().Value.CommitChanges();
            }

            {
                var data = new StringContent($@"{{""UserName"":""{userName}"",""Password"":""notMyPassword"",""PersistCookie"":false}}", Encoding.UTF8, "application/json");
                var response = await client.PostAsync("Resources/AspNetFormsAuth/Authentication/Login", data);
                Assert.AreEqual("false", await response.Content.ReadAsStringAsync(), "Login should fail when given invalid password.");
            }

            {
                var data = new StringContent($@"{{""UserName"":""{userName}"",""Password"":""{initialPassword}"",""PersistCookie"":false}}", Encoding.UTF8, "application/json");
                var response = await client.PostAsync("Resources/AspNetFormsAuth/Authentication/Login", data);
                Assert.AreEqual("true", await response.Content.ReadAsStringAsync(), @"Login failed. Check if Rhetos server contains test user ""u1"" with password ""u1p"".");
            }

            {
                var response = await client.GetAsync("Test/GetTest");
                var responseContent = await response.Content.ReadAsStringAsync();
                Assert.IsTrue(response.StatusCode == HttpStatusCode.OK && responseContent.Contains("test"), "Reading after login should succeed.");
            }

            {
                var data = new StringContent($@"{{""OldPassword"":""notMyOldPassword"",""NewPassword"":""{initialPassword}""}}", Encoding.UTF8, "application/json");
                var response = await client.PostAsync("Resources/AspNetFormsAuth/Authentication/ChangeMyPassword", data);
                Assert.AreEqual("false", await response.Content.ReadAsStringAsync(), "ChangeMyPassword should fail when given invalid old password.");
            }

            {
                var data = new StringContent($@"{{""OldPassword"":""{initialPassword}"",""NewPassword"":""u1pp""}}", Encoding.UTF8, "application/json");
                var response = await client.PostAsync("Resources/AspNetFormsAuth/Authentication/ChangeMyPassword", data);
                Assert.AreEqual("true", await response.Content.ReadAsStringAsync(), "ChangeMyPassword failed");
            }

            {
                var data = new StringContent($@"{{""OldPassword"":""u1pp"",""NewPassword"":""{initialPassword}""}}", Encoding.UTF8, "application/json");
                var response = await client.PostAsync("Resources/AspNetFormsAuth/Authentication/ChangeMyPassword", data);
                Assert.AreEqual("true", await response.Content.ReadAsStringAsync(), "ChangeMyPassword failed");
            }

            {
                var data = new StringContent($@"{{""UserName"":""{userName}"",""Password"":""{initialPassword}""}}", Encoding.UTF8, "application/json");
                var response = await client.PostAsync("Resources/AspNetFormsAuth/Authentication/SetPassword", data);
                var responseContent = await response.Content.ReadAsStringAsync();
                Assert.IsFalse(response.StatusCode != HttpStatusCode.BadRequest || !responseContent.Contains("AspNetFormsAuth.AuthenticationService") || !responseContent.Contains("SetPassword"),
                    "Test user should not have rights to call SetPassword.");
            }

            await client.PostAsync("Resources/AspNetFormsAuth/Authentication/Logout", new StringContent(""));

            {
                var response = await client.GetAsync("Test/GetTest");
                Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode, "Reading after logout should fail.");
            }

            {
                var data = new StringContent($@"{{""UserName"":""{userName}"",""Password"":""{initialPassword}""}}", Encoding.UTF8, "application/json");
                var response = await client.PostAsync("Resources/AspNetFormsAuth/Authentication/SetPassword", data);
                var responseContent = await response.Content.ReadAsStringAsync();
                Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode, "ChangeMyPassword without logging in should fail.");
            }

            {
                var data = new StringContent($@"{{""OldPassword"":""{initialPassword}"",""OldPassword"":u1pp}}", Encoding.UTF8, "application/json");
                var response = await client.PostAsync("Resources/AspNetFormsAuth/Authentication/ChangeMyPassword", data);
                var responseContent = await response.Content.ReadAsStringAsync();
                Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode, "SetPassword without logging in should fail.");
            }
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            _factory.Dispose();
        }
    }
}
