using System;
using System.Configuration;
using System.Data;
using System.Data.Common;

namespace Correspondence.Distributor.SqlRepository
{
    internal class Session : IDisposable
    {
        private IDbConnection _connection;
        private IDbCommand _command;
        private IDbTransaction _transaction;

        public Session(string connectionString)
        {
            ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings[connectionString];
            DbProviderFactory factory = DbProviderFactories.GetFactory(settings.ProviderName);
            _connection = factory.CreateConnection();
            _connection.ConnectionString = settings.ConnectionString;
            _connection.Open();
        }

        public IDbCommand Command
        {
            get
            {
                if (_command == null)
                {
                    _command = _connection.CreateCommand();
                    if (_transaction != null)
                        _command.Transaction = _transaction;
                }

                return _command;
            }
        }

        public void BeginTransaction()
        {
            if (_transaction == null)
                _transaction = _connection.BeginTransaction(IsolationLevel.ReadCommitted);
        }

        public void Commit()
        {
            if (_transaction != null)
                _transaction.Commit();
        }

        public void Dispose()
        {
            if (_command != null)
                _command.Dispose();

            if (_transaction != null)
                _transaction.Dispose();

            _connection.Close();
        }
    }
}
