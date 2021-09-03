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

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Rhetos.AspNetFormsAuth;

namespace Rhetos
{
    public static class RhetosAspNetFormsAuthApplicationBuilderExtensions
    {
        /// <summary>
        /// Add Authentication controller to MVC feature pipeline and maps it to its respective routes.
        /// Call before 'app.UseEndpoints()'.
        /// </summary>
        public static IApplicationBuilder UseRhetosAspNetFormsAuth(
            this IApplicationBuilder app)
        {
            // Slightly hacky way to modify features and conventions AFTER service provider has already been built
            // Due to this, it is important that this method is executed prior to any others that force feature enumeration
            // Feature enumeration will usually happen during any endpoint mapping, so this should occur prior to app.UseEndpoints or any middleware that invokes it
            // Also, due to inner workings of MVC, this method will not work if controllers are in 'AsServices' mode (via services.AddControllersAsServices())
            var aspNetFormsAuthOptions = app.ApplicationServices.GetRequiredService<IOptions<AspNetFormsAuthOptions>>();
            var mvcOptions = app.ApplicationServices.GetRequiredService<IOptions<MvcOptions>>();
            mvcOptions.Value.Conventions.Add(new AuthenticationControllerRouteConvention(aspNetFormsAuthOptions));

            return app;
        }
    }
}
