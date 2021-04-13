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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Rhetos;
using Rhetos.AspNetFormsAuth;
using Rhetos.Utilities;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using Rhetos.Host.AspNet;
using Rhetos.Persistence;
using System.Reflection;
using Rhetos.Security;
using System.Linq;
using System.Text;
using System.Security.Principal;
using System.Runtime.InteropServices;

namespace AdminSetup
{
    class Program
    {
        static int Main(string[] args)
        {
            string errorMessage;
            try
            {
                new App().Run(args);
                return 0;
            }
            catch (ApplicationException ex)
            {
                errorMessage = "CANCELED: " + ex.Message;
            }
            catch (Exception ex)
            {
                errorMessage = "ERROR: " + ex;
            }

            if (errorMessage != null)
            {
                Console.WriteLine();
                Console.WriteLine(errorMessage);

                Console.WriteLine();
                Console.Write("Press any key to continue . . .");
                Console.ReadKey(true);

                return 1;
            }

            return 0;
        }
    }

    class App
    {
        static readonly string ExecuteCommandInCurrentProcessOptionName = "--execute-command-in-current-process";

        internal void Run(string[] args)
        {
            var rootCommand = new RootCommand();
            rootCommand.Add(new Argument<FileInfo>("startup-assembly") { Description = "Startup assembly of the host application." });
            rootCommand.Add(new Option<string>("--password", "Administrator password."));
            //Lack of this switch means that the dbupdate command should start the command rhetos.exe dbupdate
            //in another process with the host applications runtimeconfig.json and deps.json files
            var executeCommandInCurrentProcessOption = new Option<bool>(ExecuteCommandInCurrentProcessOptionName);
            executeCommandInCurrentProcessOption.IsHidden = true;
            rootCommand.Add(executeCommandInCurrentProcessOption);
            rootCommand.Handler =
                CommandHandler.Create((FileInfo startupAssembly, string password, bool executeCommandInCurrentProcess) => {
                    if (executeCommandInCurrentProcess)
                        return ExecuteCommand(startupAssembly.FullName, password);
                    else
                        return InvokeAsExternalProcess(startupAssembly.FullName, args);
                });

            rootCommand.Invoke(args);
        }

        private int ExecuteCommand(string rhetosHostAssemblyPath, string password)
        {
            var host = GetHostBuilder(rhetosHostAssemblyPath)
                .ConfigureServices(serviceCollection => serviceCollection.AddScoped<IUserInfo, ProcessUserInfo>())
                .Build();
            using (var scope = host.Services.CreateScope())
            {
                scope.ServiceProvider.GetService<IRhetosComponent<AdminUserInitializer>>().Value.Initialize();
                SetUpAdminAccount(scope.ServiceProvider, password);
                scope.ServiceProvider.GetService<IRhetosComponent<IPersistenceTransaction>>().Value.CommitChanges();
            }

            return 0;
        }

        private void SetUpAdminAccount(IServiceProvider scope, string password)
        {
            CheckElevatedPrivileges();

            string adminPassword = string.IsNullOrWhiteSpace(password) ? InputPassword() : password;

            const string adminUserName = AdminUserInitializer.AdminUserName;

            var userManager = scope.GetService<UserManager<IdentityUser<Guid>>>();
            if(userManager == null)
                throw new ApplicationException($"The AspNetFormsAuth package is not configured properly. Call the {nameof(AspNetFormsAuthCollectionExtensions.AddAspNetFormsAuth)} method on the {nameof(IServiceCollection)} inside the Startup.ConfigureServices method.");
           
            var persistenceTransaction = scope.GetService<IRhetosComponent<IPersistenceTransaction>>();

            var principalCount = persistenceTransaction.Value.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Common.Principal WHERE Name = @0", new object[] { adminUserName }).Result;
            if (principalCount == 0)
                throw new ApplicationException($"Missing '{adminUserName}' user entry in Common.Principal entity. Please execute DeployPackages.exe, with AspNetFormsAuth package included, to initialize the 'admin' user entry.");

            var user = userManager.FindByNameAsync(adminUserName).Result;

            var token = userManager.GeneratePasswordResetTokenAsync(user).Result;
            var changedPasswordResults = userManager.ResetPasswordAsync(user, token, adminPassword).Result;

            if (!changedPasswordResults.Succeeded)
                throw new ApplicationException($"Cannot change password. ResetPassword failed with errors: {string.Join(Environment.NewLine, changedPasswordResults.Errors.Select(x => x.Description))}.");

            Console.WriteLine("Password successfully changed.");
        }

        private int InvokeAsExternalProcess(string rhetosHostDllPath, string[] baseArgs)
        {
            var logger = new ConsoleLogProvider().GetLogger("AdminSetup");
            var newArgs = new List<string>(baseArgs);
            newArgs.Add(ExecuteCommandInCurrentProcessOptionName);
            return Exe.RunWithHostConfiguration(GetType().Assembly.Location, rhetosHostDllPath, newArgs, logger);
        }

        private static IHostBuilder GetHostBuilder(string rhetosHostAssemblyPath)
        {
            var HostBuilderFactoryMethodName = "CreateHostBuilder";
            var startupAssembly = Assembly.LoadFrom(rhetosHostAssemblyPath);

            var entryPointType = startupAssembly?.EntryPoint?.DeclaringType;
            if (entryPointType == null)
                throw new FrameworkException($"Startup assembly '{startupAssembly.Location}' doesn't have an entry point.");

            var method = entryPointType.GetMethod(HostBuilderFactoryMethodName, BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Static);
            if (method == null)
                throw new FrameworkException(
                    $"Static method '{entryPointType.FullName}.{HostBuilderFactoryMethodName}' not found in entry point type in assembly {startupAssembly.Location}."
                    + $" Method is required in entry point assembly for constructing a configured instance of {nameof(IHost)}.");

            if (method.ReturnType != typeof(IHostBuilder))
                throw new FrameworkException($"Static method '{entryPointType.FullName}.{HostBuilderFactoryMethodName}' has incorrect return type. Expected return type is {nameof(IHostBuilder)}.");

            return (IHostBuilder)method.InvokeEx(null, new object[] { new string[0] });
        }

        private static void CheckElevatedPrivileges()
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
                    Console.WriteLine(ex.GetType() + ": " + ex.Message);
                    elevated = false;
                }

                if (!elevated)
                    throw new ApplicationException(System.Diagnostics.Process.GetCurrentProcess().ProcessName + " has to be executed with elevated privileges (as administrator).");
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
                        throw new ApplicationException("User pressed the escape key.");
                    }

                } while (key.Key != ConsoleKey.Enter);
                Console.WriteLine();

                string pwd = buildPwd.ToString();
                if (string.IsNullOrWhiteSpace(pwd))
                    throw new ApplicationException("The password may not be empty.");

                return pwd;
            }
            finally
            {
                Console.ForegroundColor = oldFg;
                Console.BackgroundColor = oldBg;
            }
        }
    }
}
