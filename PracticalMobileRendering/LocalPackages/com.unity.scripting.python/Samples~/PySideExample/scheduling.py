import logging
import numbers
import queue
import sys
import threading
import time

# When processing jobs, we give clients this much time (in seconds) to respond
# within the same processing interval or wait for the next editor update.
process_jobs_max_batch_time = 1 / 90


# This is the job queue. Add to it via jobs.put or call_on_main_thread.
# The main thread calls process_jobs every editor update (or every 2s in
# standalone mode) to process all outstanding jobs and then wait a bit longer
# for some fast replies.
_jobs = queue.Queue()

#####################
# This connection delays some dispatching until the main thread gets to it.
# That's necessary for accessing some Unity objects. It's also a source of deadlocks,
# so we try to avoid delays if possible.
def call_on_main_thread(f, wait_for_result = True):
    """
    Call a function on the main thread.
    
    If wait_for_result is True, will block until the main thread processes it 
    and return the value, or raise the exception it raised.

    If wait_for_result is False, then None is returned and exceptions will not 
    be raised
    """
    if wait_for_result and threading.current_thread() is threading.main_thread():
        # Only execute (and block) if we're on the main thread and want to get 
        # the result/raise exceptions. Otherwise we just queue the job
        return f()

    condition = threading.Condition()
    return_value = []
    exception = []

    def job():
        with condition:
            try:
                return_value.append(f())
            except:
                # Hand the exception to the other thread.
                exception.append(sys.exc_info()[1])

                if not wait_for_result:
                    # raise the exception on the main thread as no one is 
                    # waiting on it
                    raise

            condition.notify()

    with condition:
        _jobs.put(job)

        if wait_for_result:
            condition.wait()
            if len(exception):
                raise exception[0]
            else:
                return return_value[0]
        
        return None

def process_jobs(batch_time = process_jobs_max_batch_time):
    """
    Call this from the main loop on every editor update.

    If there are any jobs to process, process them all and keep processing, or 
    wait for jobs for `batch_time` seconds, giving threads time to send
    another request quickly if needed. The implication of this is at the last 
    milisecond, a job with a run time of 10 hours can be processed. The corollary
    is that a batch_time lower that the length of a job can be used to make sure 
    we process only one job.
    """

    if not isinstance(batch_time, numbers.Number):
        raise TypeError("'batch_time' argument must be numeric")
    
    # The main thread always holds the GIL. Explicitly yield with a time.sleep
    # so other threads can push jobs in the jobs queue.
    time.sleep(0.001)

    if _jobs.empty():
        return

    start = time.time()
    remaining = batch_time
    while remaining > 0:
        try:
            job = _jobs.get(timeout=remaining)
            try:
                job()
            except Exception as e:
                msg = f"An unexpected Exception occured while processing the job {job}: {e}"
                UnityEngine.Debug.LogException(msg)
                print(msg)

            _jobs.task_done()
        except queue.Empty:
            break
        elapsed = (time.time() - start)
        remaining = batch_time - elapsed

def process_all_jobs():
    while not _jobs.empty():
        process_jobs()


def make_exec_on_main_thread_decorator(wait_for_result):
    def decorator(f):
        """
        Decorator that will queue a job (function) for execution on the main
        thread.

        If wait_for_result is true then the function will return and throw
        exceptions normally.

        If it's false then the function will return None immediately after
        queueing the job, and exceptions will not propagate.
        """
        def func_wrapper(*args, **kwargs):
            call_on_main_thread(lambda: f(*args,**kwargs), wait_for_result=wait_for_result)
        return func_wrapper
    return decorator

exec_on_main_thread = make_exec_on_main_thread_decorator(wait_for_result = True)
exec_on_main_thread_async = make_exec_on_main_thread_decorator(wait_for_result = False)