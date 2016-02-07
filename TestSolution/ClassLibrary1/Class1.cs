using System.Diagnostics;
using Newtonsoft.Json;

namespace ClassLibrary1
{
    public class Class1
    {
        public Class1()
        {
            var test = JsonConvert.DeserializeObject("{ lbn1: 1");
            Debug.WriteLine(test);
        }
    }
}
