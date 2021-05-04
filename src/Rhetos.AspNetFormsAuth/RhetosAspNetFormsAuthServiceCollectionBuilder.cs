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

using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Reflection;

namespace Rhetos.AspNetFormsAuth
{
    public class RhetosAspNetFormsAuthServiceCollectionBuilder
    {
        public IServiceCollection Services { get; }

        public RhetosAspNetFormsAuthServiceCollectionBuilder(IServiceCollection services)
        {
            Services = services;
        }

        /// <summary>
        /// Adds the <see cref="AuthenticationController"/> to the controller feature.
        /// </summary>
        /// <returns></returns>
        public RhetosAspNetFormsAuthServiceCollectionBuilder AddControllers()
        {
            this.Services
                .AddControllers()
                .ConfigureApplicationPartManager(p =>
                {
                    p.FeatureProviders.Add(new AspNetFormsControllerFeatureProvider());
                });

            return this;
        }


        internal class AspNetFormsControllerFeatureProvider : IApplicationFeatureProvider<ControllerFeature>
        {
            public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
            {
                feature.Controllers.Add(typeof(AuthenticationController).GetTypeInfo());
            }
        }
    }
}
