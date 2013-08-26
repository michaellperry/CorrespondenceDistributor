using System;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;

namespace Correspondence.Distributor.SqlRepository
{
    internal class Session : IDisposable
    {
        private IDbConnection _connection;
        private IDbCommand _command;

        public Session(string connectionString)
        {
            ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings[connectionString];
            DbProviderFactory factory = DbProviderFactories.GetFactory(settings.ProviderName);
            _connection = factory.CreateConnection();
            _connection.ConnectionString = settings.ConnectionString;
            _connection.Open();

            _command = _connection.CreateCommand();
        }

        public IDbCommand Command
        {
            get { return _command; }
        }

        public void Dispose()
        {
            if (_command != null)
                _command.Dispose();

            _connection.Close();
        }
    }
}
