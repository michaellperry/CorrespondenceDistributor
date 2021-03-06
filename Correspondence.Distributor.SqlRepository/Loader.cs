using System;
using System.Collections.Generic;
using System.Data;
using UpdateControls.Correspondence.Mementos;

namespace Correspondence.Distributor.SqlRepository
{
    class Loader : IDisposable
    {
        private readonly IDataReader _reader;

        public Loader(IDataReader reader)
        {
            _reader = reader;
        }

        public IEnumerable<IdentifiedFactMemento> LoadMementos()
        {
            IdentifiedFactMemento current = null;

            while (_reader.Read())
            {
                // FactID, Data, TypeName, Version, DeclaringTypeName, DeclaringTypeVersion, RoleName, PredecessorFactID, IsPivot
                long factId = _reader.GetInt64(0);

                // Load the header.
                if (current == null || factId != current.Id.key)
                {
                    if (current != null)
                        yield return current;

                    string typeName = _reader.GetString(2);
                    int typeVersion = _reader.GetInt32(3);

                    // Create the memento.
                    current = new IdentifiedFactMemento(
                        new FactID() { key = factId },
                        new FactMemento(new CorrespondenceFactType(typeName, typeVersion)));
                    ReadBinary(current.Memento, 1);
                }

                // Load a predecessor.
                if (!_reader.IsDBNull(4))
                {
                    string declaringTypeName = _reader.GetString(4);
                    int declaringTypeVersion = _reader.GetInt32(5);
                    string roleName = _reader.GetString(6);
                    long predecessorFactId = _reader.GetInt64(7);
                    bool isPivot = _reader.GetBoolean(8);

                    current.Memento.AddPredecessor(
                        new RoleMemento(
                            new CorrespondenceFactType(
                                declaringTypeName,
                                declaringTypeVersion),
                            roleName,
                            null,
                            false),
                        new FactID() { key = predecessorFactId },
                        isPivot);
                }
            }

            if (current != null)
                yield return current;
        }

        public IEnumerable<FactID> LoadIDs()
        {
            while (_reader.Read())
                yield return new FactID() { key = _reader.GetInt64(0) };
        }

        public IEnumerable<AncestorPivot> LoadAncestorPivots()
        {
            while (_reader.Read())
            {
                long ancestorFactId = _reader.GetInt64(0);
                int ancestorRoleId = _reader.GetInt32(1);
                long pivotId = _reader.GetInt64(2);
                yield return new AncestorPivot
                {
                    AncestorFactId = new FactID { key = ancestorFactId },
                    AncestorRoleId = ancestorRoleId,
                    PivotId = new FactID { key = pivotId },
                };
            }
        }

        public IEnumerable<WindowsPhoneSubscription> LoadWindowsPhoneSubscriptions()
        {
            while (_reader.Read())
            {
                long pivotFactId = _reader.GetInt64(0);
                string deviceUri = _reader.GetString(1);
                yield return new WindowsPhoneSubscription
                {
                    PivotFactId = new FactID { key = pivotFactId },
                    DeviceUri = deviceUri
                };
            }
        }

        private void ReadBinary(FactMemento memento, int columnIndex)
        {
            byte[] buffer = new byte[1024];
            int length = (int)_reader.GetBytes(columnIndex, 0, buffer, 0, buffer.Length);
            memento.Data = new byte[length];
            Array.Copy(buffer, memento.Data, length);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
                if (_reader != null)
                    _reader.Dispose();
        }

        ~Loader()
        {
            Dispose(false);
        }
    }
}
