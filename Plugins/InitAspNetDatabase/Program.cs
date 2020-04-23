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

using Rhetos;
using Rhetos.AspNetFormsAuth;
using Rhetos.Utilities;
using System;
using System.Linq;
using WebMatrix.WebData;

namespace InitAspNetDatabase
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
                var host = Host.Find(AppDomain.CurrentDomain.BaseDirectory, new ConsoleLogProvider());
                var configuration = host.RhetosRuntime.BuildConfiguration(new ConsoleLogProvider(), host.ConfigurationFolder,
                    configurationBuilder => configurationBuilder.AddConfigurationManagerConfiguration());
                LegacyUtilities.Initialize(configuration);
                CreateMembershipProviderTables();
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

            Console.WriteLine("ASP.NET membership tables created.");
            return 0;
        }

        private static void CreateMembershipProviderTables()
        {
            AuthenticationServiceInitializer.InitializeDatabaseConnection(autoCreateTables: true);

            // Force lazy database initialization.
            int nonexistentUserInt = WebSecurity.GetUserId(Guid.NewGuid().ToString());
            if (nonexistentUserInt != -1)
                throw new ApplicationException("Unexpected GetUserId result.");
        }
    }
}
