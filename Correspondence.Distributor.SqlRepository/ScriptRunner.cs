using System;
using System.Data;
using System.IO;
using System.Reflection;

namespace Correspondence.Distributor.SqlRepository
{
    static class ScriptRunner
    {
        public static void ExecuteAllScripts(Session session)
        {
            session.Command.CommandType = CommandType.Text;
            ExecuteScript(0, session.Command);
            session.Command.CommandText = "SELECT Version FROM Correspondence_Version";
            int versionId = (int)session.Command.ExecuteScalar() + 1;
            while (ExecuteScript(versionId, session.Command))
            {
                session.Command.CommandText = String.Format("UPDATE Correspondence_Version SET Version = {0}", versionId);
                session.Command.ExecuteNonQuery();
                versionId = versionId + 1;
            }
        }

        private static bool ExecuteScript(int versionId, IDbCommand command)
        {
            string scriptName = String.Format("Correspondence.{0}.sql", versionId);
            Stream scriptStream = GetScriptStream(scriptName);
            if (scriptStream == null)
                return false;

            command.CommandText = ReadScriptFromStream(scriptStream);
            try
            {
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw new ApplicationException(String.Format("Exception wile running scipt {0}. {1}", scriptName, ex.Message));
            }
            return true;
        }

        private static Stream GetScriptStream(string name)
        {
            return Assembly.GetExecutingAssembly().GetManifestResourceStream(typeof(Repository), String.Format(@"Scripts.{0}", name));
        }

        private static string ReadScriptFromStream(Stream scriptStream)
        {
            using (StreamReader reader = new StreamReader(scriptStream))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
