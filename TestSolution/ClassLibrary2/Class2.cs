using System.Diagnostics;
using Newtonsoft.Json;

namespace ClassLibrary2
{
    public class Class2
    {
        public Class2()
        {
            var test = JsonConvert.DeserializeObject("{ lbn2: 2");
            Debug.WriteLine(test);
        }
    }
}
