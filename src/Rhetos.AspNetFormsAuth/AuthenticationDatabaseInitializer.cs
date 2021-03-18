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
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Rhetos.AspNetFormsAuth
{
    // Executes at deployment-time.
    [Export(typeof(Rhetos.Extensibility.IServerInitializer))]
    public class AuthenticationDatabaseInitializer : Rhetos.Extensibility.IServerInitializer
    {
        private readonly ILogger _logger;

        public AuthenticationDatabaseInitializer(ILogProvider logProvider)
        {
            _logger = logProvider.GetLogger(GetType().Name);
        }

        public const string AdminUserName = "admin";
        public const string AdminRoleName = "SecurityAdministrator";

        public IEnumerable<string> Dependencies => new[] { typeof(AdminUserInitializer).FullName };

        /// <summary>
        /// NOTE 1:
        /// The data initialization is split into two separate IServerInitializer implementations:
        /// <see cref="AuthenticationDatabaseInitializer"/> and <see cref="AdminUserInitializer"/>, because
        /// each Rhetos data initializer is executed in a separate SQL connection that is closed before
        /// the next initializer is execute. Otherwise, database locking would occur between this two initializers
        /// and block the deployment process.
        /// 
        /// NOTE 2:
        /// The initialization is placed in a separate external application, because the SimpleMembershipProvider functions
        /// require some configuration data in app.config file. The changes in app.config cannot be added as a plugin for
        /// DeployPackages.exe.
        /// </summary>
        public void Initialize()
        {
            var path = Path.Combine(Paths.PluginsFolder, @"InitAspNetDatabase.exe");
            ExecuteApplication(path, "/nopause");
        }

        private void ExecuteApplication(string path, string arguments)
        {
            ProcessStartInfo start = new ProcessStartInfo(path)
            {
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            var processOutput = new StringBuilder();
            int processErrorCode;
            using (Process process = Process.Start(start))
            {
                var outputs = new[] { process.StandardOutput, process.StandardError };
                System.Threading.Tasks.Parallel.ForEach(outputs, output =>
                    {
                        using (StreamReader reader = output)
                        {
                            string line;
                            while ((line = output.ReadLine()) != null)
                                lock (processOutput)
                                    processOutput.AppendLine(line.Trim());
                        }
                    });
                
                process.WaitForExit();
                processErrorCode = process.ExitCode;
            }

            EventType logType = processErrorCode != 0 ? EventType.Error : EventType.Trace;
            _logger.Write(logType, () => Path.GetFileName(path) + " error code: " + processErrorCode);
            _logger.Write(logType, () => Path.GetFileName(path) + " output: " + processOutput.ToString());

            if (processErrorCode != 0)
                throw new FrameworkException(Path.GetFileName(path) + " returned an error: " + processOutput.ToString());
        }
    }
}
