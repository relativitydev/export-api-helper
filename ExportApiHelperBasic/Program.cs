﻿using Relativity.ObjectManager.ExportApiHelper;
using System;
using System.Collections.Generic;
using Relativity.Services.DataContracts.DTOs.Results;
using Relativity.Services.Field;
using Relativity.Services.Objects.DataContracts;
using Relativity.Services.ServiceProxy;
using FieldRef = Relativity.Services.Objects.DataContracts.FieldRef;

namespace ExportApiHelperBasic
{
    class Program
    {
        static void Main(string[] args)
        {

            ExportApiHelperConfig config = new ExportApiHelperConfig()
            {
                RelativityUrl = new Uri("https://fest2018-current-sandbox.relativity.one"),
                WorkspaceId = 1082531,
                Credentials = new UsernamePasswordCredentials("test-user@fest.com", "Password goes here"),
                QueryRequest = new QueryRequest()
                {
                    Fields = new FieldRef[]
                    {
                        new FieldRef {Name = "Control Number"},
                        new FieldRef {Name = "Extracted Text"}
                    },
                    MaxCharactersForLongTextValues = 100 * 1024
                },
              BlockSize = 1000,
              ScaleFactor = 4
            };

            ExportApiHelper helper = config.Create();

            helper.Run(new MyExportApihandler());
        }

        private class MyExportApihandler : IExportApiHandler
        {
            private List<FieldMetadata> _fieldData;

            public void Before(ExportInitializationResults results)
            {
                Console.WriteLine("Before");
                Console.WriteLine();
                _fieldData = results.FieldData;
            }

            public bool Item(RelativityObjectSlim item)
            {
                for (int i = 0; i < _fieldData.Count; i++)
                {
                    Console.WriteLine(_fieldData[i].Name + ": " + item.Values[i]);
                }

                Console.WriteLine();

                return true;
            }

            public void Error(string message, Exception exception)
            {
                Console.WriteLine("Error "+message);
                Console.WriteLine();
            }

            public void After(bool complete)
            {
                Console.WriteLine("After");
                Console.WriteLine();
            }

            public bool ThreadSafe => false;
        }

    }
}
