#region usings

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

#endregion

namespace KCDModMerger.Logging
{
    /// <summary>
    /// Log Entry
    /// </summary>
    internal class LogEntry
    {
        private static readonly List<string> callingMethods = new List<string>();
        private static int ThreadCounter;

        /// <summary>
        /// Initializes a new instance of the <see cref="LogEntry"/> class.
        /// </summary>
        /// <param name="methodName">Name of the method.</param>
        /// <param name="stack">The stack.</param>
        /// <param name="callTime">The call time.</param>
        /// <param name="parameters">The parameters.</param>
        [Log]
        internal LogEntry(string methodName, StackTrace stack, DateTime callTime, object[] parameters)
        {
            MethodName = methodName;
            CallTime = callTime;
            Parameters = parameters;
            Stack = stack;

            if (string.IsNullOrEmpty(Thread.CurrentThread.Name))
            {
                Thread.CurrentThread.Name = "Thread #" + ThreadCounter++;
            }

            ThreadName = Thread.CurrentThread.Name;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LogEntry"/> class.
        /// </summary>
        /// <param name="methodName">Name of the method.</param>
        /// <param name="stack">The stack.</param>
        /// <param name="callTime">The call time.</param>
        /// <param name="elapsed">The elapsed.</param>
        [Log]
        internal LogEntry(string methodName, StackTrace stack, DateTime callTime, TimeSpan elapsed)
        {
            MethodName = methodName;
            CallTime = callTime;
            ElapsedTime = elapsed;
            Stack = stack;

            if (string.IsNullOrEmpty(Thread.CurrentThread.Name))
            {
                Thread.CurrentThread.Name = "Thread #" + ThreadCounter++;
            }

            ThreadName = Thread.CurrentThread.Name;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LogEntry"/> class.
        /// </summary>
        /// <param name="methodName">Name of the method.</param>
        /// <param name="callTime">The call time.</param>
        /// <param name="elapsed">The elapsed time.</param>
        /// <param name="parameters">The parameters.</param>
        /// <param name="e">The Exception.</param>
        [Log]
        internal LogEntry(string methodName, DateTime callTime, TimeSpan elapsed, object[] parameters, Exception e)
        {
            MethodName = methodName;
            CallTime = callTime;
            ElapsedTime = elapsed;
            Parameters = parameters;
            Exception = e;

            if (string.IsNullOrEmpty(Thread.CurrentThread.Name))
            {
                Thread.CurrentThread.Name = "Thread #" + ThreadCounter++;
            }

            ThreadName = Thread.CurrentThread.Name;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LogEntry"/> class.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="addExclamation">if set to <c>true</c> [add exclamation].</param>
        /// <param name="stack">The stack.</param>
        [Log]
        internal LogEntry(string message, bool addExclamation, StackTrace stack)
        {
            Message = message;
            AddExclamation = addExclamation;
            CallTime = DateTime.Now;
            Stack = stack;

            if (string.IsNullOrEmpty(Thread.CurrentThread.Name))
            {
                Thread.CurrentThread.Name = "Thread #" + ThreadCounter++;
            }

            ThreadName = Thread.CurrentThread.Name;
        }

        internal string MethodName { get; set; }
        internal StackTrace Stack { get; set; }
        internal Exception Exception { get; set; }
        internal DateTime CallTime { get; set; }
        internal TimeSpan ElapsedTime { get; set; }
        internal object[] Parameters { get; set; }
        internal string Message { get; set; }
        internal bool AddExclamation { get; set; }
        internal string ThreadName { get; set; }

        [Log]
        public override string ToString()
        {
            if (Exception != null)
            {
                var sb = new StringBuilder();
                sb.AppendLine(BuildLog($"*** Error in {MethodName}{BuildStringFromParameters()}"));
                Logger.CreateExceptionString(Exception, "", sb);

                return sb.ToString();
            }

            if (!string.IsNullOrEmpty(Message))
            {
                return BuildLog(CheckForExclamation());
            }

            if (!ElapsedTime.IsDefault())
            {
                return BuildEndLog();
            }

            if (Stack != null && !CallTime.IsDefault())
            {
                return BuildStartLog();
            }

            return BuildLog($"{MethodName}{BuildStringFromParameters()}");
        }

        [Log]
        private string CheckForExclamation()
        {
            if (!AddExclamation && !Message.EndsWith("!") && !Message.EndsWith(".") && !Message.EndsWith(":") &&
                !Message.EndsWith("}") && !Message.EndsWith("]") && !Message.EndsWith("-") && !Message.EndsWith("?") &&
                !Message.EndsWith(")") && !Message.EndsWith(" ") && !Message.EndsWith(Environment.NewLine))
                return Message + "...";

            if (AddExclamation)
            {
                return Message + "!";
            }

            return Message;
        }

        [Log]
        private string BuildStartLog()
        {
            var caller = GetCallerName(Stack.GetFrame(2).GetMethod());

            lock (callingMethods)
            {
                callingMethods.Add(MethodName);
            }

            return
                $"[{CallTime.ToLongTimeString()}][{ThreadName}] {GetIndent()}{MethodName}{BuildStringFromParameters()} called by {caller}";
        }

        [Log]
        private string BuildEndLog()
        {
            return
                $"[{CallTime.ToLongTimeString()}][{ThreadName}] {GetIndent()}{MethodName} finished in {ElapsedTime.Seconds}.{ElapsedTime.Milliseconds}ms";
        }

        [Log]
        private string BuildLog(string msg)
        {
            return
                $"[{CallTime.ToLongTimeString()}][{ThreadName}] {GetIndent()}{GetCallerName(Stack.GetFrame(1).GetMethod())}: {msg}";
        }

        [Log]
        private string GetCallerName(MethodBase method)
        {
            return method.DeclaringType?.Name +
                   (!method.Name.StartsWith(".") ? "." : "") +
                   method.Name;
        }

        [Log]
        private string GetIndent()
        {
            var count = Stack.FrameCount - 1;

            foreach (StackFrame stackFrame in Stack.GetFrames())
            {
                var method = stackFrame.GetMethod();
                lock (callingMethods)
                {
                    if (method.DeclaringType?.Assembly.FullName != Assembly.GetAssembly(typeof(App)).FullName ||
                        method.CustomAttributes.Any(entry =>
                            entry.AttributeType == typeof(Log) || entry.AttributeType == typeof(STAThreadAttribute)) ||
                        !callingMethods.Contains(GetCallerName(method)))
                    {
                        count--;
                    }
                }
            }

            if (count < 0)
            {
                count = 0;
            }

            return new string('\t', count);
        }

        [Log]
        private string BuildStringFromParameters()
        {
            if (Parameters != null && Parameters.Length > 0)
            {
                var sb = new StringBuilder("(");

                foreach (object parameter in Parameters)
                {
                    if (parameter is null)
                    {
                        sb.Append("NULL");
                    }
                    else if (parameter is string s)
                    {
                        if (!string.IsNullOrEmpty(s))
                        {
                            sb.Append(parameter);
                        }
                    }
                    else if (parameter.GetType().IsPrimitive)
                    {
                        if (!string.IsNullOrEmpty(parameter.ToString()))
                        {
                            sb.Append(parameter);
                        }
                    }
                    else
                    {
                        sb.Append(parameter.GetType());
                    }

                    sb.Append(", ");
                }

                return $"{sb.ToString().TrimEnd().TrimEnd(',').TrimEnd().TrimEnd(',')})";
            }

            return "()";
        }
    }
}