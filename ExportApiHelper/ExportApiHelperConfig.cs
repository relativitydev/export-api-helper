using System;
using System.Linq;
using Relativity.Services.Objects.DataContracts;
using Relativity.Services.ServiceProxy;

namespace Relativity.ObjectManager.ExportApiHelper
{
    public class ExportApiHelperConfig
    {
        /// <summary>
        /// The Relativity instance being targeted. This must be
        /// scheme and authority only e.g. https://relativity.mydomain.com
        /// </summary>
        public Uri RelativityUrl { get; set; }
        /// <summary>
        /// Must be a valid Credentials object that allows access to the Relativity instance
        /// </summary>
        public Credentials Credentials { get; set; }
        /// <summary>
        /// Workspace ID from which the documents will be exported
        /// </summary>
        public int WorkspaceId { get; set; } = 0;
        /// <summary>
        /// Number of documents to request at a time.
        /// Note: Each of this API's internal threads will request this
        /// number of documents each time. It's suggested to 
        /// leave the default. 
        /// </summary>
        public int BlockSize { get; set; } = 1000;
        /// <summary>
        /// Object Manager query request for the documents. 
        /// Note: To achieve maximum performance this library
        /// will deliver documents to the developer's implementation
        /// of IExportApiHandler out of order so setting Sorts in
        /// this object is contraindicated. 
        /// </summary>
        public QueryRequest QueryRequest { get; set; }
        /// <summary>
        /// A factor describing the amount of concurrency that should
        /// be used to retrieve document data. The exact number of threads
        /// that will be employed are up to the library to determine. 
        /// </summary>
        public int ScaleFactor { get; set; } = 4;

        public Relativity.ObjectManager.ExportApiHelper.ExportApiHelper Create()
        {
            if (RelativityUrl == null)
            {
                throw new ArgumentException("The 'RelativityUrl' property may not be null");
            }

            if (Credentials == null)
            {
                throw new ArgumentException("The 'Credentials' property may not be null");
            }

            if (WorkspaceId == 0)
            {
                throw new ArgumentException("The 'WorkspaceId' property must be set");
            }

            if (ScaleFactor < 1 || ScaleFactor > 16)
            {
                throw new ArgumentException("The 'ScaleFactor' property must be between 1 and 16");
            }

            if (QueryRequest == null)
            {
                throw new ArgumentException("The 'QueryRequest' property may not be null");
            }

            if (!QueryRequest.Fields.Any())
            {
                throw new ArgumentException("The 'QueryRequest.Fields' property must contain at least one field");
            }

            if (QueryRequest.ObjectType == null)
            {
                // Request documents by default
                QueryRequest.ObjectType = new ObjectTypeRef {ArtifactTypeID = 10};
            }



            return new Relativity.ObjectManager.ExportApiHelper.ExportApiHelper(RelativityUrl, Credentials, WorkspaceId, BlockSize, QueryRequest, ScaleFactor);

        }
    }
}
