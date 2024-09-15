using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TarskyTGI
{
    internal class ChatClass
    {
        public string model { get; set; }
        public int n_ctx { get; set; }
        public int n_predict { get; set; }
        public float temperature { get; set; }
        public float top_p { get; set; }
        public float min_p { get; set; }
        public float typical_p { get; set; }

        public ChatClass() { }
        public ChatClass(string modelInp, int n_ctxInp, int n_predictInp, float temperatureInp, float top_pInp, float min_pInp, float typical_pInp) 
        {
            model = modelInp;
            n_ctx = n_ctxInp;
            n_predict = n_predictInp;
            temperature = temperatureInp;
            top_p = top_pInp;
            min_p = min_pInp;
            typical_p = typical_pInp;
        }
    }
}
