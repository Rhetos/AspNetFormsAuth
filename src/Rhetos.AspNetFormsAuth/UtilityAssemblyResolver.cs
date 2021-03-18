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

using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Rhetos.AspNetFormsAuth
{
    public static class UtilityAssemblyResolver
    {
        /// <summary>
        /// Detects if current host application has legacy project structure (bin\Plugins),
        /// then registers required assembly resolver to use Rhetos libraries from parent folder.
        /// Call only once at the beginning of Program.Main().
        /// </summary>
        public static void RegisterAssemblyResolver()
        {
            const string sampleRhetosLibrary = "Rhetos.Extensibility.dll";
            if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, sampleRhetosLibrary)))
            {
                // This utility is already located at bin folder, not need for custom assembly resolver.
            }
            else if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", sampleRhetosLibrary)))
            {
                // This utility is already located at bin\Plugins folder (legacy project structure).
                // Include libraries from parent folder and subfolders.
                string searchFolder = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
                AppDomain.CurrentDomain.AssemblyResolve += (object sender, ResolveEventArgs args) => FindAssembly(args, searchFolder);
            }
            else
            {
                throw new ApplicationException($"{AppDomain.CurrentDomain.FriendlyName}: Cannot find Rhetos binaries" +
                    $" in {AppDomain.CurrentDomain.BaseDirectory} or parent folder.");
            }
        }

        /// <summary>
        /// This program is executed in bin\Plugins, so the assemblies from the parent (bin) folder must be loaded manually.
        /// </summary>
        private static Assembly FindAssembly(ResolveEventArgs args, string searchFolder)
        {
            string rhetosBinFolder = searchFolder;
            string shortAssemblyName = new AssemblyName(args.Name).Name;
            string guessAssemblyFileName = shortAssemblyName + ".dll";
            var files = Directory.GetFiles(rhetosBinFolder, guessAssemblyFileName, SearchOption.AllDirectories);

            if (files.Count() == 1)
            {
                return Assembly.LoadFrom(files.Single());
            }
            else if (files.Count() > 1)
            {
                var assembly = Assembly.LoadFrom(files.First());
                Console.WriteLine($"[Warning] {AppDomain.CurrentDomain.FriendlyName}:" +
                    $" Found more than one assembly file for '{shortAssemblyName}' inside '{rhetosBinFolder}':" +
                    $" {string.Join(", ", files)}. Loaded '{assembly.Location}'.");
                return assembly;
            }
            else
            {
                Console.WriteLine($"[Error] {AppDomain.CurrentDomain.FriendlyName}: Could not find assembly '{shortAssemblyName}' inside '{rhetosBinFolder}'.");
                return null;
            }
        }
    }
}
