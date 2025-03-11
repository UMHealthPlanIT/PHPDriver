using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using DataStationApi.Models;
using System.Threading;
using System.Diagnostics;

namespace DataStationApi.Services
{
    
    public class UloggerQueue
    {
        private static Queue<QueueItem> uQueue = new Queue<QueueItem>();
        static Task queueTask;
        static int queueLimit = 200;
        

        public static void AddToQueue(ULogEntryModel logEntry, ULoggerService uLoggerService)
        {
           
            QueueItem qI = new QueueItem() { ULoggerService = uLoggerService, uLogEntryModel = logEntry };
            Debug.WriteLine("Adding to log queue");
            if(uQueue.Count < queueLimit)
            {
                uQueue.Enqueue(qI);
                Debug.WriteLine($@"Queue count is at {uQueue.Count}");
            }
            
            
            
            

            if(queueTask == null)
            {
                queueTask = Task.Run(() => QueueLoop());
            }else if(queueTask.Status == TaskStatus.RanToCompletion)
            {
                Debug.WriteLine($@"{queueTask.Status} {queueTask.IsCompleted} {queueTask.Id}");
                queueTask = Task.Run(() => QueueLoop());
            }

            
        }

        private static void QueueLoop()
        {
            int counter = 1;
            Thread.Sleep(30 * counter * 1000);
            while (uQueue.Count != 0)
            {
                Debug.WriteLine("attempting to dequeue");
                try
                {
                    QueueItem log = uQueue.Peek();

                    log.ULoggerService.CreateNewLogRecord(log.uLogEntryModel);

                    uQueue.Dequeue();

                }catch(Exception e)
                {
                    counter++;
                    Debug.WriteLine("There was an issue attempting to dequeue, we will try again after a delay");
                    //we will sleep longer and longer if we keep failing the dequeue
                    Thread.Sleep(30 * counter * 1000);
                }
            }

            Debug.WriteLine("Queue is now empty");
        }

        class QueueItem
        {
            public ULogEntryModel uLogEntryModel { get; set; }
            public ULoggerService ULoggerService { get; set; }

        }
    }
}
