using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TarskyTGI
{
    public class JsonService
    {
        private string jsonFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TarskyTGI");

        public void copyJsonToDocuments(string json)
        {
            try
            {
                Directory.CreateDirectory(jsonFolder);
                File.Copy(json, Path.Combine(jsonFolder, json), true);
            }
            catch
            {
                // If copying fails (for example file not found), ensure folder exists and rethrow
                Directory.CreateDirectory(jsonFolder);
                try { File.Copy(json, Path.Combine(jsonFolder, json), true); } catch { }
            }
        }

        public string GetJsonFilePath(string jsonFileName)
        {
            Directory.CreateDirectory(jsonFolder);
            return Path.Combine(jsonFolder, jsonFileName);
        }

        public void EnsureJsonExists(string jsonFileName, string defaultSource = null)
        {
            var target = GetJsonFilePath(jsonFileName);
            if (!File.Exists(target))
            {
                try
                {
                    if (!string.IsNullOrEmpty(defaultSource) && File.Exists(defaultSource))
                    {
                        File.Copy(defaultSource, target, true);
                    }
                    else if (File.Exists(jsonFileName))
                    {
                        // If a file with the given name exists in the app folder, copy it
                        File.Copy(jsonFileName, target, true);
                    }
                    else
                    {
                        // Create a minimal default file
                        File.WriteAllText(target, "{}");
                    }
                }
                catch
                {
                    // Ensure target exists even if copying fails
                    try { File.WriteAllText(target, "{}"); } catch { }
                }
            }
        }
    }
}
