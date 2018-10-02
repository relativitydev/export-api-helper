using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using Relativity.Kepler.Transport;
using Relativity.Services.DataContracts.DTOs.Results;
using Relativity.Services.Objects;
using Relativity.Services.Objects.DataContracts;
using Relativity.Services.ServiceProxy;
using FieldType = Relativity.Services.FieldType;

namespace Relativity.ObjectManager.ExportApiHelper
{
    public class ExportApiHelper
    {
        // Passed through internal constructor
        private readonly Uri _relativityUrl;
        private readonly Credentials _credentials;
        private readonly int _workspaceId;
        private readonly int _blockSize;
        private readonly QueryRequest _queryRequest;
        private readonly int _scaleFactor;

        // Utility
        private readonly object _thisLock = new object();
        private int[] _longTextIds;

        // State
        private Guid _runId;
        private long _recordCount;
        private IObjectManager _objectManager;
        private IExportApiHandler _userHandler;
        private CancellationTokenSource _cancellationTokenSource;
        private CancellationToken _cancellationToken;

        // Indicator that the text is not present and needs to be streamed
        private const string _SHIBBOLETH = "#KCURA99DF2F0FEB88420388879F1282A55760#";

        // Queue to put documents that already have their long text available as a String
        private readonly BlockingCollection<RelativityObjectSlim> _standardQueue
            = new BlockingCollection<RelativityObjectSlim>(2000);

        // Queue to put documents that need their long text streamed
        private readonly BlockingCollection<RelativityObjectSlim> _streamQueue
            = new BlockingCollection<RelativityObjectSlim>(2000);

        // Queue to put documents that have their long text opened as a Stream
        // This count is low because leaving a large number of HTTP connections
        // open and waiting can cause resource exhaustion and timeouts.
        private readonly BlockingCollection<RelativityObjectSlim> _openStreamQueue
            = new BlockingCollection<RelativityObjectSlim>(10);

        internal ExportApiHelper(Uri relativityUrl, Credentials credentials, int workspaceId, int blockSize, QueryRequest queryRequest, int scaleFactor)
        {
            _relativityUrl = relativityUrl;
            _credentials = credentials;
            _workspaceId = workspaceId;
            _blockSize = blockSize;
            _queryRequest = queryRequest;
            _runId = Guid.Empty;
            _recordCount = 0;
            _objectManager = null;
            _scaleFactor = scaleFactor;
        }

        public void Run(IExportApiHandler handler, CancellationToken externalCancellationToken)
        {
            using (CancellationTokenSource internalCancellationTokenSource =
                new CancellationTokenSource())
            {
                using (_cancellationTokenSource =
                    CancellationTokenSource.CreateLinkedTokenSource(internalCancellationTokenSource.Token, externalCancellationToken))
                {
                    _cancellationToken = _cancellationTokenSource.Token;
                    _userHandler = handler;
                    Run();
                }
            }
        }

        public void Run(IExportApiHandler handler)
        {
            using (_cancellationTokenSource =
                new CancellationTokenSource())
            {
                _cancellationToken = _cancellationTokenSource.Token;
                _userHandler = handler;
                Run();
            }
        }

