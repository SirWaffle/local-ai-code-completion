using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PatternToolbox.DataStructures
{
    public class QueueDictionaryLocking<T>
    {
        private static SemaphoreSlim dictMutex = new SemaphoreSlim(1);

        Dictionary<string, List<T>> PendingDict = new();
        List<string> PendingQueue = new List<string>();

        public int QueueRequestBlocking(string key, T request)
        {
            dictMutex.Wait();

            //add to pending before we send request
            List<T>? list = null;
            if (!PendingDict.TryGetValue(key, out list))
            {
                list = new List<T>();
                PendingDict.Add(key, list);
            }

            list.Add(request);
            PendingQueue.Add(key);

            int len = list.Count;

            dictMutex.Release();

            return len;

        }

        public int GetRequestCount(string key)
        {
            List<T>? list = null;
            if (!PendingDict.TryGetValue(key, out list))
            {
                return 0;
            }

            return list == null ? 0 : list.Count();
        }

        public int GetRequestCount()
        {
            return PendingQueue.Count;
        }

        public bool TryGetAndPopNextRequestForUserBlocking(string key, ref T? req)
        {
            if (PendingQueue.Count == 0)
                return false;

            dictMutex.Wait();
            
            //add to pending before we send request
            List<T>? list = null;
            if (!PendingDict.TryGetValue(key, out list))
            {
                Console.WriteLine("Attempted to get next queued request, but user has no entries!");
            }
            else
            {
                if (list == null || list.Count == 0)
                {
                    Console.WriteLine("Attempted to get next queued request, but users entry has an empty list!");
                }
                else
                {
                    req = list[0];
                    list.RemoveAt(0);
                    PendingQueue.Remove(key);
                    dictMutex.Release();
                    return true;
                }
            }

            dictMutex.Release();
            return false;
        }

        public bool TryGetAndPopNextRequest(int timeoutMs, ref T? req)
        {
            if (PendingQueue.Count == 0)
                return false;

            dictMutex.Wait(timeoutMs);

            string key = PendingQueue[0];
            PendingQueue.RemoveAt(0);

            //add to pending before we send request
            List<T>? list = null;
            if (!PendingDict.TryGetValue(key, out list))
            {
                Console.WriteLine("Attempted to get next queued request, but the entry is missing!");
            }
            else
            {
                if(list == null || list.Count == 0)
                {
                    Console.WriteLine("Attempted to get next queued request, but the entry has an empty list!");
                }
                else
                {
                    req = list[0];
                    list.RemoveAt(0);
                    dictMutex.Release();
                    return true;
                }
            }

            dictMutex.Release();
            return false;
        }
    }
}
