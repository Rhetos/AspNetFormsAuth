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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace AdminSetup
{
    public static class Exe
    {
        public static int RunWithHostConfiguration(
            string executable,
            string hostFilePath,
            IReadOnlyList<string> baseArgs,
            ILogger logger)
        {
            var newArgs = new List<string>();
            newArgs.Add("exec");

            var runtimeConfigPath = Path.ChangeExtension(hostFilePath, "runtimeconfig.json");
            if (!File.Exists(runtimeConfigPath))
            {
                logger.Error($"Missing {runtimeConfigPath} file required to run '{Path.GetFileName(executable)}'.");
                return 1;
            }

            newArgs.Add("--runtimeconfig");
            newArgs.Add(runtimeConfigPath);

            var depsFile = Path.ChangeExtension(hostFilePath, "deps.json");
            if (File.Exists(depsFile))
            {
                newArgs.Add("--depsfile");
                newArgs.Add(depsFile);
            }
            else
            {
                logger.Warning($"The file {depsFile} was not found. This can cause a 'DllNotFoundException' during the program execution.");
            }

            newArgs.Add(executable);
            newArgs.AddRange(baseArgs);

            logger.Trace(() => "dotnet args: " + string.Join(", ", newArgs.Select(arg => "\"" + (arg ?? "null") + "\"")));

            NLogProvider.FlushAndShutdown(); // Closing log files to avoid an edge case of the log files being locked by the current process while the new process tries to write to the same log files, since they use the same NLog configuration.

            return Exe.Run("dotnet", newArgs, new ConsoleLogger(logger.Name));
        }

        public static int Run(
            string executable,
            IReadOnlyList<string> args,
            ILogger logger)
        {
            var arguments = ToArguments(args);

            ProcessStartInfo start = new ProcessStartInfo(executable)
            {
                Arguments = arguments,
                UseShellExecute = false,
            };

            int processErrorCode;
            using (var process = Process.Start(start))
            {
                logger.Info(() => $"Started '{Path.GetFileName(executable)}' process {process.Id}.");
                process.WaitForExit();
                processErrorCode = process.ExitCode;
            }

            return processErrorCode;
        }

        private static string ToArguments(IReadOnlyList<string> args)
        {
            var builder = new StringBuilder();
            for (var i = 0; i < args.Count; i++)
            {
                if (i != 0)
                {
                    builder.Append(' ');
                }

                if (!args[i].Contains(' '))
                {
                    builder.Append(args[i]);

                    continue;
                }

                builder.Append('"');

                var pendingBackslashes = 0;
                for (var j = 0; j < args[i].Length; j++)
                {
                    switch (args[i][j])
                    {
                        case '\"':
                            if (pendingBackslashes != 0)
                            {
                                builder.Append('\\', pendingBackslashes * 2);
                                pendingBackslashes = 0;
                            }

                            builder.Append("\\\"");
                            break;

                        case '\\':
                            pendingBackslashes++;
                            break;

                        default:
                            if (pendingBackslashes != 0)
                            {
                                if (pendingBackslashes == 1)
                                {
                                    builder.Append('\\');
                                }
                                else
                                {
                                    builder.Append('\\', pendingBackslashes * 2);
                                }

                                pendingBackslashes = 0;
                            }

                            builder.Append(args[i][j]);
                            break;
                    }
                }

                if (pendingBackslashes != 0)
                {
                    builder.Append('\\', pendingBackslashes * 2);
                }

                builder.Append('"');
            }

            return builder.ToString();
        }
    }
}
