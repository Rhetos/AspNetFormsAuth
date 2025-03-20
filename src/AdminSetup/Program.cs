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

using Rhetos.Logging;
using Rhetos.Utilities;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AdminSetup
{
    static class Program
    {
        static int Main(string[] args)
        {
            try
            {
                return Run(args);
            }
            finally
            {
                if (!args.Any(arg => arg.Equals("--no-pause")))
                {
                    Console.WriteLine();
                    Console.WriteLine("Press any key to continue . . .");
                    Console.ReadKey(true);
                }
            }
        }

        static readonly string ExecuteCommandInCurrentProcessOptionName = "--execute-command-in-current-process";

        public static int Run(string[] args)
        {
            Argument<FileInfo> startupAssemblyArgument = new Argument<FileInfo>("startup-assembly") { Description = "Startup assembly of the host application." };
            Option<string> passwordOption = new Option<string>("--password", "Administrator password.");
            Option<bool> noPauseOption = new Option<bool>("--no-pause", "Don't wait for user input after execution.");
            //Lack of this switch means that the dbupdate command should start the command rhetos.exe dbupdate
            //in another process with the host applications runtimeconfig.json and deps.json files
            Option<bool> currentProcessOption = new Option<bool>(ExecuteCommandInCurrentProcessOptionName) { IsHidden = true };

            var rootCommand = new RootCommand
            {
                startupAssemblyArgument,
                passwordOption,
                noPauseOption,
                currentProcessOption
            };

            rootCommand.SetHandler((FileInfo startupAssembly, string password, bool noPause, bool executeCommandInCurrentProcess) =>
                {
                    if (executeCommandInCurrentProcess)
                    {
                        var app = new App();
                        return app.SetUpAdminAccount(startupAssembly.FullName, password);
                    }
                    else
                        return InvokeAsExternalProcess(startupAssembly.FullName, args);
                },
                startupAssemblyArgument, passwordOption, noPauseOption, currentProcessOption);

            return rootCommand.Invoke(args);
        }

        /// <summary>
		/// Executing the current CLI utility in the context of the host application, involving the host app's dependencies,
		/// .NET version and runtime framework.
		/// This is need for the utility to be able to use the provided application's object model without issues with missing dependency libraries.
		/// </summary>
        private static Task<int> InvokeAsExternalProcess(string rhetosHostDllPath, string[] baseArgs)
        {
            var newArgs = new List<string>(baseArgs) { ExecuteCommandInCurrentProcessOptionName };
            var result = Exe.RunWithHostConfiguration(typeof(Program).Assembly.Location, rhetosHostDllPath, newArgs, new ConsoleLogger(EventType.Trace, "AdminSetup"));
            return Task.FromResult(result);
        }
    }
}
