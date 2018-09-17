using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace ExportApiHelperSample
{
    /// <summary>
    /// Class that collects hashes of a series of strings and Streams
    /// into a single hash. It's built in such a way that the ultimate
    /// hash is invariant of the order the strings and Streams are 
    /// added. This allows repeatable hashes against a collection 
    /// of items regardless of order. 
    /// 
    /// The Add methods are thread safe
    /// </summary>

    public class HashCollector
    {
        private readonly long[] _md5Totals = new long[16];
        private readonly ThreadLocal<byte[]> _threadLocalBuffer = new ThreadLocal<byte[]>(() => new byte[1024 * 10]);

        public long Add(string s)
        {
            byte[] bytes = Encoding.Unicode.GetBytes(s);
            MD5 md5 = MD5.Create();
            md5.ComputeHash(bytes);
            AddToMd5Total(md5);
            return bytes.Length;
        }

        public long Add(Stream s)
        {
            int totalCount = 0;
            int count;
            byte[] buffer = _threadLocalBuffer.Value;

            MD5 md5 = MD5.Create();
            while ((count = s.Read(buffer, 0, buffer.Length)) != 0)
            {
                //Console.WriteLine(count);
                int skipBytes = (totalCount == 0 && buffer[0] == 0xFF && buffer[1] == 0xFE) ? 2 : 0;

                totalCount += count - skipBytes;
                md5.TransformBlock(buffer, skipBytes, count - skipBytes, null, 0);
            }
            md5.TransformFinalBlock(buffer, 0, 0);
            AddToMd5Total(md5);

            return totalCount;
        }

        public HashAlgorithm TotalHash
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                foreach (long t in _md5Totals)
                    sb.Append(t);
                MD5 md5 = MD5.Create();
                md5.ComputeHash(Encoding.ASCII.GetBytes(sb.ToString()));
                return md5;
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            byte[] bytes = TotalHash.Hash;
            foreach (byte t in bytes)
                sb.Append(t.ToString("x2"));
            return sb.ToString();
        }

        private void AddToMd5Total(HashAlgorithm md5)
        {
            byte[] bytes = md5.Hash;
            for (int i = 0; i < bytes.Length; i++)
            {
                Interlocked.Add(ref _md5Totals[i], bytes[i]);
            }
        }

    }
}
