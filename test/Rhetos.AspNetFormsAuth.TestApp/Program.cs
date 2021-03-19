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

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Rhetos.AspNetFormsAuth.TestApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });

        // This method exposes IRhetosHostBuilder for purposes of tooling using same configuration values
        // and same RhetosHostBuilder configuration delegate as the app itself
        public static IRhetosHostBuilder CreateRhetosHostBuilder()
        {
            // create Host for this web app
            var host = CreateHostBuilder(null).Build();

            // extract configuration of the web app
            var configuration = host.Services.GetRequiredService<IConfiguration>();

            // Create RhetosHostBuilder and configure it
            var rhetosHostBuilder = new RhetosHostBuilder();
            Startup.ConfigureRhetosHostBuilder(rhetosHostBuilder, configuration);

            return rhetosHostBuilder;
        }
    }
}
