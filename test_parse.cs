using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

class Program {
    static void Main() {
        var json = "{\"choices\": [{\"delta\": {\"content\": \"hi\"}}]}";
        var serializer = new JavaScriptSerializer();
        var parsed = serializer.Deserialize<Dictionary<string, object>>(json);
        var choices = parsed["choices"];
        Console.WriteLine(choices.GetType().FullName);
    }
}
