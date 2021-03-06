﻿// ***********************************************************************
// Copyright (c) 2020-2020 Charlie Poole, Terje Sandstrom
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// ***********************************************************************

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Serialization;
using System.Xml;
using NUnit.VisualStudio.TestAdapter.Internal;
using NUnit.VisualStudio.TestAdapter.NUnitEngine;

namespace NUnit.VisualStudio.TestAdapter.NUnitEngine
{
    public interface INUnitTestEvent
    {
        XmlNode Node { get; }
    }


    public abstract class NUnitTestEvent : NUnitTestNode
    {
        public enum ResultType
        {
            Failed,
            Success,
            Skipped,
            Warning,
            NoIdea
        }

        public enum SiteType
        {
            NoIdea,
            Test,
            Setup,
            TearDown
        }
        public enum TestTypes
        {
            NoIdea,
            TestFixture,
            TestMethod
        }

        public TestTypes TestType()
        {
            string type = Node.GetAttribute("type");
            switch (type)
            {
                case "TestFixture":
                    return TestTypes.TestFixture;
                case "TestMethod":
                    return TestTypes.TestMethod;
                default:
                    return TestTypes.NoIdea;
            }
        }

        public ResultType Result()
        {
            string res = Node.GetAttribute("result");
            switch (res)
            {
                case "Failed":
                    return ResultType.Failed;
                case "Passed":
                    return ResultType.Success;
                case "Skipped":
                    return ResultType.Skipped;
                case "Warning":
                    return ResultType.Warning;
                default:
                    return ResultType.NoIdea;
            }
        }

        public bool IsFailed => Result() == ResultType.Failed;

        public SiteType Site()
        {
            string site = Node.GetAttribute("site");
            switch (site)
            {
                case "SetUp":
                    return SiteType.Setup;
                case "TearDown":
                    return SiteType.TearDown;
                default:
                    return SiteType.NoIdea;
            }
        }

        public string Label => Node.GetAttribute("label");

        public bool IsIgnored => Label == "Ignored";

        public TimeSpan Duration => TimeSpan.FromSeconds(Node.GetAttribute("duration", 0.0));

        protected NUnitTestEvent(string testEvent) : this(XmlHelper.CreateXmlNode(testEvent))
        { }

        protected NUnitTestEvent(XmlNode node) : base(node)
        {
        }

        public string MethodName => Node.GetAttribute("methodname");
        public string ClassName => Node.GetAttribute("classname");
        public string Output => Node.SelectSingleNode("output")?.InnerText;


        public CheckedTime StartTime()
        {
            string startTime = Node.GetAttribute("start-time");
            return startTime != null
                ? new CheckedTime { Ok = true, Time = DateTimeOffset.Parse(startTime, CultureInfo.InvariantCulture) }
                : new CheckedTime { Ok = false, Time = DateTimeOffset.Now };
        }

        public CheckedTime EndTime()
        {
            string endTime = Node.GetAttribute("end-time");
            return endTime != null
                ? new CheckedTime { Ok = true, Time = DateTimeOffset.Parse(endTime, CultureInfo.InvariantCulture) }
                : new CheckedTime { Ok = false, Time = DateTimeOffset.Now };
        }

        public struct CheckedTime
        {
            public bool Ok;
            public DateTimeOffset Time;
        }
    }

    /// <summary>
    /// Handles the NUnit 'start-test' event.
    /// </summary>
    public class NUnitTestEventStartTest : NUnitTestEvent
    {
        public NUnitTestEventStartTest(INUnitTestEvent node) : this(node.Node)
        { }
        public NUnitTestEventStartTest(string testEvent) : this(XmlHelper.CreateXmlNode(testEvent))
        { }

        public NUnitTestEventStartTest(XmlNode node) : base(node)
        {
            if (node.Name != "start-test")
                throw new NUnitEventWrongTypeException($"Expected 'start-test', got {node.Name}");
        }
    }

    /// <summary>
    /// Handles the NUnit 'test-case' event.
    /// </summary>
    public class NUnitTestEventTestCase : NUnitTestEvent
    {
        public NUnitTestEventTestCase(INUnitTestEvent node) : this(node.Node)
        {
        }

        public NUnitTestEventTestCase(string testEvent) : this(XmlHelper.CreateXmlNode(testEvent))
        {
        }

        public NUnitFailure Failure { get; }

        public NUnitTestEventTestCase(XmlNode node) : base(node)
        {
            if (node.Name != "test-case")
                throw new NUnitEventWrongTypeException($"Expected 'test-case', got {node.Name}");
            var failureNode = Node.SelectSingleNode("failure");
            if (failureNode != null)
            {
                Failure = new NUnitFailure(
                    failureNode.SelectSingleNode("message")?.InnerText,
                    failureNode.SelectSingleNode("stack-trace")?.InnerText);
            }
            ReasonMessage = Node.SelectSingleNode("reason/message")?.InnerText;
        }

        public string ReasonMessage { get; }

        public bool HasReason => string.IsNullOrEmpty(ReasonMessage);
        public bool HasFailure => Failure != null;
    }

    public class NUnitProperty
    {
        public string Name { get; }
        public string Value { get; }

        public NUnitProperty(string name, string value)
        {
            Name = name;
            Value = value;
        }
    }

    public class NUnitFailure
    {
        public string Message { get; }
        public string Stacktrace { get; }

        public NUnitFailure(string message, string stacktrace)
        {
            Message = message;
            Stacktrace = stacktrace;
        }
    }


    /// <summary>
    /// Handles the NUnit 'test-suite' event.
    /// </summary>
    public class NUnitTestEventSuiteFinished : NUnitTestEvent
    {
        public NUnitTestEventSuiteFinished(INUnitTestEvent node) : this(node.Node)
        { }
        public NUnitTestEventSuiteFinished(string testEvent) : this(XmlHelper.CreateXmlNode(testEvent))
        { }

        public NUnitTestEventSuiteFinished(XmlNode node) : base(node)
        {
            if (node.Name != "test-suite")
                throw new NUnitEventWrongTypeException($"Expected 'test-suite', got {node.Name}");
            var failureNode = Node.SelectSingleNode("failure");
            if (failureNode != null)
            {
                FailureMessage = failureNode.SelectSingleNode("message")?.InnerText;
            }
            ReasonMessage = Node.SelectSingleNode("reason/message")?.InnerText;
        }

        public string ReasonMessage { get; }

        public bool HasReason => !string.IsNullOrEmpty(ReasonMessage);
        public string FailureMessage { get; }

        public bool HasFailure => !string.IsNullOrEmpty(FailureMessage);
    }

    [Serializable]
    public class NUnitEventWrongTypeException : Exception
    {
        // For guidelines regarding the creation of new exception types, see
        //    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/cpgenref/html/cpconerrorraisinghandlingguidelines.asp
        // and
        //    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/dncscol/html/csharp07192001.asp

        public NUnitEventWrongTypeException()
        {
        }

        public NUnitEventWrongTypeException(string message) : base(message)
        {
        }

        public NUnitEventWrongTypeException(string message, Exception inner) : base(message, inner)
        {
        }

        protected NUnitEventWrongTypeException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }
}
