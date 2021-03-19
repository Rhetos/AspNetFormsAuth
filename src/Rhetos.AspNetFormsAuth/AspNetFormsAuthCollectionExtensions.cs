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
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Rhetos.AspNetFormsAuth;
using Rhetos.Host.AspNet;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class AspNetFormsAuthCollectionExtensions
    {
        /// <summary>
        /// Setup the required components for the <see cref="AuthenticationService"/>.
        /// Adds the <see cref="AuthenticationController"/> to the controller feature.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="useRegexRulesForPasswordStrengthCheck">If set to true it uses RegularExpression value in the Common.AspNetFormsAuthPasswordStrength table to check the password strength.
        /// Otherwise it uses the <see cref="IdentityOptions.Password"/> to validate the password.</param>
        /// <returns></returns>
        public static RhetosAspNetServiceCollectionBuilder AddAspNetFormsAuth(this RhetosAspNetServiceCollectionBuilder builder, bool useRegexRulesForPasswordStrengthCheck)
        {
            builder.Services.AddIdentityCore<IdentityUser<Guid>>(options =>
                {
                    if (useRegexRulesForPasswordStrengthCheck)
                    {
                        options.Password.RequireDigit = false;
                        options.Password.RequireLowercase = false;
                        options.Password.RequireNonAlphanumeric = false;
                        options.Password.RequireUppercase = false;
                        options.Password.RequiredLength = 1;
                    }
                })
                .AddUserStore<RhetosUserStore>()
                .AddDefaultTokenProviders()
                .AddSignInManager<SignInManager<IdentityUser<Guid>>>();

            builder.Services.TryAddScoped<ISecurityStampValidator, SecurityStampValidator<IdentityUser<Guid>>>();
            builder.Services.AddSingleton(context => new AuthenticationServiceOptions { UseRegexRulesForPasswordStrengthCheck = useRegexRulesForPasswordStrengthCheck });
            builder.Services.TryAddScoped<AuthenticationService>();

            builder.AddRestApi();

            builder.UseAspNetCoreIdentityUser();

            builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
                .AddCookie(IdentityConstants.ApplicationScheme, o => o.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                });

            builder.Services
                .AddControllers()
                .ConfigureApplicationPartManager(p =>
                {
                    p.FeatureProviders.Add(new AspNetFormsControllerFeatureProvider());
                });

            return builder;
        }
    }

    internal class AspNetFormsControllerFeatureProvider : IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
        {
            feature.Controllers.Add(typeof(AuthenticationController).GetTypeInfo());
        }
    }
}
