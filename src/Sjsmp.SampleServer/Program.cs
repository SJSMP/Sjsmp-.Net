using Sjsmp.Server;
using System;

namespace Sjmp.SampleServer
{
    class Program
    {
        static void Main(string[] args)
        {
            string schemaPushUrl = "http://portal.activebc.ru/sjmp/register";
            if (args.Length > 0)
            {
                schemaPushUrl = args[0];
            }

            //using (Server server = new Server("SchemaName", "Schema description", 12345))
            using (SjmpServer server = new SjmpServer("SchemaName", "Schema description с русским текстом 111", "Sample group", schemaPushUrl: schemaPushUrl))
            {
                SampleObject obj1 = new SampleObject();
                SampleObject obj2 = new SampleObject();
                server.RegisterObject(obj1, "SampleObjectName1", "First SampleObject Description", "SampleObject Group");
                server.RegisterObject(obj2, "SampleObjectName2", "Second SampleObject Description", "SampleObject Group");

                Console.WriteLine("Server started, press enter to close");
                Console.ReadLine();

                obj1.stopTimer();
                obj2.stopTimer();
                server.UnRegisterObject(obj1);
                server.UnRegisterObject(obj2);
            }
        }
    }
}
