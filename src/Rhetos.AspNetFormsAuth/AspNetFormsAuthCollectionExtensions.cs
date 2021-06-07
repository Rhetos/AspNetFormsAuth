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

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Rhetos.AspNetFormsAuth;
using System;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class AspNetFormsAuthCollectionExtensions
    {
        /// <summary>
        /// Setup the required components for the <see cref="AuthenticationService"/>.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static RhetosServiceCollectionBuilder AddAspNetFormsAuth(this RhetosServiceCollectionBuilder builder, Action<AspNetFormsAuthOptions> configureOptions = null)
        {
            builder.Services.AddOptions();
            if (configureOptions != null)
            {
                builder.Services.Configure(configureOptions);
            }

            builder.Services.AddIdentityCore<IdentityUser<Guid>>(options =>
                {
                    options.Password.RequireDigit = false;
                    options.Password.RequireLowercase = false;
                    options.Password.RequireNonAlphanumeric = false;
                    options.Password.RequireUppercase = false;
                    options.Password.RequiredLength = 1;
                    options.User.AllowedUserNameCharacters = "";
                })
                .AddUserStore<RhetosUserStore>()
                .AddDefaultTokenProviders()
                .AddSignInManager<SignInManager<IdentityUser<Guid>>>();

            builder.Services.TryAddScoped<ISecurityStampValidator, SecurityStampValidator<IdentityUser<Guid>>>();
            builder.Services.TryAddScoped<AuthenticationService>();

            builder.AddRestApiFilters();

            builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
                .AddCookie(IdentityConstants.ApplicationScheme, o => o.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                });

            return builder;
        }
    }
}