        private void Run()
        {
            // Create lists for the various thread types

            List<Thread> handerCallerThreads = new List<Thread>();
            List<Thread> textStreamerThreads = new List<Thread>();
            List<Thread> blockingConsumerThreads = new List<Thread>();

            // Lets catch all exceptions and report them through the handler 

            try
            {
                // Get Object Manager

                try
                {
                    _objectManager = GetKeplerServiceFactory()
                        .CreateProxy<Relativity.Services.Objects.IObjectManager>();
                }
                catch (Exception exception)
                {
                    _userHandler.Error("GetKeplerServiceFactory().CreateProxy failed", exception);
                    return;
                }

                // Initialize Query

                ExportInitializationResults exportInitializationResults = null;

                try
                {
                    exportInitializationResults =
                        _objectManager.InitializeExportAsync(_workspaceId, _queryRequest, 0).Result;
                }
                catch (Exception exception)
                {
                    _userHandler.Error("InitializeExportAsync failed", exception);
                    return;
                }

                // Save Run info

                _runId = exportInitializationResults.RunID;
                _recordCount = exportInitializationResults.RecordCount;

                // Find indexes of all long text fields

                List<int> longTextIds = new List<int>();

                for (int i = 0; i < exportInitializationResults.FieldData.Count; i++)
                {
                    if (exportInitializationResults.FieldData[i].FieldType == FieldType.LongText)
                    {
                        longTextIds.Add(i);
                    }
                }

                _longTextIds = longTextIds.ToArray();
                
                // Call the handler's Before method

                _userHandler.Before(exportInitializationResults);

                // Create threads that reads blocks of documents

                for (int i = 0; i < _scaleFactor; i++)
                {
                    Thread t = new Thread(BlockConsumer) { Name = "BlockConcumer" + i };
                    t.Start();
                    blockingConsumerThreads.Add(t);
                }

                // Create threads that open long text streams 

                for (int i = 0; i < _scaleFactor; i++)
                {
                    Thread t = new Thread(TextStreamer) { Name = "TextStreamer" + i };
                    t.Start();
                    textStreamerThreads.Add(t);
                }

                // Create threads that call the handler's Item method.
                // Use only a single thread if the handler has not
                // declared itself as thread safe.

                int handlerCallerThreadCount = _userHandler.ThreadSafe ? _scaleFactor : 1;

                for (int i = 0; i < handlerCallerThreadCount; i++)
                {
                    Thread t = new Thread(HandlerCaller) { Name = "HandlerCaller" + i };
                    t.Start();
                    handerCallerThreads.Add(t);
                }
            }
            catch (Exception exception)
            {
                SendErrorAndCancel("Unexpected exception", exception);
            }

            // Wait for the threads reading blocks of documents
            // to complete

            foreach (Thread t in blockingConsumerThreads)
            {
                t.Join();
            }

            // Indicate the we will add no more documents
            // to the standard and stream queues

            _standardQueue.CompleteAdding();
            _streamQueue.CompleteAdding();

            // Wait for the threads opening streams to complete

            foreach (Thread t in textStreamerThreads)
            {
                t.Join();
            }

            // Indicate that no more documents with open
            // streams will be added to that queue

            _openStreamQueue.CompleteAdding();

            // Wait for all the documents remaining
            // in the standard and open stream queues
            // to be sent to the handler

            foreach (Thread t in handerCallerThreads)
            {
                t.Join();
            }

            // Call the handler's After method

            _userHandler.After(!_cancellationToken.IsCancellationRequested);
        }


        private void TextStreamer(object obj)
        {
            foreach (RelativityObjectSlim ros in _streamQueue.GetConsumingEnumerable())
            {
                if (_cancellationToken.IsCancellationRequested) break;

                RelativityObjectRef documentObjectRef = new RelativityObjectRef { ArtifactID = ros.ArtifactID };

                try
                {
                    foreach (int index in _longTextIds)
                    {
                        object longText = ros.Values[index];
                        if (longText != null && longText.ToString().Equals(_SHIBBOLETH))
                        {
                            IKeplerStream keplerStream = _objectManager
                                .StreamLongTextAsync(_workspaceId, documentObjectRef, _queryRequest.Fields.ElementAt(index)).Result;
                            Stream realStream = keplerStream.GetStreamAsync().Result;
                            ros.Values[index] = realStream;
                        }
                    }
                }
                catch (Exception exception)
                {
                    // Close any streams we already opened in this ROS
                    CloseOpenStreamsInRos(ros);
                    // Send error to handler and cancel
                    SendErrorAndCancel("StreamLongTextAsync failed", exception);
                    // Skip rest of loop
                    break;
                }

                try
                {
                    _openStreamQueue.Add(ros, _cancellationToken);
                }
                catch (OperationCanceledException) { }
            }

            // Close all open streams in the Open Stream Queue if we are canceling 

            if (_cancellationToken.IsCancellationRequested)
            {
                // close open streams left in open stream queue
                _openStreamQueue.CompleteAdding();
                foreach (RelativityObjectSlim rosToClose in _openStreamQueue.GetConsumingEnumerable())
                {
                    CloseOpenStreamsInRos(rosToClose);
                }
            }
        }

