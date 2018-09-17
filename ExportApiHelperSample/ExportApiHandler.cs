using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Relativity.ObjectManager.ExportApiHelper;
using Relativity.Services.DataContracts.DTOs.Results;
using Relativity.Services.Field;
using Relativity.Services.Objects.DataContracts;
using FieldType = Relativity.Services.FieldType;

namespace ExportApiHelperSample
{
    class ExportApiHandler : IExportApiHandler
    {
        private int _count = 0;
        private readonly Metrics _metrics;
        private List<FieldMetadata> _fields;
        private readonly HashCollector _hashCollector = new HashCollector();

        public ExportApiHandler(Metrics metrics)
        {
            _metrics = metrics;
        }

        public void Before(ExportInitializationResults results)
        {
            _fields = results.FieldData;
            Console.WriteLine("ExportApiHandler Before called with itemCount: "+results.RecordCount);
        }

        public bool Item(RelativityObjectSlim item)
        {
            int currentCount = Interlocked.Increment(ref _count);

            if (currentCount % 1000 == 0)
            {
                Console.WriteLine(currentCount);
            }

            Interlocked.Increment(ref _metrics.TotalCount);

            for (int i = 0; i < _fields.Count; i++)
            {
               if (_fields[i].FieldType == FieldType.LongText)
                {
                    object value = item.Values[i];

                    if (value is string)
                    {
                        //Console.WriteLine("ExportApiHandler Item " + _count + " called with control of '" + item.Values[0] + "' and text type of String");
                        long length = _hashCollector.Add((string)value);
                        Interlocked.Add(ref _metrics.TotalSize, length);
                    }
                    else if (value is Stream)
                    {
                        //Console.WriteLine("ExportApiHandler Item " + _count + " called with control of '" + item.Values[0] + "' and text type of Stream");
                        long length = _hashCollector.Add((Stream)value);
                        Interlocked.Add(ref _metrics.TotalSize, length);
                    }
                    else
                    {
                        Console.WriteLine("ExportApiHandler Item " + _count + " called with control of '" + item.Values[0] + "' and incorrect text type of '"+value.GetType().ToString()+"'");
                        return false;
                    }
                }
            }

            return true;
        }

        public void After(bool complete)
        {
            Console.WriteLine("ExportApiHandler After called with complete = " + complete);
            Console.WriteLine("Hash is " + _hashCollector);
        }

        public void Error(string message, Exception exception)
        {
            Console.WriteLine("ExportApiHandler Error called with message '" + message + "' and exception '" + exception?.Message + "'");
        }

        public bool TheadSafe => true;

    }
}
