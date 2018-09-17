using System;
using System.Threading;
using Relativity.ObjectManager.ExportApiHelper;
using Relativity.Services.Objects.DataContracts;
using Relativity.Services.ServiceProxy;

namespace ExportApiHelperSample
{
    class Program
    {
        static void Main(string[] args)
        {
            new Program(args).Run();
        }

        private readonly string[] _args;

        Program(string[] args)
        {
            _args = args;
        }

        private void Run()
        {
            ExportApiHelperConfig config = new ExportApiHelperConfig()
            {
                BlockSize = 1000,
                QueryRequest = new QueryRequest()
                {
                    Fields = new FieldRef[]
                    {
                        new FieldRef {Name = "Control Number"},
                        new FieldRef {Name = "Extracted Text"}
                    },
                    MaxCharactersForLongTextValues = 100 * 1024
                },
                WorkspaceId = 1234567,
                RelativityUrl = new Uri("https://relativity.mycompany.com"),
                Credentials = new UsernamePasswordCredentials("me@mycompany.com", "MyPasswordDontDoThis987$"),
                ScaleFactor = 4
            };

            Relativity.ObjectManager.ExportApiHelper.ExportApiHelper helper = config.Create();

            Metrics metrics = new Metrics();
            metrics.Begin();

            CancellationTokenSource cts = new CancellationTokenSource();
            helper.Run(new ExportApiHandler(metrics), cts.Token);

            metrics.End();
            Console.WriteLine(metrics);
        }

    }
}
