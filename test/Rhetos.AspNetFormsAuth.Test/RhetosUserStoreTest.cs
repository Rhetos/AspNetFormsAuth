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
using System.Threading;

namespace Rhetos.AspNetFormsAuth.Test
{
    [TestClass]
    public class RhetosUSerStoreTest
    {
        private static WebApplicationFactory<Startup> _factory;

        [ClassInitialize]
        public static void ClassInit(TestContext testContext)
        {
            _factory = new CustomWebApplicationFactory<Startup>();
        }

        [TestMethod]
        public async Task PersistIdentityUserTest()
        {
            using (var serviceScope = _factory.Services.CreateScope())
            {
                var principal = new Common.Principal { Name = "IdentityUserTest" };
                var repository = serviceScope.ServiceProvider.GetService<IRhetosComponent<Common.DomRepository>>().Value;
                repository.Common.Principal.Insert(principal);

                var userStore = serviceScope.ServiceProvider.GetService<IUserStore<IdentityUser<Guid>>>();
                var user = await userStore.FindByIdAsync(principal.ID.ToString(), new CancellationToken(false));
                Assert.AreEqual(principal.Name, user.UserName);
                Assert.AreEqual(null, user.PasswordHash);
                Assert.AreEqual(true, user.LockoutEnabled);
                Assert.AreEqual(null, user.LockoutEnd);
                Assert.AreEqual(0, user.AccessFailedCount);

                user.PasswordHash = "SomeHash";
                user.LockoutEnd = new DateTimeOffset(2020, 5, 5, 5, 5, 5, new TimeSpan(0));
                user.AccessFailedCount = 3;
                await userStore.UpdateAsync(user, new CancellationToken(false));
                var updatedUser = await userStore.FindByIdAsync(principal.ID.ToString(), new CancellationToken(false));
                Assert.AreEqual(user.PasswordHash, updatedUser.PasswordHash);
                Assert.AreEqual(user.LockoutEnd, updatedUser.LockoutEnd);
                Assert.AreEqual(user.AccessFailedCount, updatedUser.AccessFailedCount);
            }
        }

        [TestMethod]
        public async Task FindUserByNameTest()
        {
            using (var serviceScope = _factory.Services.CreateScope())
            {
                var principal = new Common.Principal { Name = "IdentityUserTest" };
                var repository = serviceScope.ServiceProvider.GetService<IRhetosComponent<Common.DomRepository>>().Value;
                repository.Common.Principal.Insert(principal);

                var userStore = serviceScope.ServiceProvider.GetService<IUserStore<IdentityUser<Guid>>>();
                var user = await userStore.FindByIdAsync(principal.ID.ToString(), new CancellationToken(false));
                user.PasswordHash = "SomeHash";
                user.LockoutEnd = new DateTimeOffset(2020, 5, 5, 5, 5, 5, new TimeSpan(0));
                user.AccessFailedCount = 3;
                await userStore.UpdateAsync(user, new CancellationToken(false));
                var updatedUser = await userStore.FindByNameAsync(principal.Name, new CancellationToken(false));

                Assert.AreEqual(user.Id, principal.ID);
                Assert.AreEqual(user.UserName, principal.Name);
                Assert.AreEqual(user.PasswordHash, updatedUser.PasswordHash);
                Assert.AreEqual(user.LockoutEnd, updatedUser.LockoutEnd);
                Assert.AreEqual(user.AccessFailedCount, updatedUser.AccessFailedCount);
            }
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            _factory.Dispose();
        }
    }
}
