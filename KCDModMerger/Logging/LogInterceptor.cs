#region usings

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Cauldron.Interception;

#endregion

namespace KCDModMerger.Logging
{
    [InterceptionRule(InterceptionRuleOptions.DoNotInterceptIfDecorated, typeof(Log))]
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = false)]
    internal sealed class LogInterceptor : Attribute, IMethodInterceptor
    {
        private DateTime callTime;
        private string methodName;
        private object[] parameters;
        private Stopwatch stopWatch;
        private StackTrace stack;

        // TODO: Use stacktrace to identify common callers and group calls
        // TODO: Log 'MethodName called by Caller' and 'MethodName elapsed stopwatch.Elapsed'
        [Log]
        public void OnEnter(Type declaringType, object instance, MethodBase methodbase, object[] values)
        {
            stack = new StackTrace();
            //Debug.WriteLine(stack.GetFrame(2).GetMethod().Name);
            methodName = declaringType.Name + (methodbase.Name.StartsWith(".") ? "" : ".") + methodbase.Name;
            callTime = DateTime.Now;
            parameters = values;
            stopWatch = Stopwatch.StartNew();

            Logger.Log(methodName, stack, callTime, parameters);
        }

        [Log]
        public bool OnException(Exception e)
        {
            stopWatch.Stop();
            Logger.Log(methodName, callTime, stopWatch.Elapsed, parameters, e);

            return true;
        }

        [Log]
        public void OnExit()
        {
            stopWatch.Stop();
            Logger.Log(methodName, stack, DateTime.Now, stopWatch.Elapsed);
        }
    }
}