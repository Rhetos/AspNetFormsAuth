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

using Microsoft.AspNetCore.Identity;
using Rhetos.Persistence;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Rhetos.AspNetFormsAuth
{
    public class RhetosUserStore : IUserStore<IdentityUser<Guid>>, IUserPasswordStore<IdentityUser<Guid>>, IUserLockoutStore<IdentityUser<Guid>>
    {
        private readonly IRhetosComponent<IPersistenceTransaction> persistenceTransaction;

        public RhetosUserStore(IRhetosComponent<IPersistenceTransaction> persistenceTransaction)
        {
            this.persistenceTransaction = persistenceTransaction;
        }

        public Task<IdentityResult> CreateAsync(IdentityUser<Guid> user, CancellationToken cancellationToken)
        {
            var userStoreType = nameof(IUserStore<IdentityUser<Guid>>);
            var userManagerType = nameof(UserManager<IdentityUser<Guid>>);
            throw new NotSupportedException($"This implementation of the {userStoreType} expects to have the Common.Principal already created." +
                    $" Use the DomRepository class to create the Common.Principal and then find and update the user data with the {userManagerType} class.");
        }

        public Task<IdentityResult> DeleteAsync(IdentityUser<Guid> user, CancellationToken cancellationToken)
        {
            throw new NotSupportedException($"This implementation of the {nameof(IUserStore<IdentityUser<Guid>>)} does not support the deletion of the user. Use the DomRepository class to delete the user.");
        }

        public async Task<IdentityResult> UpdateAsync(IdentityUser<Guid> user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var principals = await persistenceTransaction.Value.ExecuteQueryAsync(@"
                    SELECT
                        cp.AspNetUserId,
                        HasMembership = CAST(CASE WHEN m.Userid IS NULL THEN 0 ELSE 1 END AS BIT)
                    FROM
                        Common.Principal cp
                        LEFT JOIN webpages_Membership m ON m.Userid = cp.AspNetUserId
                    WHERE cp.ID = @0",
                    new object[] { user.Id },
                    reader => new
                    {
                        AspNetUserId = reader.IsDBNull(0) ? new int?() : reader.GetInt32(0),
                        HasMembership = reader.IsDBNull(1) ? new bool?() : reader.GetBoolean(1)
                    });

                if(principals.Count == 0)
                    return IdentityResult.Failed(new IdentityError { Description = "There is no Principal with the requested Id." });

                var principal = principals.Single();
                if (!principal.AspNetUserId.HasValue)
                    return IdentityResult.Failed(new IdentityError { Description = "The value for AspNetUserId in Common.Principal was not set for the requested user." });

                if (principal.HasMembership == true)
                {
                    await persistenceTransaction.Value.ExecuteNonQueryAsync(@"
                        UPDATE m
                        SET
                            PasswordFailuresSinceLastSuccess = @1,
                            Password = @2,
                            LockoutEnd = @3
                        FROM 
                            dbo.webpages_Membership m
                            LEFT JOIN Common.Principal cp ON cp.AspNetUserId = m.UserId
                        WHERE cp.ID = @0",
                        user.Id, user.AccessFailedCount,
                        user.PasswordHash ?? string.Empty,
                        user.LockoutEnd?.DateTime);
                }
                else
                {
                    //In the previous version of the plugin the field IsConfirmed was not used and it was always set to true
                    await persistenceTransaction.Value.ExecuteNonQueryAsync(@"
                        INSERT INTO dbo.webpages_Membership (UserId, PasswordFailuresSinceLastSuccess, Password, PasswordSalt, LockoutEnd, IsConfirmed) VALUES(@0, @1, @2, @3,@4, @5)",
                        principal.AspNetUserId.Value, user.AccessFailedCount,
                        user.PasswordHash ?? string.Empty, string.Empty,
                        user.LockoutEnd?.DateTime,
                        true);
                }

                return IdentityResult.Success;
            }
            catch (Exception e)
            {
                persistenceTransaction.Value.DiscardChanges();
                return IdentityResult.Failed(new IdentityError { Description = e.Message });
            }
        }

        public void Dispose()
        {
            // Nothing to dispose.
        }

        public async Task<IdentityUser<Guid>> FindByIdAsync(string userId, CancellationToken cancellationToken)
        {
            return await QueryUser(@"
                SELECT
                    cp.ID,
                    m.Password,
                    m.PasswordFailuresSinceLastSuccess,
                    cp.Name,
                    m.LockoutEnd
                FROM
                    Common.Principal cp
                    LEFT JOIN webpages_Membership m ON m.Userid = cp.AspNetUserId
                WHERE cp.ID = @0", userId);
        }

        public async Task<IdentityUser<Guid>> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
        {
            return await QueryUser(@"
                SELECT
                    cp.ID,
                    m.Password,
                    m.PasswordFailuresSinceLastSuccess,
                    cp.Name,
                    m.LockoutEnd
                FROM
                    Common.Principal cp
                    LEFT JOIN webpages_Membership m ON m.Userid = cp.AspNetUserId
                WHERE cp.Name = @0", normalizedUserName);
        }

        private async Task<IdentityUser<Guid>> QueryUser(string query, params object[] parameters)
        {
            var results = await this.persistenceTransaction.Value.ExecuteQueryAsync(query, parameters, (reader) =>
            {
                return new IdentityUser<Guid>
                {
                    Id = reader.GetGuid(0),
                    PasswordHash = reader.IsDBNull(1) ? null : ReturnNullIfStringIsEmpty(reader.GetString(1)),
                    AccessFailedCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                    UserName = reader.IsDBNull(3) ? null : reader.GetString(3),
                    LockoutEnd = reader.IsDBNull(4) ? null : DateTime.SpecifyKind(reader.GetDateTime(4), DateTimeKind.Utc),
                    LockoutEnabled = true
                };
            });

            return results.FirstOrDefault();
        }

        //ASP.NET IDentity interprets a non null string as if the password has been set
        //Because the Password field in the dbo.webpages_Membership table is required
        //we are interpreting an empty string as null
        private string ReturnNullIfStringIsEmpty(string s)
        {
            if (string.IsNullOrEmpty(s))
                return null;
            return s;
        }

        public Task<string> GetNormalizedUserNameAsync(IdentityUser<Guid> user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            return Task.FromResult(user.NormalizedUserName);
        }

        public Task<string> GetUserIdAsync(IdentityUser<Guid> user, CancellationToken cancellationToken)
        {
            return Task.FromResult(user.Id.ToString());
        }

        public Task<string> GetUserNameAsync(IdentityUser<Guid> user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            return Task.FromResult(user.UserName);
        }

        public Task SetNormalizedUserNameAsync(IdentityUser<Guid> user, string normalizedName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (user == null)
                throw new ArgumentNullException(nameof(user));

            if (normalizedName == null)
                throw new ArgumentNullException(nameof(normalizedName));

            user.NormalizedUserName = normalizedName;

            return Task.CompletedTask;
        }

        public Task SetUserNameAsync(IdentityUser<Guid> user, string userName, CancellationToken cancellationToken)
        {
            throw new NotSupportedException($"Changing the username is only supported through the Common.DomRepository class.");
        }

        public Task SetPasswordHashAsync(IdentityUser<Guid> user, string passwordHash, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            user.PasswordHash = passwordHash;
            return Task.CompletedTask;
        }

        public Task<string> GetPasswordHashAsync(IdentityUser<Guid> user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(user.PasswordHash);
        }

        public Task<bool> HasPasswordAsync(IdentityUser<Guid> user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(!string.IsNullOrEmpty(user.PasswordHash));
        }

        public Task<DateTimeOffset?> GetLockoutEndDateAsync(IdentityUser<Guid> user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }
            return Task.FromResult(user.LockoutEnd);
        }

        public Task SetLockoutEndDateAsync(IdentityUser<Guid> user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }
            user.LockoutEnd = lockoutEnd;
            return Task.CompletedTask;
        }

        public Task<int> IncrementAccessFailedCountAsync(IdentityUser<Guid> user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }
            user.AccessFailedCount++;
            return Task.FromResult(user.AccessFailedCount);
        }

        public Task ResetAccessFailedCountAsync(IdentityUser<Guid> user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }
            user.AccessFailedCount = 0;
            return Task.CompletedTask;
        }

        public Task<int> GetAccessFailedCountAsync(IdentityUser<Guid> user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }
            return Task.FromResult(user.AccessFailedCount);
        }

        public Task<bool> GetLockoutEnabledAsync(IdentityUser<Guid> user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }
            return Task.FromResult(user.LockoutEnabled);
        }

        public Task SetLockoutEnabledAsync(IdentityUser<Guid> user, bool enabled, CancellationToken cancellationToken)
        {
            throw new NotSupportedException($"Setting the ${nameof(IdentityUser<Guid>.LockoutEnabled)} property is not supported in this implementation of the user store.");
        }
    }
}
