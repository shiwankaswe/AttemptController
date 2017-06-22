using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AttemptController.DataStructures
{
    public static class TaskParalllel
    {
        public delegate bool FunctionToGetWorkItem(out Task workTask);

        public static void RemoveCompletedTasks(Task[] tasks, ref int numTasksInProgress, Action<Exception> callOnException)
        {
            int taskNumber = 0;
            while (taskNumber < numTasksInProgress)
            {
                Task t = tasks[taskNumber];
                if (t.IsCompleted || t.IsCanceled || t.IsFaulted)
                {
                    --numTasksInProgress;
                    if (t.IsFaulted)
                        callOnException(t.Exception);
                    if (taskNumber < numTasksInProgress)
                        tasks[taskNumber] = tasks[numTasksInProgress];
                }
                else
                {
                    taskNumber++;
                }

            }
        }

        public static async Task Worker(FunctionToGetWorkItem getWorkItem, Action<Exception> callOnException, int maxParallel)
        {
            Task[] tasksInProgress = new Task[maxParallel];
            int numTasksInProgress = 0;
            while (true)
            {
                Task newTask;
                if (!getWorkItem(out newTask))
                {
                    if (numTasksInProgress > 0)
                        await Task.WhenAll(tasksInProgress.Take(numTasksInProgress));
                    return;
                }
                if (newTask.IsCompleted || newTask.IsCanceled)
                    continue;
                if (newTask.IsFaulted)
                {
                    callOnException(newTask.Exception);
                    continue;
                }

                if (numTasksInProgress > 0)
                {
                    RemoveCompletedTasks(tasksInProgress, ref numTasksInProgress, callOnException);

                    if (numTasksInProgress >= tasksInProgress.Length)
                    {
                        await Task.WhenAny(tasksInProgress);
                        RemoveCompletedTasks(tasksInProgress, ref numTasksInProgress, callOnException);
                    }
                }

                tasksInProgress[numTasksInProgress++] = newTask;
            }
        }

        public static async Task ForEachWithWorkers<T>(
            IEnumerable<T> items,
            Func<T, ulong, CancellationToken, Task> actionToRunAsync,
            Action<Exception> callOnException = null,
            int numWorkers = 64,
            int maxTasksPerWorker = 50,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Queue<T> workItems = new Queue<T>(items);
            ulong workItemNumber = 0;
            object workLock = new object();
            Task[] workerTasks = new Task[numWorkers];
            for (int i = 0; i < workerTasks.Length; i++)
            {
                workerTasks[i] = Task.Run( async () => await Worker( (out Task workItem) =>
                {
                    T item;
                    ulong thisWorkItemNumber = 0;
                    lock (workLock)
                    {
                        if (workItems.Count <= 0)
                        {
                            workItem = null;
                            return false;
                        }
                        item = workItems.Dequeue();
                        thisWorkItemNumber = workItemNumber++;
                    }
                    workItem = actionToRunAsync(item, thisWorkItemNumber, cancellationToken);
                    return true;
                }, callOnException, maxTasksPerWorker), cancellationToken);
            }
            await Task.WhenAll(workerTasks);
        }

        public static async Task RepeatWithWorkers(
            ulong numberOfTimesToRepeat,
            Func<ulong, CancellationToken, Task> actionToRunAsync,
            Action<Exception> callOnException = null,
            int numWorkers = 32,
            int maxTasksPerWorker = 50,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            ulong workItemNumber = 0;
            object workLock = new object();
            Task[] workerTasks = new Task[numWorkers];
            for (int i = 0; i < workerTasks.Length; i++)
            {
                workerTasks[i] = Task.Run(async () => await Worker((out Task workItem) =>
                {
                    ulong thisWorkItemNumber = 0;
                    lock (workLock)
                    {
                        if (workItemNumber >= numberOfTimesToRepeat)
                        {
                            workItem = null;
                            return false;
                        }
                        thisWorkItemNumber = workItemNumber++;
                    }
                    workItem = actionToRunAsync(thisWorkItemNumber, cancellationToken);
                    return true;
                }, callOnException, maxTasksPerWorker), cancellationToken);
            }
            await Task.WhenAll(workerTasks).ConfigureAwait(false);
        }



    }
}
