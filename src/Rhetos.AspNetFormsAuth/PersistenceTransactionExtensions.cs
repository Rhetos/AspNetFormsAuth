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

using Rhetos.Persistence;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Threading.Tasks;

namespace Rhetos.AspNetFormsAuth
{
    public static class PersistenceTransactionExtensions
    {
        public static async Task<T> ExecuteScalarAsync<T>(this IPersistenceTransaction persistenceTransaction, string query, params object[] parameters)
        {
            return await PrepareCommand(persistenceTransaction, query, parameters, async command => (T)await command.ExecuteScalarAsync());
        }

        public static async Task<int> ExecuteNonQueryAsync(this IPersistenceTransaction persistenceTransaction, string query, params object[] parameters)
        {
            return await PrepareCommand(persistenceTransaction, query, parameters, async  command => await command.ExecuteNonQueryAsync());
        }

        public static async Task<List<T>> ExecuteQueryAsync<T>(this IPersistenceTransaction persistenceTransaction, string query, object[] parameters, Func<DbDataReader, T> read)
        {
            return await PrepareCommand(persistenceTransaction, query, parameters, async command =>
            {
                var results = new List<T>();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (reader != null)
                    {
                        while (reader.Read())
                        {
                            results.Add(read(reader));
                        }
                    }
                }
                return results;
            });
        }

        private static T PrepareCommand<T>(IPersistenceTransaction persistenceTransaction, string query, object[] parameters, Func<DbCommand, T> executeCommand)
        {
            using (var command = persistenceTransaction.Connection.CreateCommand())
            {
                command.Transaction = persistenceTransaction.Transaction;
                command.CommandText = query;
                AddParameters(command, parameters);

                return executeCommand(command);
            }
        }

        private static void AddParameters(DbCommand command, params object[] parameters)
        {
            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = "@" + i.ToString(CultureInfo.InvariantCulture);
                parameter.Value = parameters[i] ?? DBNull.Value;
                command.Parameters.Add(parameter);
            }
        }
    }
}
