using System;
using System.Text;

namespace ExportApiHelperSample
{

    public class Metrics
    {
        public int TotalCount;
        public long TotalSize;
        public long StartTime;
        public long StopTime;

        public void Begin()
        {
            StartTime = DateTime.Now.Ticks / 10000;
        }

        public void End()
        {
            StopTime = DateTime.Now.Ticks / 10000;
        }

        public void Add(Metrics other)
        {
            TotalCount += other.TotalCount;
            TotalSize += other.TotalSize;
        }

        public override string ToString()
        {
            long elapsed = 0;

            StringBuilder sb = new StringBuilder();

            sb.Append("TotalCount: " + TotalCount + " TotalSize:" + BytesToString(TotalSize) );

            if (StartTime != 0)
            {
                if (StopTime == 0)
                {
                    elapsed = (DateTime.Now.Ticks / 10000) - StartTime;
                }
                else
                {
                    elapsed = StopTime - StartTime;
                }
            }

            if (elapsed != 0)
            {
                long sizeThroughput = 1000 * TotalSize / elapsed;
                long filesThroughput = 1000 * TotalCount / elapsed;
                sb.Append(" ElapsedTime: " + elapsed + " StartTime: " + StartTime + " StopTime: " + StopTime + " Throughput: " + Metrics.BytesToString(sizeThroughput, true) + "/sec " + filesThroughput + " files/sec");
            }

            return sb.ToString();

        }

        public static string BytesToString(long byteCount, bool withSpace = false)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; //Longs run out around EB
            if (byteCount == 0)
                return "0" + suf[0];
            long bytes = Math.Abs(byteCount);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(byteCount) * num) + (withSpace ? " " : "") + suf[place];
        }
    }

}
