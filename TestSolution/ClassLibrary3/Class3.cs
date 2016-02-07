using System.Diagnostics;
using Newtonsoft.Json;

namespace ClassLibrary3
{
    public class Class3
    {
        public Class3()
        {
            var test = JsonConvert.DeserializeObject("{ lbn3: 3");
            Debug.WriteLine(test);
        }
    }
}
