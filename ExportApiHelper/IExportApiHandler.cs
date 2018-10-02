using System;
using Relativity.Services.DataContracts.DTOs.Results;
using Relativity.Services.Objects.DataContracts;

namespace Relativity.ObjectManager.ExportApiHelper
{
    public interface IExportApiHandler
    {
        /// <summary>
        /// Called before any documents are sent to the Item method unless an error
        /// occurs during initialization.
        /// </summary>
        /// <param name="results">Initialization results including the document count and field types</param>
        void Before(ExportInitializationResults results);

        /// <summary>
        /// Called once for each document
        /// </summary>
        /// <param name="item">Represents the fields requested for this document</param>
        /// <returns>A return value of false cancels the rest of the run</returns>
        bool Item(RelativityObjectSlim item);

        /// <summary>
        /// Called once and only once when an error or exception has occurred.
        /// Will not be called on a cancellation request. 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="exception"></param>
        void Error(string message, Exception exception);

        /// <summary>
        /// Called after all documents have been sent to the Item method or the process
        /// did not complete due to an error or cancellation request.
        /// <param name="complete">True if the process completed sucessfully</param>
        /// </summary>
        void After(bool complete);

        /// <summary>
        /// Determines if the handler is thread safe. If this property is false 
        /// the handler will be called from a single thread although document export will
        /// still occur in multiple threads. 
        /// </summary>
        bool ThreadSafe { get; }
    }
}