        private void HandlerCaller(object obj)
        {
            bool done = false;
            BlockingCollection<RelativityObjectSlim>[] queues = { _openStreamQueue, _standardQueue };

            while (!done && !_cancellationToken.IsCancellationRequested)
            {
                if (queues.All(q => q.IsCompleted))
                {
                    done = true;
                }
                else
                {
                    {
                        //Console.WriteLine("Std:" + _standardQueue.Count + " Str:" + _streamQueue.Count + " OpenStr:" + _openStreamQueue.Count);
                    }
                    RelativityObjectSlim ros;
                    if (BlockingCollection<RelativityObjectSlim>.TryTakeFromAny(queues, out ros) >= 0)
                    {
                        bool continueProcessing = false;

                        try
                        {
                            continueProcessing = _userHandler.Item(ros);
                        }
                        catch (Exception exception)
                        {
                            SendErrorAndCancel("Exception during IExportApiHandler's Item method", exception);
                        }

                        CloseOpenStreamsInRos(ros);

                        if (!continueProcessing)
                        {
                            _cancellationTokenSource.Cancel();
                        }
                    }
                }
            }
        }


        private void CloseOpenStreamsInRos(RelativityObjectSlim ros)
        {
            foreach (int index in _longTextIds)
            {
                object longText = ros.Values[index];
                (longText as Stream)?.Close();
            }
        }

        private void BlockConsumer(Object obj)
        {
            bool done = false;

            while (!done && !_cancellationToken.IsCancellationRequested)
            {
                RelativityObjectSlim[] currentBlock = null;

                try
                {
                    currentBlock =
                        _objectManager.RetrieveNextResultsBlockFromExportAsync(_workspaceId, _runId, _blockSize).Result;
                }
                catch (Exception exception)
                {
                    SendErrorAndCancel("RetrieveNextResultsBlockFromExportAsync failed", exception);
                    break;
                }

                if (currentBlock != null && currentBlock.Any())
                {
                    foreach (RelativityObjectSlim ros in currentBlock)
                    {
                        bool hasStream = false;

                        if (_cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }

                        foreach (int index in _longTextIds)
                        {
                            object longText = ros.Values[index];
                            if (longText != null && longText.ToString().Equals(_SHIBBOLETH))
                            {
                                hasStream = true;
                                break;
                            }
                        }

                        if (hasStream)
                        {
                            try
                            {
                                _streamQueue.Add(ros, _cancellationToken);
                            }
                            catch (OperationCanceledException) { }
                        }
                        else
                        {
                            try
                            {
                                _standardQueue.Add(ros, _cancellationToken);
                            }
                            catch (OperationCanceledException) { }
                        }
                    }
                }
                else
                {
                    done = true;
                }
            }
        }

        /// <summary>
        /// This method sends a error message/exception to the handler
        /// and cancels all working threads. Note the lock to assure
        /// that the handler's Error message will only be called once.
        /// </summary>
        /// <param name="message">Informational message</param>
        /// <param name="exception">The exception that caused the error</param>

        private void SendErrorAndCancel(string message, Exception exception)
        {
            lock (_thisLock)
            {
                if (!_cancellationToken.IsCancellationRequested)
                {
                    _userHandler.Error(message, exception);
                    _cancellationTokenSource.Cancel();
                }
            }
        }

        private ServiceFactory GetKeplerServiceFactory()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            ServicePointManager.DefaultConnectionLimit = 128;

            Uri restUri = new Uri(_relativityUrl, "Relativity.REST/api");
            Uri servicesUri = new Uri(_relativityUrl, "Relativity.REST/apiRelativity.Services");
            ServiceFactorySettings settings = new ServiceFactorySettings(servicesUri, restUri, _credentials);
            ServiceFactory factory = new ServiceFactory(settings);
            return factory;
        }

    }
}
