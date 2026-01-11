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
        private string jsonPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TarskyTGI");

        public void copyJsonToDocuments(string json) {
            try
            {
                File.Copy(json, Path.Combine(jsonPath, json), true);
            }
            catch
            {
                Directory.CreateDirectory(jsonPath);
                File.Copy(json, Path.Combine(jsonPath, json), true);
            }
        }
    }
}
