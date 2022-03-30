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

using Autofac;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Rhetos;
using Rhetos.AspNetFormsAuth;
using Rhetos.Logging;
using Rhetos.Security;
using Rhetos.Utilities;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

namespace AdminSetup
{
    class App
    {
        private readonly ILogger _logger;

        public App()
        {
            _logger = new ConsoleLogger(EventType.Trace, "AdminSetup");
        }

        public int SetUpAdminAccount(string rhetosHostAssemblyPath, string password)
        {
            return SafeExecuteCommand(() =>
            {
                var hostServices = RhetosHost.GetHostServices(
                    rhetosHostAssemblyPath,
                    rhetosHostBuilder => rhetosHostBuilder.ConfigureContainer(builder => builder.RegisterType<ConsoleLogProvider>().As<ILogProvider>().SingleInstance()),
                    (hostBuilderContext, serviceCollection) => serviceCollection.AddScoped<IUserInfo, ProcessUserInfo>());

                using (var scope = hostServices.CreateScope())
                {
                    scope.ServiceProvider.GetService<IRhetosComponent<AdminUserInitializer>>().Value.Initialize();
                    SetUpAdminAccount(scope.ServiceProvider, password);
                    scope.ServiceProvider.GetService<IRhetosComponent<IUnitOfWork>>().Value.CommitAndClose();
                }
            });
        }

        private void SetUpAdminAccount(IServiceProvider scope, string password)
        {
            CheckElevatedPrivileges();

            string adminPassword = string.IsNullOrWhiteSpace(password) ? InputPassword() : password;

            const string adminUserName = AdminUserInitializer.AdminUserName;

            var userManager = scope.GetService<UserManager<IdentityUser<Guid>>>();
            if(userManager == null)
                throw new UserException($"The AspNetFormsAuth package is not configured properly. Call the {nameof(AspNetFormsAuthCollectionExtensions.AddAspNetFormsAuth)} method on the {nameof(IServiceCollection)} inside the Startup.ConfigureServices method.");
           
            var sqlExecuter = scope.GetService<IRhetosComponent<ISqlExecuter>>();

            var principalCount = 0;
            sqlExecuter.Value.ExecuteReaderInterpolated($"SELECT COUNT(*) FROM Common.Principal WHERE Name = {adminUserName}",
                reader => principalCount = reader.GetInt32(0));
            if (principalCount == 0)
                throw new UserException($"Missing '{adminUserName}' user entry in Common.Principal entity. Please execute the 'rhetos dbupdate' command, with AspNetFormsAuth package included, to initialize the 'admin' user entry.");

            var user = userManager.FindByNameAsync(adminUserName).Result;

            var token = userManager.GeneratePasswordResetTokenAsync(user).Result;
            var changedPasswordResults = userManager.ResetPasswordAsync(user, token, adminPassword).Result;

            if (!changedPasswordResults.Succeeded)
                throw new UserException($"Cannot change password. ResetPassword failed with errors: {string.Join(Environment.NewLine, changedPasswordResults.Errors.Select(x => x.Description))}.");

            _logger.Info("Password successfully changed.");
        }

        private void CheckElevatedPrivileges()
        {
            //Checking for admin privileges is only supported on Windows
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                bool elevated;
                try
                {
                    WindowsPrincipal principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
                    elevated = principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex.GetType() + ": " + ex.Message);
                    elevated = false;
                }

                if (!elevated)
                    throw new UserException("This process has to be executed with elevated privileges (as administrator).");
            }
        }

        private static string InputPassword()
        {
            var oldFg = Console.ForegroundColor;
            var oldBg = Console.BackgroundColor;
            try
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.BackgroundColor = ConsoleColor.Black;

                var buildPwd = new StringBuilder();
                ConsoleKeyInfo key;

                Console.WriteLine();
                Console.Write("Enter new password for user 'admin': ");
                do
                {
                    key = Console.ReadKey(true);

                    if (((int)key.KeyChar) >= 32)
                    {
                        buildPwd.Append(key.KeyChar);
                        Console.Write("*");
                    }
                    else if (key.Key == ConsoleKey.Backspace && buildPwd.Length > 0)
                    {
                        buildPwd.Remove(buildPwd.Length - 1, 1);
                        Console.Write("\b \b");
                    }
                    else if (key.Key == ConsoleKey.Escape)
                    {
                        Console.WriteLine();
                        throw new UserException("User pressed the escape key.");
                    }

                } while (key.Key != ConsoleKey.Enter);
                Console.WriteLine();

                string pwd = buildPwd.ToString();
                if (string.IsNullOrWhiteSpace(pwd))
                    throw new UserException("The password may not be empty.");

                return pwd;
            }
            finally
            {
                Console.ForegroundColor = oldFg;
                Console.BackgroundColor = oldBg;
            }
        }

        private int SafeExecuteCommand(Action action)
        {
            try
            {
                action.Invoke();
                return 0;
            }
            catch (UserException ex)
            {
                _logger.Error("CANCELED: " + ex.Message);
                return 1;
            }
            catch (Exception ex)
            {
                _logger.Error(ex.ToString());
                return 1;
            }
        }
    }
}
