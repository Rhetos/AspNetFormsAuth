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

using Rhetos.Dom.DefaultConcepts;
using Rhetos.Logging;
using Rhetos.Persistence;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace Rhetos.AspNetFormsAuth
{
    // Executes at deployment-time.
    [Export(typeof(Rhetos.Extensibility.IServerInitializer))]
    public class AdminUserInitializer : Rhetos.Extensibility.IServerInitializer
    {
        private readonly GenericRepositories _repositories;

        public AdminUserInitializer(GenericRepositories repositories)
        {
            _repositories = repositories;
        }

        public void Initialize()
        {
            var adminPrincipal = _repositories.CreateInstance<IPrincipal>();
            adminPrincipal.Name = AuthenticationDatabaseInitializer.AdminUserName;
            _repositories.InsertOrReadId(adminPrincipal, item => item.Name);

            var adminRole = _repositories.CreateInstance<IRole>();
            adminRole.Name = AuthenticationDatabaseInitializer.AdminRoleName;
            _repositories.InsertOrReadId(adminRole, item => item.Name);

            var adminPrincipalHasRole = _repositories.CreateInstance<IPrincipalHasRole>();
            adminPrincipalHasRole.PrincipalID = adminPrincipal.ID;
            adminPrincipalHasRole.RoleID = adminRole.ID;
            _repositories.InsertOrReadId(adminPrincipalHasRole, item => new { item.PrincipalID, item.RoleID });

            foreach (var securityClaim in AuthenticationServiceClaims.GetDefaultAdminClaims())
            {
                var commonClaim = _repositories.CreateInstance<ICommonClaim>();
                commonClaim.ClaimResource = securityClaim.Resource;
                commonClaim.ClaimRight = securityClaim.Right;
                _repositories.InsertOrReadId(commonClaim, item => new { item.ClaimResource, item.ClaimRight });

                var permission = _repositories.CreateInstance<IRolePermission>();
                permission.RoleID = adminRole.ID;
                permission.ClaimID = commonClaim.ID;
                permission.IsAuthorized = true;
                _repositories.InsertOrUpdateReadId(permission, item => new { item.RoleID, item.ClaimID }, item => item.IsAuthorized);
            }
        }

        public IEnumerable<string> Dependencies => null;
    }
}
