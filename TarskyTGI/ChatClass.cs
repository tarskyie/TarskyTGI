using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TarskyTGI
{
    internal class ChatClass
    {
        public string model { get; set; } = string.Empty;
        public string format { get; set; } = "chatml";
        public int n_ctx { get; set; } = 1024;
        public int n_predict { get; set; } = 128;
        public float temperature { get; set; } = 0.8f;
        public float top_p { get; set; } = 0.95f;
        public float min_p { get; set; } = 0.05f;
        public float typical_p { get; set; } = 1.0f;
        public int layers { get; set; } = 35;

        public ChatClass(string modelInp, string formatInp, int n_ctxInp, int n_predictInp, float temperatureInp, float top_pInp, float min_pInp, float typical_pInp, int layersInp)
        {
            model = modelInp;
            format = formatInp;
            n_ctx = n_ctxInp;
            n_predict = n_predictInp;
            temperature = temperatureInp;
            top_p = top_pInp;
            min_p = min_pInp;
            typical_p = typical_pInp;
            layers = layersInp;
        }

        public ChatClass() { }
    }
}
