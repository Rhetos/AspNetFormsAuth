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

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rhetos.Utilities;
using System;
using System.Threading.Tasks;

namespace Rhetos.AspNetFormsAuth.TestApp.Controllers
{
    [ApiController]
    [ApiExplorerSettings(GroupName = "rhetos")]
    public class TestController : ControllerBase
    {
        private readonly IRhetosComponent<IUserInfo> _userInfo;

        public TestController(IRhetosComponent<IUserInfo> userInfo)
        {
            _userInfo = userInfo;
        }

        [HttpGet]
        [Authorize]
        [Route("Test/GetTest")]
        public string GetTest()
        {
            return "test";
        }

        [HttpGet]
        [Route("Test/GetUserInfo")]
        public string GetUserInfo()
        {
            return _userInfo.Value.Report();
        }
    }
}
