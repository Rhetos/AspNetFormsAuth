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
using Rhetos;
using Rhetos.AspNetFormsAuth;
using Rhetos.Dom.DefaultConcepts;
using Rhetos.Persistence;
using Rhetos.Security;
using Rhetos.Utilities;
using System;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Web.Security;
using WebMatrix.WebData;

namespace AdminSetup
{
    class Program
    {
        static int Main(string[] args)
        {
            // The Program class cannot use Rhetos classes directly, because it needs to register assembly resolved first. Application built with DeployPackages would fail with error "Could not load file or assembly...".
            UtilityAssemblyResolver.RegisterAssemblyResolver();
            return App.Run(args);
        }
    }

    static class App
    {
        internal static int Run(string[] args)
        {
            string errorMessage = null;
            try
            {
                Exception createAdminUserException = null;
                try
                {
                    ExecuteInRhetosContainer(CreateAdminUserAndPermissions);
                }
                catch (Exception ex)
                {
                    // If CreateAdminUserAndPermissions() fails, this program will still try to execute SetUpAdminAccount() then report the exception later.
                    createAdminUserException = ex;
                }

                string password = null;
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "-pass" && i < args.Length - 1)
                    {
                        password = args[i + 1];
                        break;
                    }
                }

                SetUpAdminAccount(password);

                if (createAdminUserException != null)
                    ExceptionsUtility.Rethrow(createAdminUserException);
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
                if (!args.Any(arg => arg.Equals("/nopause")))
                {
                    Console.WriteLine();
                    Console.Write("Press any key to continue . . .");
                    Console.ReadKey(true);
                }
                return 1;
            }

            return 0;
        }

        private static void CreateAdminUserAndPermissions(IContainer container)
        {
            var repositories = container.Resolve<GenericRepositories>();
            new AdminUserInitializer(repositories).Initialize();
        }

        private static void ExecuteInRhetosContainer(Action<IContainer> action)
        {
            Exception originalException = null;
            try
            {
                using (var container = CreateRhetosContainer())
                {
                    try
                    {
                        action(container);
                    }
                    catch (Exception ex)
                    {
                        // Some exceptions result with invalid SQL transaction state that results with another exception on disposal of this 'using' block.
                        // The original exception is logged here to make sure that it is not overridden.
                        originalException = ex;

                        container.Resolve<IPersistenceTransaction>().DiscardChanges();
                        ExceptionsUtility.Rethrow(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                if (originalException != null && ex != originalException)
                {
                    Console.WriteLine("Error on cleanup: " + ex.ToString());
                    ExceptionsUtility.Rethrow(originalException);
                }
                else
                    ExceptionsUtility.Rethrow(ex);
            }
        }

        private static IContainer CreateRhetosContainer()
        {
            return Host.CreateRhetosContainer(
                addCustomConfiguration: configurationBuilder => configurationBuilder.AddConfigurationManagerConfiguration(),
                registerCustomComponents: containerBuilder => containerBuilder.RegisterType<ProcessUserInfo>().As<IUserInfo>());
        }

        private static void SetUpAdminAccount(string defaultPassword = null)
        {
            CheckElevatedPrivileges();

            AuthenticationServiceInitializer.InitializeDatabaseConnection(autoCreateTables: true);

            const string adminUserName = AuthenticationDatabaseInitializer.AdminUserName;

            int id = WebSecurity.GetUserId(adminUserName);
            if (id == -1)
                throw new ApplicationException($"Missing '{adminUserName}' user entry in Common.Principal entity. Please execute DeployPackages.exe, with AspNetFormsAuth package included, to initialize the 'admin' user entry.");

            string adminPassword = string.IsNullOrWhiteSpace(defaultPassword) ? InputPassword() : defaultPassword;

            try
            {
                WebSecurity.CreateAccount(adminUserName, adminPassword);
                Console.WriteLine("Password successfully initialized.");
            }
            catch (MembershipCreateUserException ex)
            {
                if (ex.Message != "The username is already in use.")
                    throw;

                var token = WebSecurity.GeneratePasswordResetToken(adminUserName);
                var changed = WebSecurity.ResetPassword(token, adminPassword);
                if (!changed)
                    throw new ApplicationException("Cannot change password. WebSecurity.ResetPassword failed.");

                Console.WriteLine("Password successfully changed.");
            }
        }

        private static void CheckElevatedPrivileges()
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
